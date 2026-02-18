using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        if (roleClaim != UserRole.SuperAdmin.ToString())
        {
            return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        }

        var viewModel = new DashboardViewModel
        {
            UserRole = UserRole.SuperAdmin,
            UserName = User.Claims.FirstOrDefault(c => c.Type == "FullName")?.Value ?? "Super Admin",
            ShopName = "System Administration",
            
            TotalJobOrders = 1250,
            PendingJobOrdersCount = 45,
            InProgressJobOrders = 82,
            CompletedToday = 28,
            TodayRevenue = 15420.50m,
            WeekRevenue = 78340.00m,
            MonthRevenue = 312580.00m,
            LowStockItems = 12,
            
            // System Revenue Trend (line chart) - last 6 months
            RevenueChart = new List<ChartDataPoint>
            {
                new() { Label = "Jan", Value = 245000 },
                new() { Label = "Feb", Value = 268000 },
                new() { Label = "Mar", Value = 290000 },
                new() { Label = "Apr", Value = 275000 },
                new() { Label = "May", Value = 310000 },
                new() { Label = "Jun", Value = 312580 }
            },
            
            // Shops Growth (bar chart) - last 6 months
            JobOrderChart = new List<ChartDataPoint>
            {
                new() { Label = "Jan", Value = 2 },
                new() { Label = "Feb", Value = 3 },
                new() { Label = "Mar", Value = 3 },
                new() { Label = "Apr", Value = 4 },
                new() { Label = "May", Value = 4 },
                new() { Label = "Jun", Value = 5 }
            },
            
            RecentActivity = new List<RecentActivityItem>
            {
                new() { Title = "New Shop Registration", Description = "TechFix Pro joined the platform", Icon = "store", IconColor = "success", TimeAgo = "2 min ago", BadgeText = "New", BadgeClass = "status-success" },
                new() { Title = "User Account Created", Description = "John Smith (Admin) at QuickRepairs", Icon = "user", IconColor = "primary", TimeAgo = "15 min ago" },
                new() { Title = "System Update", Description = "Invoice module updated to v2.3", Icon = "settings", IconColor = "info", TimeAgo = "1 hr ago", BadgeText = "System", BadgeClass = "status-info" },
                new() { Title = "Shop Suspended", Description = "OldTech Solutions - Payment overdue", Icon = "alert", IconColor = "warning", TimeAgo = "2 hrs ago", BadgeText = "Alert", BadgeClass = "status-warning" },
                new() { Title = "Payment Received", Description = "QuickRepairs - Monthly subscription", Icon = "dollar-sign", IconColor = "success", TimeAgo = "3 hrs ago" }
            },
            
            PendingJobOrders = new List<JobOrderSummary>()
        };
        
        ViewBag.TotalShops = 5;
        ViewBag.ActiveShops = 4;
        ViewBag.NewShopsThisMonth = 1;
        ViewBag.TotalUsers = 24;
        ViewBag.SystemRevenue = viewModel.MonthRevenue;
        ViewBag.ActiveJobs = viewModel.InProgressJobOrders;
        
        return View(viewModel);
    }
}
