using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        if (roleClaim != UserRole.Admin.ToString())
        {
            return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        }

        var viewModel = new DashboardViewModel
        {
            UserRole = UserRole.Admin,
            UserName = User.Claims.FirstOrDefault(c => c.Type == "FullName")?.Value ?? "Shop Owner",
            ShopName = "ByteBill Main Shop", // TODO: Load from Shop table
            
            // KPI stats - TODO: Connect to API for real metrics
            TotalJobOrders = 0,
            PendingJobOrdersCount = 0,
            InProgressJobOrders = 0,
            CompletedToday = 0,
            TodayRevenue = 0m,
            WeekRevenue = 0m,
            MonthRevenue = 0m,
            PendingInvoices = 0,
            OutstandingAmount = 0m,
            LowStockItems = 0,
            
            // Monthly Revenue chart data (last 6 months)
            RevenueChart = new List<ChartDataPoint>(),
            
            // Job Order status distribution
            JobOrderChart = new List<ChartDataPoint>(),
            
            // Recent activity feed
            RecentActivity = new List<RecentActivityItem>(),
            
            // Recent Job Orders
            RecentJobOrders = new List<JobOrderSummary>(),
            
            // Pending Job Orders
            PendingJobOrders = new List<JobOrderSummary>()
        };
        
        return View(viewModel);
    }
}
