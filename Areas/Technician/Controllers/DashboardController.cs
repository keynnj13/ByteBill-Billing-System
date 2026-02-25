using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Technician.Controllers;

[Area("Technician")]
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
        return roleClaim == UserRole.Technician.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var today = DateTime.UtcNow.Date;

        // Get technician's assigned jobs
        var myJobs = _db.JobOrders
            .Where(j => j.ShopId == shopId && j.AssignedTechUserId == userId && !j.IsArchived);

        // Stats
        var myJobsToday = await myJobs
            .CountAsync(j => j.CreatedAt >= today);

        var completedToday = await myJobs
            .CountAsync(j => j.Status == JobOrderStatus.Completed && j.UpdatedAt >= today);

        var inProgress = await myJobs
            .CountAsync(j => j.Status == JobOrderStatus.InProgress || j.Status == JobOrderStatus.Diagnosis);

        var waitingParts = await myJobs
            .CountAsync(j => j.Status == JobOrderStatus.WaitingForParts);

        // Recent activity from status history
        var recentActivity = await _db.JobOrderStatusHistories
            .Where(h => h.ChangedByUserId == userId)
            .OrderByDescending(h => h.ChangedAt)
            .Take(10)
            .Select(h => new
            {
                h.JobOrderId,
                JobOrderNo = h.JobOrder!.JobOrderNo,
                h.OldStatus,
                h.NewStatus,
                h.ChangedAt,
                h.Remarks
            })
            .ToListAsync();

        var activityItems = recentActivity.Select(a =>
        {
            var (icon, color) = a.NewStatus switch
            {
                "Completed" => ("check-circle", "success"),
                "InProgress" => ("clock", "primary"),
                "WaitingForParts" => ("package", "warning"),
                "Diagnosis" => ("search", "primary"),
                _ => ("activity", "primary")
            };

            return new RecentActivityItem
            {
                Icon = icon,
                IconColor = color,
                Title = $"{a.JobOrderNo} → {a.NewStatus}",
                Description = !string.IsNullOrEmpty(a.Remarks) ? a.Remarks : $"Status changed from {a.OldStatus}",
                TimeAgo = GetTimeAgo(a.ChangedAt)
            };
        }).ToList();

        // Work queue — pending/active jobs
        var workQueue = await myJobs
            .Where(j => j.Status != JobOrderStatus.Completed
                && j.Status != JobOrderStatus.Delivered
                && j.Status != JobOrderStatus.Cancelled)
            .OrderByDescending(j => j.Priority == "Urgent" ? 0 : j.Priority == "High" ? 1 : 2)
            .ThenByDescending(j => j.CreatedAt)
            .Take(10)
            .Select(j => new JobOrderSummary
            {
                Id = j.JobOrderId,
                JobNumber = j.JobOrderNo,
                OrderNumber = j.JobOrderNo,
                CustomerName = j.Customer!.FirstName + " " + j.Customer.LastName,
                DeviceType = j.Device!.DeviceType + " - " + j.Device.Brand + " " + j.Device.Model,
                Status = j.Status,
                StatusBadgeClass = j.Status == JobOrderStatus.Pending ? "pending" :
                    j.Status == JobOrderStatus.InProgress ? "primary" :
                    j.Status == JobOrderStatus.WaitingForParts ? "warning" :
                    j.Status == JobOrderStatus.Diagnosis ? "info" : "muted",
                CreatedAt = j.CreatedAt
            })
            .ToListAsync();

        var viewModel = new DashboardViewModel
        {
            TodayRevenue = 0,
            PendingInvoices = 0,
            PaidToday = 0,
            OutstandingBalance = 0,
            RecentActivity = activityItems,
            PendingJobOrders = workQueue
        };

        ViewBag.MyJobsToday = myJobsToday;
        ViewBag.CompletedToday = completedToday;
        ViewBag.InProgress = inProgress;
        ViewBag.WaitingParts = waitingParts;

        return View(viewModel);
    }

    private static string GetTimeAgo(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dateTime.ToString("MMM d");
    }
}
