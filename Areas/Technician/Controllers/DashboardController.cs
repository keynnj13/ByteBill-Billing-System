using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Technician.Controllers;

[Area("Technician")]
[Authorize]
public class DashboardController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Technician.ToString();
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var viewModel = new DashboardViewModel
        {
            TodayRevenue = 0,
            PendingInvoices = 0,
            PaidToday = 0,
            OutstandingBalance = 0,
            RecentActivity = new List<RecentActivityItem>
            {
                new() { Icon = "wrench", IconColor = "primary", Title = "Job assigned", Description = "JO-2024-0157 - Dell XPS 15 needs diagnosis", TimeAgo = "10 min ago" },
                new() { Icon = "check-circle", IconColor = "success", Title = "Job completed", Description = "JO-2024-0156 - MacBook Pro repair finished", TimeAgo = "30 min ago" },
                new() { Icon = "wrench", IconColor = "warning", Title = "In progress", Description = "JO-2024-0154 - iPhone screen replacement", TimeAgo = "1 hour ago" }
            },
            PendingJobOrders = new List<JobOrderSummary>
            {
                new() { Id = 157, OrderNumber = "JO-2024-0157", CustomerName = "Sarah Chen", Status = JobOrderStatus.CheckedIn, DeviceType = "Dell XPS 15", StatusBadgeClass = "info" },
                new() { Id = 154, OrderNumber = "JO-2024-0154", CustomerName = "Bob Martinez", Status = JobOrderStatus.InProgress, DeviceType = "iPhone 14 Pro", StatusBadgeClass = "warning" },
                new() { Id = 153, OrderNumber = "JO-2024-0153", CustomerName = "Alice Thompson", Status = JobOrderStatus.WaitingForParts, DeviceType = "HP Pavilion", StatusBadgeClass = "secondary" }
            }
        };
        
        ViewBag.MyJobsToday = 4;
        ViewBag.CompletedToday = 2;
        ViewBag.InProgress = 2;
        ViewBag.WaitingParts = 1;
        
        return View(viewModel);
    }
}
