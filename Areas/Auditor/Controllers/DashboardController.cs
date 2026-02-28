using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Auditor.Controllers;

[Area("Auditor")]
[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Auditor.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        // ── Revenue MTD (confirmed payments) ──
        var revenueMtd = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed && p.PaymentDate >= thisMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        // ── Refunds MTD ──
        var refundsMtd = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.AdjustmentType == AdjustmentType.Refund
                     && a.Status == AdjustmentStatus.Approved && a.CreatedAt >= thisMonth)
            .SumAsync(a => (decimal?)a.Amount) ?? 0;

        // ── Adjustments MTD (credits + debits) ──
        var adjustmentsMtd = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.Status == AdjustmentStatus.Approved
                     && a.AdjustmentType != AdjustmentType.Refund && a.CreatedAt >= thisMonth)
            .SumAsync(a => (decimal?)a.Amount) ?? 0;

        // ── Voided Invoices MTD ──
        var voidedMtd = await _db.Invoices
            .CountAsync(i => i.ShopId == shopId && i.Status == InvoiceStatus.Void && i.CreatedAt >= thisMonth);

        // ── Recent Audit Activity (last 10) ──
        var recentActivity = await _db.AuditLogs
            .Where(a => a.ShopId == shopId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new RecentActivityItem
            {
                Icon = a.Action == "Create" ? "plus" : a.Action == "Update" ? "edit" : a.Action == "Delete" ? "trash-2"
                     : a.Action == "Login" ? "log-in" : a.Action == "StatusChange" ? "refresh-cw" : "activity",
                IconColor = a.Action == "Create" ? "success" : a.Action == "Update" ? "info"
                          : a.Action == "Delete" ? "danger" : a.Action == "Login" ? "primary" : "warning",
                Title = a.Action + " " + a.EntityName,
                Description = a.User != null ? a.User.FirstName + " " + a.User.LastName + (a.Details != null ? " — " + a.Details : "") : (a.Details ?? ""),
                TimeAgo = ""
            })
            .ToListAsync();

        // Calculate TimeAgo on the client side
        var activityTimes = await _db.AuditLogs
            .Where(a => a.ShopId == shopId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => a.CreatedAt)
            .ToListAsync();

        for (int i = 0; i < recentActivity.Count && i < activityTimes.Count; i++)
        {
            recentActivity[i].TimeAgo = GetTimeAgo(activityTimes[i]);
        }

        var viewModel = new DashboardViewModel
        {
            TodayRevenue = revenueMtd,
            RecentActivity = recentActivity,
            PendingJobOrders = new List<JobOrderSummary>()
        };

        ViewBag.TotalRevenueMTD = revenueMtd;
        ViewBag.TotalRefundsMTD = refundsMtd;
        ViewBag.TotalAdjustmentsMTD = adjustmentsMtd;
        ViewBag.VoidedInvoicesMTD = voidedMtd;

        return View(viewModel);
    }

    /// <summary>Polling endpoint for real-time Auditor dashboard updates.</summary>
    [HttpGet]
    public async Task<IActionResult> Poll()
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        var revenueMtd = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed && p.PaymentDate >= thisMonth)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;
        var refundsMtd = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.AdjustmentType == AdjustmentType.Refund
                     && a.Status == AdjustmentStatus.Approved && a.CreatedAt >= thisMonth)
            .SumAsync(a => (decimal?)a.Amount) ?? 0;
        var adjustmentsMtd = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.Status == AdjustmentStatus.Approved
                     && a.AdjustmentType != AdjustmentType.Refund && a.CreatedAt >= thisMonth)
            .SumAsync(a => (decimal?)a.Amount) ?? 0;
        var voidedMtd = await _db.Invoices
            .CountAsync(i => i.ShopId == shopId && i.Status == InvoiceStatus.Void && i.CreatedAt >= thisMonth);

        return Json(new { revenueMtd, refundsMtd, adjustmentsMtd, voidedMtd });
    }

    private static string GetTimeAgo(DateTime dt)
    {
        var span = DateTime.UtcNow - dt;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dt.ToString("MMM d");
    }
}
