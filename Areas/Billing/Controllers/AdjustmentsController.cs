using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class AdjustmentsController : Controller
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

        var adjustments = new[]
        {
            new { Id = 1, Type = "Discount", InvoiceNumber = "INV-2024-0140", CustomerName = "Alice Thompson", Amount = -50.00m, Reason = "Loyal customer discount", CreatedAt = DateTime.Now.AddDays(-1), ApprovedBy = "John Anderson" },
            new { Id = 2, Type = "Refund", InvoiceNumber = "INV-2024-0135", CustomerName = "Bob Martinez", Amount = -25.00m, Reason = "Parts return", CreatedAt = DateTime.Now.AddDays(-3), ApprovedBy = "John Anderson" },
            new { Id = 3, Type = "Credit", InvoiceNumber = "INV-2024-0128", CustomerName = "Carol White", Amount = -15.00m, Reason = "Service credit for inconvenience", CreatedAt = DateTime.Now.AddDays(-5), ApprovedBy = "John Anderson" }
        };
        
        ViewBag.Adjustments = adjustments;
        return View();
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        return View();
    }
}
