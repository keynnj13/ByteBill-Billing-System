using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Technician.Controllers;

[Area("Technician")]
[Authorize]
public class PartsUsageController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Technician.ToString();
    }

    [HttpGet]
    public IActionResult Index(long? jobOrderId)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var usageHistory = new[]
        {
            new { Id = 1, JobOrderNumber = "JO-2024-0156", PartName = "Display Cable (MacBook Pro 2021)", Quantity = 1, UsedAt = DateTime.Now.AddHours(-6) },
            new { Id = 2, JobOrderNumber = "JO-2024-0154", PartName = "iPhone 14 Pro Screen Assembly", Quantity = 1, UsedAt = DateTime.Now.AddHours(-3) },
            new { Id = 3, JobOrderNumber = "JO-2024-0150", PartName = "Samsung 870 EVO 500GB SSD", Quantity = 1, UsedAt = DateTime.Now.AddDays(-1) },
            new { Id = 4, JobOrderNumber = "JO-2024-0150", PartName = "Noctua NT-H1 Thermal Paste", Quantity = 1, UsedAt = DateTime.Now.AddDays(-1) }
        };

        var availableParts = new[]
        {
            new { Id = 1, SKU = "SSD-500-SAM", Name = "Samsung 870 EVO 500GB SSD", InStock = 12 },
            new { Id = 2, SKU = "RAM-16-COR", Name = "Corsair Vengeance 16GB DDR4", InStock = 8 },
            new { Id = 3, SKU = "HDD-1TB-WD", Name = "WD Blue 1TB HDD", InStock = 3 },
            new { Id = 4, SKU = "CBL-HDMI-2M", Name = "HDMI Cable 2m", InStock = 25 },
            new { Id = 5, SKU = "PST-THRM-NT", Name = "Noctua NT-H1 Thermal Paste", InStock = 2 }
        };
        
        ViewBag.UsageHistory = usageHistory;
        ViewBag.AvailableParts = availableParts;
        ViewBag.JobOrderId = jobOrderId;
        
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RecordUsage(long jobOrderId, long partId, int quantity)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        TempData["Success"] = "Parts usage recorded successfully";
        return RedirectToAction(nameof(Index));
    }
}
