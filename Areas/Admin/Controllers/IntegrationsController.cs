using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class IntegrationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PayMongoSettings _payMongoSettings;
    private readonly IXeroService _xero;
    private readonly IPayMongoService _payMongo;

    public IntegrationsController(
        ApplicationDbContext db,
        IOptions<PayMongoSettings> payMongoSettings,
        IXeroService xero,
        IPayMongoService payMongo)
    {
        _db = db;
        _payMongoSettings = payMongoSettings.Value;
        _xero = xero;
        _payMongo = payMongo;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    private static bool IsAjaxRequest(string? requestedWith)
        => string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();

        // ── Xero Sync Logs (real DB) ──
        var xeroLogs = await _db.XeroSyncLogs
            .Where(x => x.ShopId == shopId)
            .OrderByDescending(x => x.SyncedAt)
            .Select(x => new XeroSyncLogItem
            {
                Id = x.XeroSyncLogId,
                SyncType = x.SyncType,
                Status = x.Status,
                EntityReference = x.InvoiceId.HasValue
                    ? _db.Invoices.Where(i => i.InvoiceId == x.InvoiceId).Select(i => i.InvoiceNo).FirstOrDefault()
                    : x.PaymentId.HasValue
                        ? _db.Payments.Where(p => p.PaymentId == x.PaymentId).Select(p => p.PaymentNo).FirstOrDefault()
                        : x.AccountingEntryId.HasValue ? "AE-" + x.AccountingEntryId : null,
                XeroRecordId = x.XeroRecordId,
                Message = x.Message,
                SyncedByName = x.SyncedByUser != null
                    ? x.SyncedByUser.FirstName + " " + x.SyncedByUser.LastName
                    : "System",
                SyncedAt = x.SyncedAt
            })
            .ToListAsync();

        var xeroTotalSyncs = await _db.XeroSyncLogs.CountAsync(x => x.ShopId == shopId);
        var xeroFailedCount = await _db.XeroSyncLogs.CountAsync(x => x.ShopId == shopId && x.Status == "Failed");
        var xeroLastSync = await _db.XeroSyncLogs
            .Where(x => x.ShopId == shopId && x.Status == "Success")
            .OrderByDescending(x => x.SyncedAt)
            .Select(x => (DateTime?)x.SyncedAt)
            .FirstOrDefaultAsync();

        // ── PayMongo Transactions (real DB) ──
        var payMongoTxnCount = await _db.PayMongoTxns.CountAsync(t => t.ShopId == shopId);
        var payMongoTotalAmount = await _db.PayMongoTxns
            .Where(t => t.ShopId == shopId && t.PayMongoStatus == "paid")
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        var recentPayMongoTxns = await _db.PayMongoTxns
            .Where(t => t.ShopId == shopId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new PayMongoTxnItem
            {
                Id = t.PayMongoTxnId,
                PayMongoId = t.PayMongoPaymentIntentId,
                Type = t.ResourceType == "checkout_session" ? "Checkout"
                     : t.PayMongoStatus == "refunded" ? "Refund" : "Payment",
                Status = t.PayMongoStatus == "paid" ? "Paid"
                       : t.PayMongoStatus == "failed" ? "Failed"
                       : t.PayMongoStatus == "refunded" ? "Refunded" : "Pending",
                Amount = t.Amount,
                CustomerName = t.Invoice != null && t.Invoice.Customer != null
                    ? t.Invoice.Customer.FirstName + " " + t.Invoice.Customer.LastName : null,
                InvoiceNo = t.Invoice != null ? t.Invoice.InvoiceNo : null,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        var payMongoHasKeys = !string.IsNullOrWhiteSpace(_payMongoSettings.SecretKey)
                           && !string.IsNullOrWhiteSpace(_payMongoSettings.PublicKey);

        var vm = new IntegrationIndexViewModel
        {
            // Xero
            XeroConnected = await _xero.IsConnectedAsync(shopId),
            XeroLastSyncAt = xeroLastSync,
            XeroSyncCount = xeroTotalSyncs,
            XeroFailedCount = xeroFailedCount,
            RecentXeroSyncs = xeroLogs,

            // PayMongo
            PayMongoEnabled = payMongoHasKeys,
            PayMongoTransactions = payMongoTxnCount,
            PayMongoTotalAmount = payMongoTotalAmount,
            RecentPayMongoTxns = recentPayMongoTxns,

            // Management UI data
            PayMongoWebhookUrl = $"{Request.Scheme}://{Request.Host}/api/paymongoapi/webhook",
            PayMongoHasKeys = payMongoHasKeys,
            PayMongoKeyLastFour = !string.IsNullOrWhiteSpace(_payMongoSettings.SecretKey) && _payMongoSettings.SecretKey.Length > 4
                ? "****" + _payMongoSettings.SecretKey[^4..]
                : null
        };

        return View(vm);
    }

    // ── Xero OAuth 2.0 ────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConnectXero()
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        var authUrl = _xero.GetAuthorizationUrl(shopId);
        return Redirect(authUrl);
    }

    /// <summary>Direct GET link to initiate Xero OAuth (for browser navigation).</summary>
    [HttpGet]
    public IActionResult ConnectXeroDirect()
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        var authUrl = _xero.GetAuthorizationUrl(shopId);
        return Redirect(authUrl);
    }

    /// <summary>OAuth 2.0 callback from Xero — exchanges code for tokens.</summary>
    [HttpGet]
    [AllowAnonymous] // Xero redirects here with code + state
    public async Task<IActionResult> XeroCallback(string code, string state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            TempData["XeroError"] = "Invalid Xero callback — missing parameters.";
            return RedirectToAction(nameof(Index));
        }

        // Decode shop ID from state
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(state));
            if (!decoded.StartsWith("shop:") || !long.TryParse(decoded[5..], out var shopId))
            {
                TempData["XeroError"] = "Invalid state parameter.";
                return RedirectToAction(nameof(Index));
            }

            var success = await _xero.ExchangeCodeForTokensAsync(shopId, code);
            if (success)
                TempData["XeroSuccess"] = "Successfully connected to Xero!";
            else
                TempData["XeroError"] = "Failed to connect to Xero. Please try again.";
        }
        catch
        {
            TempData["XeroError"] = "Error processing Xero callback.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectXero(
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        await _xero.DisconnectAsync(shopId);

        if (IsAjaxRequest(requestedWith))
            return Json(new { success = true, message = "Xero disconnected successfully." });

        TempData["XeroSuccess"] = "Xero disconnected.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncXero(
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        var isConnected = await _xero.IsConnectedAsync(shopId);
        if (!isConnected)
        {
            if (IsAjaxRequest(requestedWith))
                return Json(new { success = false, message = "Xero is not connected. Please connect first." });
            TempData["XeroError"] = "Xero is not connected.";
            return RedirectToAction(nameof(Index));
        }

        var (synced, failed) = await _xero.SyncAllAsync(shopId, userId);
        var message = $"Sync complete: {synced} synced, {failed} failed.";

        if (IsAjaxRequest(requestedWith))
            return Json(new { success = true, message, synced, failed });

        TempData[synced > 0 ? "XeroSuccess" : "XeroError"] = message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestPayMongoConnection()
    {
        if (!IsAuthorized()) return Forbid();

        var (success, message) = await _payMongo.TestConnectionAsync();
        return Json(new { success, message });
    }

    /// <summary>Diagnostic: show available Xero accounts for debugging payment sync.</summary>
    [HttpGet]
    public async Task<IActionResult> TestXeroAccounts()
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        var result = await _xero.TestXeroAccountsAsync(shopId);
        return Json(result);
    }

    /// <summary>Returns PayMongo transaction details as JSON for the detail modal.</summary>
    [HttpGet]
    public async Task<IActionResult> PayMongoTxnDetail(long id)
    {
        if (!IsAuthorized()) return Forbid();

        if (id <= 0)
            ModelState.AddModelError(nameof(id), "Invalid transaction id.");

        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request." });

        var shopId = User.GetShopId();

        var txn = await _db.PayMongoTxns
            .Where(t => t.PayMongoTxnId == id && t.ShopId == shopId)
            .Select(t => new
            {
                t.PayMongoTxnId,
                t.PayMongoPaymentIntentId,
                t.ResourceType,
                t.PayMongoStatus,
                t.PayMongoPaymentMethod,
                t.Amount,
                t.CheckoutUrl,
                t.CreatedAt,
                t.UpdatedAt,
                InvoiceNo = t.Invoice != null ? t.Invoice.InvoiceNo : null,
                CustomerName = t.Invoice != null && t.Invoice.Customer != null
                    ? t.Invoice.Customer.FirstName + " " + t.Invoice.Customer.LastName : null,
                PaymentNo = t.Payment != null ? t.Payment.PaymentNo : null,
                PaymentDate = t.Payment != null ? (DateTime?)t.Payment.PaymentDate : null
            })
            .FirstOrDefaultAsync();

        if (txn == null) return NotFound();
        return Json(txn);
    }
}
