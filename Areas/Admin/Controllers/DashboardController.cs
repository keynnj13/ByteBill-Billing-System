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
            ShopName = "TechFix Pro",
            
            // KPI stats
            TotalJobOrders = 245,
            PendingJobOrdersCount = 12,
            InProgressJobOrders = 18,
            CompletedToday = 8,
            TodayRevenue = 3420.50m,
            WeekRevenue = 18340.00m,
            MonthRevenue = 72580.00m,
            PendingInvoices = 15,
            OutstandingAmount = 8450.00m,
            LowStockItems = 5,
            
            // Monthly Revenue chart data (last 6 months)
            RevenueChart = new List<ChartDataPoint>
            {
                new() { Label = "Oct", Value = 52400m },
                new() { Label = "Nov", Value = 61200m },
                new() { Label = "Dec", Value = 48900m },
                new() { Label = "Jan", Value = 65800m },
                new() { Label = "Feb", Value = 72580m },
                new() { Label = "Mar", Value = 58300m }
            },
            
            // Job Order status distribution
            JobOrderChart = new List<ChartDataPoint>
            {
                new() { Label = "Pending",      Value = 12 },
                new() { Label = "In Progress",  Value = 18 },
                new() { Label = "Diagnosed",    Value = 8 },
                new() { Label = "Completed",    Value = 195 },
                new() { Label = "Cancelled",    Value = 5 },
                new() { Label = "Awaiting Parts", Value = 7 }
            },
            
            // Recent activity feed
            RecentActivity = new List<RecentActivityItem>
            {
                new() { Title = "Payment Received", Description = "INV-2024-0142 — ₱450.00 from Mike Johnson", Icon = "credit-card", IconColor = "success", TimeAgo = "5 min ago" },
                new() { Title = "Job Order Created", Description = "JO-2024-0089 — Laptop screen replacement", Icon = "clipboard-list", IconColor = "primary", TimeAgo = "20 min ago" },
                new() { Title = "Invoice Sent", Description = "INV-2024-0145 to Sarah Chen", Icon = "file-invoice", IconColor = "info", TimeAgo = "45 min ago" },
                new() { Title = "Job Completed", Description = "JO-2024-0085 — Desktop virus removal", Icon = "check-circle", IconColor = "success", TimeAgo = "1 hr ago" },
                new() { Title = "Low Stock Alert", Description = "DDR4 RAM 8GB — only 2 left", Icon = "alert", IconColor = "danger", TimeAgo = "2 hr ago" }
            },
            
            // Recent Job Orders
            RecentJobOrders = new List<JobOrderSummary>
            {
                new() { Id = 1, JobNumber = "JO-2024-0089", OrderNumber = "JO-2024-0089", CustomerName = "Alice Thompson", DeviceType = "Laptop",  Status = JobOrderStatus.Pending,          StatusBadgeClass = "warning",   CreatedAt = DateTime.Now.AddMinutes(-20), TechnicianName = "Unassigned" },
                new() { Id = 2, JobNumber = "JO-2024-0088", OrderNumber = "JO-2024-0088", CustomerName = "Bob Martinez",   DeviceType = "Desktop", Status = JobOrderStatus.InProgress,       StatusBadgeClass = "primary",   CreatedAt = DateTime.Now.AddHours(-2),    TechnicianName = "David Lee" },
                new() { Id = 3, JobNumber = "JO-2024-0087", OrderNumber = "JO-2024-0087", CustomerName = "Carol White",    DeviceType = "Phone",   Status = JobOrderStatus.AwaitingApproval, StatusBadgeClass = "warning",   CreatedAt = DateTime.Now.AddHours(-4),    TechnicianName = "David Lee" },
                new() { Id = 4, JobNumber = "JO-2024-0086", OrderNumber = "JO-2024-0086", CustomerName = "Dan Brown",      DeviceType = "Tablet",  Status = JobOrderStatus.Diagnosed,        StatusBadgeClass = "info",      CreatedAt = DateTime.Now.AddHours(-6),    TechnicianName = "Emily Chen" },
                new() { Id = 5, JobNumber = "JO-2024-0085", OrderNumber = "JO-2024-0085", CustomerName = "Eve Davis",      DeviceType = "Desktop", Status = JobOrderStatus.Completed,        StatusBadgeClass = "success",   CreatedAt = DateTime.Now.AddHours(-8),    TechnicianName = "David Lee" }
            },
            
            // Pending Job Orders
            PendingJobOrders = new List<JobOrderSummary>
            {
                new() { Id = 1, JobNumber = "JO-2024-0089", OrderNumber = "JO-2024-0089", CustomerName = "Alice Thompson", DeviceType = "Laptop", Status = JobOrderStatus.Pending, StatusBadgeClass = "warning", CreatedAt = DateTime.Now.AddMinutes(-20), TechnicianName = "Unassigned" },
                new() { Id = 3, JobNumber = "JO-2024-0087", OrderNumber = "JO-2024-0087", CustomerName = "Carol White",    DeviceType = "Phone",  Status = JobOrderStatus.AwaitingApproval, StatusBadgeClass = "warning", CreatedAt = DateTime.Now.AddHours(-4), TechnicianName = "David Lee" }
            }
        };
        
        return View(viewModel);
    }
}
