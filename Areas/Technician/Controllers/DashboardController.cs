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
            RecentActivity = new List<RecentActivityItem>(),
            PendingJobOrders = new List<JobOrderSummary>()
            // TODO: Connect to API - GET /api/jobordersapi for technician's assigned jobs
        };
        
        ViewBag.MyJobsToday = 0;
        ViewBag.CompletedToday = 0;
        ViewBag.InProgress = 0;
        ViewBag.WaitingParts = 0;
        
        return View(viewModel);
    }
}
