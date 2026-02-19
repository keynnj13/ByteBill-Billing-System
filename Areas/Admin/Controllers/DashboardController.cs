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
    public async Task<IActionResult> Index()
    {
        if (!User.IsInRoles("Admin"))
            return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var vm = await BuildDashboardAsync();
        return View(vm);
    }

    /// <summary>Real-time polling endpoint – returns all dashboard data as JSON.</summary>
    [HttpGet]
    public async Task<IActionResult> Poll()
    {
        if (!User.IsInRoles("Admin"))
            return Unauthorized();

        var data = await BuildDashboardDataAsync();
        return Json(data);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  Shared data-fetching helpers
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private async Task<DashboardViewModel> BuildDashboardAsync()
    {
        var shopId = User.GetShopId();
        var shopName = await _db.Shops
            .Where(s => s.ShopId == shopId)
            .Select(s => s.ShopName)
            .FirstOrDefaultAsync() ?? "My Shop";

        var data = await BuildDashboardDataAsync();

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
            PendingInvoices = data.UnpaidInvoices,
            OutstandingAmount = data.OutstandingAmount,
            LowStockItems = data.LowStockCount,
            RevenueChart = data.RevenueChart,
            JobOrderChart = data.JobOrderChart,
            RecentJobOrders = data.RecentJobOrders,
            RecentActivity = data.RecentActivity
        };
    }

    private async Task<DashboardPollDto> BuildDashboardDataAsync()
    {
        var shopId = User.GetShopId();
        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);

        // ── Job Order counts ──
        var jobOrders = await _db.JobOrders
            .Where(j => j.ShopId == shopId)
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

        // ── Revenue (confirmed payments) ──
        var todayRevenue = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed && p.PaymentDate.Date == today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var weekRevenue = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed && p.PaymentDate.Date >= weekStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        // ── Invoices ──
        var invoiceStats = await _db.Invoices
            .Where(i => i.ShopId == shopId)
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

        // ── Monthly Revenue chart (last 6 months) ──
        var sixMonthsAgo = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
        var monthlyRevenue = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed && p.PaymentDate >= sixMonthsAgo)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var revenueChart = new List<ChartDataPoint>();
        for (int i = 0; i < 6; i++)
        {
            var dt = sixMonthsAgo.AddMonths(i);
            var val = monthlyRevenue.FirstOrDefault(m => m.Year == dt.Year && m.Month == dt.Month);
            revenueChart.Add(new ChartDataPoint
            {
                Label = dt.ToString("MMM yyyy"),
                Value = val?.Total ?? 0m
            });
        }

        // ── Job Order Status distribution chart ──
        var statusDistribution = await _db.JobOrders
            .Where(j => j.ShopId == shopId)
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
                StatusBadgeClass = j.Status == JobOrderStatus.Completed || j.Status == JobOrderStatus.Delivered ? "success"
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
                "Delivered" => ("package", "success"),
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
    public int UnpaidInvoices { get; set; }
    public decimal OutstandingAmount { get; set; }
    public int LowStockCount { get; set; }
    public List<ChartDataPoint> RevenueChart { get; set; } = new();
    public List<ChartDataPoint> JobOrderChart { get; set; } = new();
    public List<JobOrderSummary> RecentJobOrders { get; set; } = new();
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
}
