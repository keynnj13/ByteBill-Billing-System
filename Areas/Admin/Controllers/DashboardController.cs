using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? period, DateTime? from, DateTime? to)
    {
        if (!User.IsInRoles("Admin"))
            return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        // Resolve date range from preset or custom
        var (dateFrom, dateTo, activePeriod) = ResolveDateRange(period, from, to);

        var vm = await BuildDashboardAsync(dateFrom, dateTo);
        vm.ActivePeriod = activePeriod;
        vm.FilterFrom = dateFrom;
        vm.FilterTo = dateTo;
        return View(vm);
    }

    /// <summary>Real-time polling endpoint – returns all dashboard data as JSON.</summary>
    [HttpGet]
    public async Task<IActionResult> Poll(string? period, DateTime? from, DateTime? to)
    {
        if (!User.IsInRoles("Admin"))
            return Unauthorized();

        var (dateFrom, dateTo, _) = ResolveDateRange(period, from, to);
        var data = await BuildDashboardDataAsync(dateFrom, dateTo);
        return Json(data);
    }

    private static (DateTime from, DateTime to, string period) ResolveDateRange(string? period, DateTime? from, DateTime? to)
    {
        var today = DateTime.UtcNow.Date;
        return period switch
        {
            "today"    => (today, today.AddDays(1).AddTicks(-1), "today"),
            "week"     => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(1).AddTicks(-1), "week"),
            "month"    => (new DateTime(today.Year, today.Month, 1), today.AddDays(1).AddTicks(-1), "month"),
            "last30"   => (today.AddDays(-30), today.AddDays(1).AddTicks(-1), "last30"),
            "last3mo"  => (today.AddMonths(-3), today.AddDays(1).AddTicks(-1), "last3mo"),
            "custom" when from.HasValue && to.HasValue =>
                (from.Value.Date, to.Value.Date.AddDays(1).AddTicks(-1), "custom"),
            _ => (DateTime.MinValue, DateTime.MaxValue, "all")  // no filter = all time
        };
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Shared data-fetching helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private async Task<DashboardViewModel> BuildDashboardAsync(DateTime dateFrom, DateTime dateTo)
    {
        var shopId = User.GetShopId();
        var shopName = await _db.Shops
            .Where(s => s.ShopId == shopId)
            .Select(s => s.ShopName)
            .FirstOrDefaultAsync() ?? "My Shop";

        var data = await BuildDashboardDataAsync(dateFrom, dateTo);

        return new DashboardViewModel
        {
            UserRole = UserRole.Admin,
            UserName = User.GetFullName(),
            ShopName = shopName,
            TotalJobOrders = data.TotalJobOrders,
            PendingJobOrdersCount = data.PendingJobs,
            InProgressJobOrders = data.InProgressJobs,
            CompletedToday = data.CompletedToday,
            TodayRevenue = data.TodayRevenue,
            WeekRevenue = data.WeekRevenue,
            PeriodRevenue = data.PeriodRevenue,
            PendingInvoices = data.UnpaidInvoices,
            OutstandingAmount = data.OutstandingAmount,
            LowStockItems = data.LowStockCount,
            RevenueChart = data.RevenueChart,
            JobOrderChart = data.JobOrderChart,
            RecentJobOrders = data.RecentJobOrders,
            RecentActivity = data.RecentActivity
        };
    }

    private async Task<DashboardPollDto> BuildDashboardDataAsync(DateTime dateFrom, DateTime dateTo)
    {
        var shopId = User.GetShopId();
        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var hasDateFilter = dateFrom != DateTime.MinValue;

        // ── Job Order counts (filtered by CreatedAt if date range specified) ──
        var joBase = _db.JobOrders.Where(j => j.ShopId == shopId);
        if (hasDateFilter) joBase = joBase.Where(j => j.CreatedAt >= dateFrom && j.CreatedAt <= dateTo);

        var jobOrders = await joBase
            .GroupBy(j => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(j => j.Status == JobOrderStatus.Pending),
                InProgress = g.Count(j => j.Status == JobOrderStatus.InProgress
                                       || j.Status == JobOrderStatus.Diagnosis
                                       || j.Status == JobOrderStatus.CheckedIn),
                CompletedToday = g.Count(j => j.Status == JobOrderStatus.Completed && j.UpdatedAt != null && j.UpdatedAt.Value.Date == today)
            })
            .FirstOrDefaultAsync();

        // ── Revenue (confirmed payments – filtered by date range) ──
        var payBase = _db.Payments.Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed);
        if (hasDateFilter) payBase = payBase.Where(p => p.PaymentDate >= dateFrom && p.PaymentDate <= dateTo);

        var todayRevenue = await payBase
            .Where(p => p.PaymentDate.Date == today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var weekRevenue = await payBase
            .Where(p => p.PaymentDate.Date >= weekStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var periodRevenue = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed)
            .Where(p => !hasDateFilter || (p.PaymentDate >= dateFrom && p.PaymentDate <= dateTo))
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        // ── Invoices (filtered) ──
        var invBase = _db.Invoices.Where(i => i.ShopId == shopId);
        if (hasDateFilter) invBase = invBase.Where(i => i.InvoiceDate >= dateFrom && i.InvoiceDate <= dateTo);

        var invoiceStats = await invBase
            .GroupBy(i => 1)
            .Select(g => new
            {
                Unpaid = g.Count(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial),
                Outstanding = g.Where(i => i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial).Sum(i => (decimal?)i.Balance) ?? 0m
            })
            .FirstOrDefaultAsync();

        // ── Low stock ──
        var lowStockCount = await _db.InventoryItems
            .Where(i => i.ShopId == shopId && i.IsActive && i.QtyOnHand <= i.ReorderLevel)
            .CountAsync();

        // ── Monthly Revenue chart (last 6 months or within date range) ──
        var chartStart = hasDateFilter ? new DateTime(dateFrom.Year, dateFrom.Month, 1) : new DateTime(today.Year, today.Month, 1).AddMonths(-5);
        var chartEnd = hasDateFilter ? dateTo : DateTime.MaxValue;
        var monthlyRevenue = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed && p.PaymentDate >= chartStart && p.PaymentDate <= chartEnd)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var revenueChart = new List<ChartDataPoint>();
        var chartMonths = hasDateFilter
            ? (int)((dateTo.Year - dateFrom.Year) * 12 + dateTo.Month - dateFrom.Month) + 1
            : 6;
        chartMonths = Math.Min(chartMonths, 12); // cap at 12
        for (int i = 0; i < chartMonths; i++)
        {
            var dt = chartStart.AddMonths(i);
            var val = monthlyRevenue.FirstOrDefault(m => m.Year == dt.Year && m.Month == dt.Month);
            revenueChart.Add(new ChartDataPoint
            {
                Label = dt.ToString("MMM yyyy"),
                Value = val?.Total ?? 0m
            });
        }

        // ── Job Order Status distribution chart (filtered) ──
        var statusBase = _db.JobOrders.Where(j => j.ShopId == shopId);
        if (hasDateFilter) statusBase = statusBase.Where(j => j.CreatedAt >= dateFrom && j.CreatedAt <= dateTo);

        var statusDistribution = await statusBase
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var jobOrderChart = statusDistribution
            .Select(s => new ChartDataPoint { Label = s.Status.ToString(), Value = s.Count })
            .OrderByDescending(c => c.Value)
            .ToList();

        // ── Recent Job Orders (last 10) ──
        var recentJobs = await _db.JobOrders
            .Where(j => j.ShopId == shopId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(10)
            .Select(j => new JobOrderSummary
            {
                Id = j.JobOrderId,
                JobNumber = j.JobOrderNo,
                OrderNumber = j.JobOrderNo,
                CustomerName = j.Customer!.FirstName + " " + j.Customer.LastName,
                DeviceType = j.Device!.DeviceType,
                Status = j.Status,
                StatusBadgeClass = j.Status == JobOrderStatus.Completed ? "success"
                    : j.Status == JobOrderStatus.InProgress ? "primary"
                    : j.Status == JobOrderStatus.Pending ? "warning"
                    : j.Status == JobOrderStatus.Cancelled ? "danger"
                    : j.Status == JobOrderStatus.WaitingForParts ? "secondary"
                    : "info",
                CreatedAt = j.CreatedAt,
                TechnicianName = j.AssignedTechUser != null
                    ? j.AssignedTechUser.FirstName + " " + j.AssignedTechUser.LastName
                    : null
            })
            .ToListAsync();

        // ── Recent Activity (from status history, last 15) ──
        var recentStatusChanges = await _db.JobOrderStatusHistories
            .Where(h => _db.JobOrders.Any(j => j.ShopId == shopId && j.JobOrderId == h.JobOrderId))
            .OrderByDescending(h => h.ChangedAt)
            .Take(15)
            .Select(h => new
            {
                h.NewStatus,
                h.OldStatus,
                h.ChangedAt,
                h.Remarks,
                UserName = h.ChangedByUser!.FirstName + " " + h.ChangedByUser.LastName,
                JobOrderNo = h.JobOrder!.JobOrderNo
            })
            .ToListAsync();

        var recentActivity = recentStatusChanges.Select(h =>
        {
            var (icon, color) = h.NewStatus switch
            {
                "Pending" => ("plus", "primary"),
                "CheckedIn" => ("log-in", "info"),
                "Diagnosis" => ("search", "info"),
                "InProgress" => ("play", "primary"),
                "Completed" => ("check-circle", "success"),
                "Cancelled" => ("x-circle", "danger"),
                "WaitingForParts" => ("pause", "warning"),
                _ => ("activity", "primary")
            };
            return new RecentActivityItem
            {
                Icon = icon,
                IconColor = color,
                Title = $"{h.JobOrderNo} → {h.NewStatus}",
                Description = $"By {h.UserName}" + (string.IsNullOrEmpty(h.Remarks) ? "" : $" — {h.Remarks}"),
                TimeAgo = GetTimeAgo(h.ChangedAt)
            };
        }).ToList();

        return new DashboardPollDto
        {
            TotalJobOrders = jobOrders?.Total ?? 0,
            PendingJobs = jobOrders?.Pending ?? 0,
            InProgressJobs = jobOrders?.InProgress ?? 0,
            CompletedToday = jobOrders?.CompletedToday ?? 0,
            TodayRevenue = todayRevenue,
            WeekRevenue = weekRevenue,
            PeriodRevenue = periodRevenue,
            UnpaidInvoices = invoiceStats?.Unpaid ?? 0,
            OutstandingAmount = invoiceStats?.Outstanding ?? 0m,
            LowStockCount = lowStockCount,
            RevenueChart = revenueChart,
            JobOrderChart = jobOrderChart,
            RecentJobOrders = recentJobs,
            RecentActivity = recentActivity
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

/// <summary>DTO returned by the Poll endpoint for real-time dashboard updates.</summary>
public class DashboardPollDto
{
    public int TotalJobOrders { get; set; }
    public int PendingJobs { get; set; }
    public int InProgressJobs { get; set; }
    public int CompletedToday { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal PeriodRevenue { get; set; }
    public int UnpaidInvoices { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int LowStockCount { get; set; }
    public List<ChartDataPoint> RevenueChart { get; set; } = new();
    public List<ChartDataPoint> JobOrderChart { get; set; } = new();
    public List<JobOrderSummary> RecentJobOrders { get; set; } = new();
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
}
