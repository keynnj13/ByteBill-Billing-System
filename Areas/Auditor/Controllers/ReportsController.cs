using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Auditor.Controllers;

[Area("Auditor")]
[Authorize]
public class ReportsController : Controller
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

        var reportTypes = new[]
        {
            new { Id = "revenue", Name = "Revenue Report", Description = "Daily, weekly, and monthly revenue breakdown", Icon = "dollar-sign" },
            new { Id = "payments", Name = "Payments Report", Description = "Payment methods distribution and trends", Icon = "credit-card" },
            new { Id = "outstanding", Name = "Outstanding Balances", Description = "Aging report of unpaid invoices", Icon = "clock" },
            new { Id = "adjustments", Name = "Adjustments Summary", Description = "All discounts, refunds, and write-offs", Icon = "edit" },
            new { Id = "voids", Name = "Voided Transactions", Description = "Voided invoices and payments", Icon = "x-circle" },
            new { Id = "technician", Name = "Technician Performance", Description = "Job completion rates and revenue by technician", Icon = "user" }
        };
        
        ViewBag.ReportTypes = reportTypes;
        return View();
    }

    [HttpGet]
    public IActionResult Revenue(DateTime? startDate, DateTime? endDate)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        startDate ??= DateTime.Now.AddDays(-30);
        endDate ??= DateTime.Now;
        
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        ViewBag.TotalRevenue = 45820.00m;
        ViewBag.TotalTransactions = 142;
        ViewBag.AverageTransaction = 322.67m;
        
        var dailyRevenue = new[]
        {
            new { Date = DateTime.Now.AddDays(-6), Amount = 1250.00m },
            new { Date = DateTime.Now.AddDays(-5), Amount = 980.00m },
            new { Date = DateTime.Now.AddDays(-4), Amount = 1540.00m },
            new { Date = DateTime.Now.AddDays(-3), Amount = 890.00m },
            new { Date = DateTime.Now.AddDays(-2), Amount = 1680.00m },
            new { Date = DateTime.Now.AddDays(-1), Amount = 2100.00m },
            new { Date = DateTime.Now, Amount = 2850.00m }
        };
        
        ViewBag.DailyRevenue = dailyRevenue;
        return View();
    }

    [HttpGet]
    public IActionResult Outstanding()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var agingData = new[]
        {
            new { Period = "Current (0-30 days)", Count = 8, Amount = 2450.00m },
            new { Period = "31-60 days", Count = 4, Amount = 1280.00m },
            new { Period = "61-90 days", Count = 2, Amount = 680.00m },
            new { Period = "Over 90 days", Count = 1, Amount = 320.00m }
        };
        
        ViewBag.AgingData = agingData;
        ViewBag.TotalOutstanding = 4730.00m;
        return View();
    }
}
