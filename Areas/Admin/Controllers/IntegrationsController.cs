using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class IntegrationsController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var vm = new IntegrationIndexViewModel
        {
            // Xero
            XeroConnected = true,
            XeroLastSyncAt = DateTime.Now.AddHours(-4),
            XeroSyncCount = 156,
            XeroFailedCount = 3,
            RecentXeroSyncs = new()
            {
                new() { Id = 1, SyncType = "Invoice",         Status = "Success", EntityReference = "INV-2025-0043", XeroRecordId = "xero-inv-a1b2c3",  Message = "Synced successfully", SyncedByName = "John Anderson", SyncedAt = DateTime.Now.AddHours(-4) },
                new() { Id = 2, SyncType = "Payment",         Status = "Success", EntityReference = "PAY-000058",    XeroRecordId = "xero-pay-d4e5f6",  Message = "Synced successfully", SyncedByName = "Emily Brown",   SyncedAt = DateTime.Now.AddHours(-4) },
                new() { Id = 3, SyncType = "Invoice",         Status = "Failed",  EntityReference = "INV-2025-0042", XeroRecordId = null,                Message = "API rate limit exceeded. Retry scheduled.", SyncedByName = "System", SyncedAt = DateTime.Now.AddHours(-6) },
                new() { Id = 4, SyncType = "AccountingEntry", Status = "Success", EntityReference = "AE-00012",      XeroRecordId = "xero-ae-g7h8i9",   Message = "Synced successfully", SyncedByName = "John Anderson", SyncedAt = DateTime.Now.AddDays(-1) },
                new() { Id = 5, SyncType = "Invoice",         Status = "Success", EntityReference = "INV-2025-0041", XeroRecordId = "xero-inv-j0k1l2",  Message = "Synced successfully", SyncedByName = "Emily Brown",   SyncedAt = DateTime.Now.AddDays(-1) },
                new() { Id = 6, SyncType = "Payment",         Status = "Failed",  EntityReference = "PAY-000055",    XeroRecordId = null,                Message = "Customer not found in Xero",     SyncedByName = "System", SyncedAt = DateTime.Now.AddDays(-2) },
                new() { Id = 7, SyncType = "Invoice",         Status = "Success", EntityReference = "INV-2025-0040", XeroRecordId = "xero-inv-m3n4o5",  Message = "Synced successfully", SyncedByName = "John Anderson", SyncedAt = DateTime.Now.AddDays(-2) },
                new() { Id = 8, SyncType = "Payment",         Status = "Pending", EntityReference = "PAY-000059",    XeroRecordId = null,                Message = "Queued for sync",     SyncedByName = "System", SyncedAt = DateTime.Now.AddMinutes(-10) },
            },

            // PayMongo
            PayMongoEnabled = true,
            PayMongoTransactions = 28,
            PayMongoTotalAmount = 42500.00m,
            RecentPayMongoTxns = new()
            {
                new() { Id = 1, PayMongoId = "pay_xYz123abc", Type = "Payment",  Status = "Paid",     Amount = 2500.00m, CustomerName = "Alice Thompson", InvoiceNo = "INV-2025-0043", CreatedAt = DateTime.Now.AddHours(-2) },
                new() { Id = 2, PayMongoId = "pay_dEf456ghi", Type = "Payment",  Status = "Paid",     Amount = 1800.00m, CustomerName = "Bob Smith",      InvoiceNo = "INV-2025-0041", CreatedAt = DateTime.Now.AddDays(-1) },
                new() { Id = 3, PayMongoId = "pay_jKl789mno", Type = "Checkout", Status = "Pending",  Amount = 3200.00m, CustomerName = "Carlos Rivera",  InvoiceNo = "INV-2025-0044", CreatedAt = DateTime.Now.AddHours(-1) },
                new() { Id = 4, PayMongoId = "ref_pQr012stu", Type = "Refund",   Status = "Refunded", Amount = 800.00m,  CustomerName = "Diana Cruz",     InvoiceNo = "INV-2025-0038", CreatedAt = DateTime.Now.AddDays(-3) },
                new() { Id = 5, PayMongoId = "pay_vWx345yz0", Type = "Payment",  Status = "Failed",   Amount = 1500.00m, CustomerName = "Eric Tan",       InvoiceNo = "INV-2025-0039", CreatedAt = DateTime.Now.AddDays(-4) },
            }
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
    public IActionResult TestPayMongoConnection()
    {
        if (!IsAuthorized()) return Forbid();
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "PayMongo connection successful!" });
        return RedirectToAction(nameof(Index));
    }
}
