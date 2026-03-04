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
    public async Task<IActionResult> Index(string? period, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        var (dateFrom, dateTo, activePeriod) = ResolveDateRange(period, from, to);
        var hasDateFilter = dateFrom != DateTime.MinValue;
        var now = DateTime.UtcNow;
        var today = now.Date;

        // ── Revenue (confirmed payments – filtered) ──
        var payBase = _db.Payments.Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed);
        if (hasDateFilter) payBase = payBase.Where(p => p.PaymentDate >= dateFrom && p.PaymentDate <= dateTo);
        var periodRevenue = await payBase.SumAsync(p => (decimal?)p.Amount) ?? 0;

        // ── Refunds (filtered) ──
        var refBase = _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.AdjustmentType == AdjustmentType.Refund && a.Status == AdjustmentStatus.Approved);
        if (hasDateFilter) refBase = refBase.Where(a => a.CreatedAt >= dateFrom && a.CreatedAt <= dateTo);
        var periodRefunds = await refBase.SumAsync(a => (decimal?)a.Amount) ?? 0;

        // ── Adjustments (filtered) ──
        var adjBase = _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.Status == AdjustmentStatus.Approved && a.AdjustmentType != AdjustmentType.Refund);
        if (hasDateFilter) adjBase = adjBase.Where(a => a.CreatedAt >= dateFrom && a.CreatedAt <= dateTo);
        var periodAdjustments = await adjBase.SumAsync(a => (decimal?)a.Amount) ?? 0;

        // ── Voided Invoices (filtered) ──
        var voidBase = _db.Invoices.Where(i => i.ShopId == shopId && i.Status == InvoiceStatus.Void);
        if (hasDateFilter) voidBase = voidBase.Where(i => i.CreatedAt >= dateFrom && i.CreatedAt <= dateTo);
        var voidedCount = await voidBase.CountAsync();

        // ── Invoice stats (filtered where applicable) ──
        var invCountBase = _db.Invoices.Where(i => i.ShopId == shopId);
        if (hasDateFilter) invCountBase = invCountBase.Where(i => i.CreatedAt >= dateFrom && i.CreatedAt <= dateTo);
        var totalInvoicesInPeriod = await invCountBase.CountAsync();
        var unpaidInvoices = await _db.Invoices
            .CountAsync(i => i.ShopId == shopId && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial));
        var overdueInvoices = await _db.Invoices
            .CountAsync(i => i.ShopId == shopId && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial)
                          && i.DueDate.HasValue && i.DueDate.Value < today);
        var outstandingBalance = await _db.Invoices
            .Where(i => i.ShopId == shopId && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial))
            .SumAsync(i => (decimal?)i.Balance) ?? 0;

        // ── Payment count (filtered) ──
        var paymentCount = await payBase.CountAsync();

        // ── Collection rate ──
        var totalBilled = periodRevenue + outstandingBalance;
        var collectionRate = totalBilled > 0 ? Math.Round(periodRevenue / totalBilled * 100, 1) : 100;

        // ── Revenue chart – last 7 days or within date range ──
        var chartStart = hasDateFilter ? dateFrom : today.AddDays(-6);
        var chartEnd = hasDateFilter ? dateTo : today.AddDays(1).AddTicks(-1);
        var dailyRevenue = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed
                     && p.PaymentDate >= chartStart && p.PaymentDate <= chartEnd)
            .GroupBy(p => p.PaymentDate.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var revenueChart = new List<ChartDataPoint>();
        var chartDays = hasDateFilter
            ? Math.Min((int)(dateTo.Date - dateFrom.Date).TotalDays + 1, 14)
            : 7;
        for (int d = 0; d < chartDays; d++)
        {
            var date = (hasDateFilter ? dateFrom : today.AddDays(-6)).AddDays(d);
            var amount = dailyRevenue.FirstOrDefault(r => r.Date == date.Date)?.Total ?? 0;
            revenueChart.Add(new ChartDataPoint { Label = date.ToString("ddd"), Value = amount });
        }

        // ── Invoice status breakdown (filtered) ──
        var paidCount = await invCountBase.CountAsync(i => i.Status == InvoiceStatus.Paid);
        var unpaidCount = await invCountBase.CountAsync(i => i.Status == InvoiceStatus.Unpaid);
        var partialCount = await invCountBase.CountAsync(i => i.Status == InvoiceStatus.Partial);
        var voidCount = voidedCount;

        // ── Recent 5 invoices ──
        var recentInvoices = await _db.Invoices
            .Where(i => i.ShopId == shopId)
            .OrderByDescending(i => i.CreatedAt)
            .Take(5)
            .Select(i => new RecentInvoiceItem
            {
                Id = i.InvoiceId,
                InvoiceNumber = i.InvoiceNo,
                CustomerName = i.Customer != null ? i.Customer.FirstName + " " + i.Customer.LastName : "N/A",
                Total = i.TotalAmount,
                Balance = i.Balance,
                Status = i.Status.ToString(),
                StatusClass = i.Status == InvoiceStatus.Paid ? "status-success"
                            : i.Status == InvoiceStatus.Void ? "status-danger"
                            : i.Status == InvoiceStatus.Partial ? "status-warning" : "status-info",
                CreatedAt = i.CreatedAt,
                DueDate = i.DueDate
            })
            .ToListAsync();

        // ── Recent 5 payments ──
        var recentPayments = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed)
            .OrderByDescending(p => p.PaymentDate)
            .Take(5)
            .Select(p => new RecentPaymentItem
            {
                Id = p.PaymentId,
                PaymentNumber = p.PaymentNo,
                CustomerName = p.Customer != null ? p.Customer.FirstName + " " + p.Customer.LastName : "N/A",
                Amount = p.Amount,
                Method = p.Method.ToString(),
                PaidAt = p.PaymentDate
            })
            .ToListAsync();

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
            PeriodRevenue = periodRevenue,
            TodayRevenue = periodRevenue,
            MonthlyRevenue = periodRevenue,
            OutstandingBalance = outstandingBalance,
            TotalInvoices = totalInvoicesInPeriod,
            UnpaidInvoices = unpaidInvoices,
            OverdueInvoices = overdueInvoices,
            RecentActivity = recentActivity,
            RecentInvoices = recentInvoices,
            RecentPayments = recentPayments,
            RevenueChart = revenueChart,
            PendingJobOrders = new List<JobOrderSummary>(),
            ActivePeriod = activePeriod,
            FilterFrom = dateFrom == DateTime.MinValue ? null : dateFrom,
            FilterTo = dateTo == DateTime.MaxValue ? null : dateTo
        };

        ViewBag.TotalRevenueMTD = periodRevenue;
        ViewBag.TotalRefundsMTD = periodRefunds;
        ViewBag.TotalAdjustmentsMTD = periodAdjustments;
        ViewBag.VoidedInvoicesMTD = voidedCount;
        ViewBag.PaymentCountMTD = paymentCount;
        ViewBag.CollectionRate = collectionRate;
        ViewBag.PaidCount = paidCount;
        ViewBag.UnpaidCount = unpaidCount;
        ViewBag.PartialCount = partialCount;
        ViewBag.VoidCount = voidCount;

        return View(viewModel);
    }

    /// <summary>Polling endpoint for real-time Auditor dashboard updates.</summary>
    [HttpGet]
    public async Task<IActionResult> Poll(string? period, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        var (dateFrom, dateTo, _) = ResolveDateRange(period, from, to);
        var hasDateFilter = dateFrom != DateTime.MinValue;

        var payBase = _db.Payments.Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed);
        if (hasDateFilter) payBase = payBase.Where(p => p.PaymentDate >= dateFrom && p.PaymentDate <= dateTo);
        var revenue = await payBase.SumAsync(p => (decimal?)p.Amount) ?? 0;

        var refBase = _db.CreditDebitAdjustments.Where(a => a.ShopId == shopId && a.AdjustmentType == AdjustmentType.Refund && a.Status == AdjustmentStatus.Approved);
        if (hasDateFilter) refBase = refBase.Where(a => a.CreatedAt >= dateFrom && a.CreatedAt <= dateTo);
        var refunds = await refBase.SumAsync(a => (decimal?)a.Amount) ?? 0;

        var adjBase = _db.CreditDebitAdjustments.Where(a => a.ShopId == shopId && a.Status == AdjustmentStatus.Approved && a.AdjustmentType != AdjustmentType.Refund);
        if (hasDateFilter) adjBase = adjBase.Where(a => a.CreatedAt >= dateFrom && a.CreatedAt <= dateTo);
        var adjustments = await adjBase.SumAsync(a => (decimal?)a.Amount) ?? 0;

        var voidBase = _db.Invoices.Where(i => i.ShopId == shopId && i.Status == InvoiceStatus.Void);
        if (hasDateFilter) voidBase = voidBase.Where(i => i.CreatedAt >= dateFrom && i.CreatedAt <= dateTo);
        var voided = await voidBase.CountAsync();

        return Json(new { revenueMtd = revenue, refundsMtd = refunds, adjustmentsMtd = adjustments, voidedMtd = voided });
    }

    private static (DateTime from, DateTime to, string period) ResolveDateRange(string? period, DateTime? from, DateTime? to)
    {
        var today = DateTime.UtcNow.Date;
        return period switch
        {
            "today"   => (today, today.AddDays(1).AddTicks(-1), "today"),
            "week"    => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(1).AddTicks(-1), "week"),
            "month"   => (new DateTime(today.Year, today.Month, 1), today.AddDays(1).AddTicks(-1), "month"),
            "last30"  => (today.AddDays(-30), today.AddDays(1).AddTicks(-1), "last30"),
            "last3mo" => (today.AddMonths(-3), today.AddDays(1).AddTicks(-1), "last3mo"),
            "custom" when from.HasValue && to.HasValue =>
                (from.Value.Date, to.Value.Date.AddDays(1).AddTicks(-1), "custom"),
            _ => (DateTime.MinValue, DateTime.MaxValue, "all")
        };
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
