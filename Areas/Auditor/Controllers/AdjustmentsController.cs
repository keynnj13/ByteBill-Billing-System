using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Auditor.Controllers;

[Area("Auditor")]
[Authorize]
public class AdjustmentsController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Auditor.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? type, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var adjustments = new[]
        {
            new { Id = 1, Type = "Discount", InvoiceNumber = "INV-2024-0140", CustomerName = "Alice Thompson", Amount = -50.00m, Reason = "Loyal customer discount", CreatedAt = DateTime.Now.AddDays(-1), CreatedBy = "Emily Brown", ApprovedBy = "John Anderson" },
            new { Id = 2, Type = "Refund", InvoiceNumber = "INV-2024-0135", CustomerName = "Bob Martinez", Amount = -25.00m, Reason = "Parts return", CreatedAt = DateTime.Now.AddDays(-3), CreatedBy = "Emily Brown", ApprovedBy = "John Anderson" },
            new { Id = 3, Type = "Credit", InvoiceNumber = "INV-2024-0128", CustomerName = "Carol White", Amount = -15.00m, Reason = "Service credit for inconvenience", CreatedAt = DateTime.Now.AddDays(-5), CreatedBy = "Emily Brown", ApprovedBy = "John Anderson" },
            new { Id = 4, Type = "Write-off", InvoiceNumber = "INV-2024-0098", CustomerName = "David Wilson", Amount = -150.00m, Reason = "Uncollectible debt", CreatedAt = DateTime.Now.AddDays(-15), CreatedBy = "John Anderson", ApprovedBy = "Super Admin" }
        };
        
        ViewBag.Adjustments = adjustments;
        ViewBag.TypeFilter = type;
        ViewBag.TotalDiscounts = 50.00m;
        ViewBag.TotalRefunds = 25.00m;
        ViewBag.TotalCredits = 15.00m;
        ViewBag.TotalWriteoffs = 150.00m;
        
        return View();
    }
}
