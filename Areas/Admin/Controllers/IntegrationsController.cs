using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class IntegrationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly PayMongoSettings _payMongoSettings;

    public IntegrationsController(ApplicationDbContext db, IOptions<PayMongoSettings> payMongoSettings)
    {
        _db = db;
        _payMongoSettings = payMongoSettings.Value;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();

        // ── Xero Sync Logs (real DB) ──
        var xeroLogs = await _db.XeroSyncLogs
            .Where(x => x.ShopId == shopId)
            .OrderByDescending(x => x.SyncedAt)
            .Take(10)
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
            .Take(10)
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
            XeroConnected = xeroTotalSyncs > 0,
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SyncXero()
    {
        if (!IsAuthorized()) return Forbid();
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Xero sync initiated. This may take a few minutes." });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestPayMongoConnection()
    {
        if (!IsAuthorized()) return Forbid();

        try
        {
            if (string.IsNullOrWhiteSpace(_payMongoSettings.SecretKey))
                return Json(new { success = false, message = "PayMongo Secret Key is not configured." });

            using var http = new HttpClient();
            var authBytes = Encoding.UTF8.GetBytes($"{_payMongoSettings.SecretKey}:");
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            // Test by listing payment methods — lightweight API call
            var response = await http.GetAsync($"{_payMongoSettings.BaseUrl}/links?limit=1");

            if (response.IsSuccessStatusCode)
                return Json(new { success = true, message = "PayMongo connection successful! API keys are valid." });

            var body = await response.Content.ReadAsStringAsync();
            return Json(new { success = false, message = $"PayMongo returned {(int)response.StatusCode}: {body}" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Connection failed: {ex.Message}" });
        }
    }

    /// <summary>Returns PayMongo transaction details as JSON for the detail modal.</summary>
    [HttpGet]
    public async Task<IActionResult> PayMongoTxnDetail(long id)
    {
        if (!IsAuthorized()) return Forbid();
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
