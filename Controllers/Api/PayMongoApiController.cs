using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.PayMongo;
using ByteBill_BS.Extensions;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ByteBill_BS.Controllers.Api;

/// <summary>
/// PayMongo API — handles payment link creation, checkout sessions, and webhooks.
/// Webhook endpoint is unauthenticated (validated via Svix signature).
/// All other endpoints require BillingOrAbove.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PayMongoApiController : ControllerBase
{
    private readonly IPayMongoService _svc;
    private readonly ILogger<PayMongoApiController> _logger;

    public PayMongoApiController(IPayMongoService svc, ILogger<PayMongoApiController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    // ── Create Payment Link ──────────────────────────────────────────
    // POST api/paymongoapi/link
    [HttpPost("link")]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> CreatePaymentLink([FromBody] CreatePayMongoLinkRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _svc.CreatePaymentLinkAsync(shopId, userId, req);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    // ── Create Checkout Session ──────────────────────────────────────
    // POST api/paymongoapi/checkout
    [HttpPost("checkout")]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest req)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _svc.CreateCheckoutSessionAsync(shopId, userId, req);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    // ── Get Status ───────────────────────────────────────────────────
    // GET api/paymongoapi/status/5
    [HttpGet("status/{txnId:long}")]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> GetStatus(long txnId)
    {
        var shopId = User.GetShopId();
        var status = await _svc.GetStatusAsync(shopId, txnId);

        if (status is null)
            return NotFound(ApiResponse<object>.Fail("PayMongo transaction not found."));

        return Ok(ApiResponse<PayMongoStatusDto>.Ok(status));
    }

    // ── Get Transactions by Invoice ──────────────────────────────────
    // GET api/paymongoapi/invoice/5
    [HttpGet("invoice/{invoiceId:long}")]
    [Authorize(Policy = "BillingOrAbove")]
    public async Task<IActionResult> GetByInvoice(long invoiceId)
    {
        var shopId = User.GetShopId();
        var txns = await _svc.GetByInvoiceAsync(shopId, invoiceId);
        return Ok(ApiResponse<List<PayMongoStatusDto>>.Ok(txns));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WEBHOOK — No authentication, validated via Svix signature
    // ═══════════════════════════════════════════════════════════════════
    // POST api/paymongoapi/webhook
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(
        [FromBody] JsonElement payload,
        [FromHeader(Name = "Svix-Id")] string? svixId,
        [FromHeader(Name = "Svix-Timestamp")] string? svixTimestamp,
        [FromHeader(Name = "Svix-Signature")] string? svixSignature)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            _logger.LogWarning("PayMongo webhook received empty body");
            return BadRequest(new { error = "Empty body" });
        }

        var rawBody = payload.GetRawText();

        if (string.IsNullOrEmpty(rawBody))
        {
            _logger.LogWarning("PayMongo webhook received empty body");
            return BadRequest(new { error = "Empty body" });
        }

        var handled = await _svc.HandleWebhookAsync(rawBody, svixId, svixTimestamp, svixSignature);

        if (!handled)
        {
            _logger.LogWarning("PayMongo webhook event was not handled");
            // Still return 200 to prevent PayMongo from retrying for unhandled events
        }

        return Ok(new { received = true });
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  Payment Success/Cancel pages (MVC controller for redirects)
// ═══════════════════════════════════════════════════════════════════════
[Route("payment")]
public class PaymentCallbackController : Controller
{
    private readonly IPayMongoService _payMongo;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PaymentCallbackController> _logger;

    public PaymentCallbackController(IPayMongoService payMongo, ApplicationDbContext db, ILogger<PaymentCallbackController> logger)
    {
        _payMongo = payMongo;
        _db = db;
        _logger = logger;
    }

    [HttpGet("success")]
    public async Task<IActionResult> Success(long? invoice)
    {
        if (invoice.HasValue)
        {
            try
            {
                // Auto-verify payment with PayMongo API and record it
                var recorded = await _payMongo.VerifyAndRecordPaymentAsync(invoice.Value);

                if (User.Identity?.IsAuthenticated == true)
                {
                    TempData["Success"] = recorded
                        ? "Payment recorded successfully!"
                        : "Payment is being processed. It will appear shortly.";

                    var role = User.FindFirst("Role")?.Value ?? "";
                    var area = role is "Admin" or "SuperAdmin" ? "Admin" : "Billing";

                    if (recorded)
                    {
                        // Find the payment record to redirect to its receipt
                        var paymentId = await _db.PayMongoTxns
                            .Where(t => t.InvoiceId == invoice.Value && t.PayMongoStatus == "paid" && t.PaymentId.HasValue)
                            .OrderByDescending(t => t.UpdatedAt)
                            .Select(t => t.PaymentId)
                            .FirstOrDefaultAsync();

                        if (paymentId.HasValue)
                            return RedirectToAction("Receipt", "Payments", new { area, id = paymentId.Value });
                    }

                    return RedirectToAction("Index", "Invoices", new { area });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify PayMongo payment for invoice {InvoiceId}", invoice);
            }
        }

        // Fallback for unauthenticated users or if verification fails
        ViewBag.InvoiceId = invoice;
        ViewBag.Status = "success";
        return View("PaymentCallback");
    }

    [HttpGet("cancel")]
    public IActionResult Cancel(long? invoice)
    {
        if (User.Identity?.IsAuthenticated == true && invoice.HasValue)
        {
            TempData["Error"] = "Payment was cancelled. No charges were made.";
            var role = User.FindFirst("Role")?.Value ?? "";
            if (role is "Admin" or "SuperAdmin")
                return RedirectToAction("Index", "Invoices", new { area = "Admin" });
            else
                return RedirectToAction("Index", "Invoices", new { area = "Billing" });
        }

        ViewBag.InvoiceId = invoice;
        ViewBag.Status = "cancel";
        return View("PaymentCallback");
    }
}
