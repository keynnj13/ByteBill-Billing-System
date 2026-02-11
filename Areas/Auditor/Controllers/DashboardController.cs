using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Auditor.Controllers;

[Area("Auditor")]
[Authorize]
public class DashboardController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Auditor.ToString();
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
                new() { Icon = "file-text", IconColor = "info", Title = "Invoice voided", Description = "INV-2024-0138 voided by John Anderson", TimeAgo = "1 hour ago" },
                new() { Icon = "credit-card", IconColor = "warning", Title = "Refund processed", Description = "$50 refund for INV-2024-0135", TimeAgo = "2 hours ago" },
                new() { Icon = "edit", IconColor = "primary", Title = "Invoice adjusted", Description = "Discount applied to INV-2024-0140", TimeAgo = "3 hours ago" }
            },
            PendingJobOrders = new List<JobOrderSummary>()
        };
        
        ViewBag.TotalRevenueMTD = 45820.00m;
        ViewBag.TotalRefundsMTD = 125.00m;
        ViewBag.TotalAdjustmentsMTD = 280.00m;
        ViewBag.VoidedInvoicesMTD = 3;
        
        return View(viewModel);
    }
}
