using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class DashboardController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Billing.ToString();
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var viewModel = new DashboardViewModel
        {
            TodayRevenue = 2850.00m,
            PendingInvoices = 12,
            PaidToday = 8,
            OutstandingBalance = 4280.00m,
            RecentActivity = new List<RecentActivityItem>
            {
                new() { Icon = "credit-card", IconColor = "success", Title = "Payment received", Description = "Mike Johnson paid $450 for INV-2024-0142", TimeAgo = "5 min ago" },
                new() { Icon = "receipt", IconColor = "primary", Title = "Invoice created", Description = "INV-2024-0143 created for Sarah Chen", TimeAgo = "15 min ago" },
                new() { Icon = "credit-card", IconColor = "success", Title = "Payment received", Description = "Bob Martinez paid $320 for INV-2024-0140", TimeAgo = "1 hour ago" },
                new() { Icon = "receipt", IconColor = "warning", Title = "Invoice overdue", Description = "INV-2024-0128 for Alice Thompson is overdue", TimeAgo = "2 hours ago" }
            },
            PendingJobOrders = new List<JobOrderSummary>
            {
                new() { Id = 1, OrderNumber = "JO-2024-0156", CustomerName = "Mike Johnson", Status = JobOrderStatus.Completed, DeviceType = "MacBook Pro", StatusBadgeClass = "success" },
                new() { Id = 2, OrderNumber = "JO-2024-0154", CustomerName = "Sarah Chen", Status = JobOrderStatus.Completed, DeviceType = "Dell XPS 15", StatusBadgeClass = "success" },
                new() { Id = 3, OrderNumber = "JO-2024-0152", CustomerName = "Carol White", Status = JobOrderStatus.Completed, DeviceType = "iPhone 14", StatusBadgeClass = "success" }
            }
        };
        
        return View(viewModel);
    }
}
