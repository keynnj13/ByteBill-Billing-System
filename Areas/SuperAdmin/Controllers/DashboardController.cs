using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class DashboardController : Controller
{
    private readonly ISuperAdminService _service;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ISuperAdminService service, ILogger<DashboardController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.SuperAdmin.ToString();
    }

    private string GetFirstName()
    {
        var fullName = User.Claims.FirstOrDefault(c => c.Type == "FullName")?.Value ?? "Admin";
        return fullName.Split(' ')[0];
    }

    [HttpGet]
    public async Task<IActionResult> Index(string period = "6months")
    {
        if (!IsAuthorized())
            return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        try
        {
            var data = await _service.GetDashboardDataAsync(period);

            var viewModel = new DashboardViewModel
            {
                UserRole = UserRole.SuperAdmin,
                UserName = GetFirstName(),
                ShopName = "System Administration",
                MonthRevenue = data.MonthRevenue,
                InProgressJobOrders = data.ActiveSubscriptions,
                RevenueChart = data.RevenueChart,
                JobOrderChart = data.ShopsGrowthChart,
                RecentActivity = data.RecentActivity,
                PendingJobOrders = new List<JobOrderSummary>()
            };

            ViewBag.TotalShops = data.TotalShops;
            ViewBag.ActiveShops = data.ActiveShops;
            ViewBag.NewShopsThisMonth = data.NewShopsThisMonth;
            ViewBag.TotalUsers = data.TotalUsers;
            ViewBag.SystemRevenue = data.MonthRevenue;
            ViewBag.PreviousMonthRevenue = data.PreviousMonthRevenue;
            ViewBag.ActiveSubscriptions = data.ActiveSubscriptions;
            ViewBag.ExpiringSubscriptions = data.ExpiringSubscriptions;
            ViewBag.OverduePayments = data.OverduePayments;
            ViewBag.SubscriptionDistribution = data.SubscriptionDistribution;
            ViewBag.Period = period;

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SuperAdmin Dashboard failed to load. Period={Period}", period);
            throw; // Re-throw so the developer exception page shows the full stack trace
        }
    }
}
