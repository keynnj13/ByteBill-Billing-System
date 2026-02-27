using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Technician.Controllers;

[Area("Technician")]
[Authorize]
public class PartsUsageController : Controller
{
    private readonly ApplicationDbContext _db;

    public PartsUsageController(ApplicationDbContext db)
    {
        _db = db;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Technician.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        // Get parts usage history for this technician's job orders
        var usageHistory = await _db.JobOrderParts
            .Where(p => p.JobOrder!.ShopId == shopId && p.JobOrder.AssignedTechUserId == userId)
            .OrderByDescending(p => p.JobOrder!.UpdatedAt ?? p.JobOrder.CreatedAt)
            .Take(50)
            .Select(p => new
            {
                p.JobOrderPartId,
                JobOrderNumber = p.JobOrder!.JobOrderNo,
                JobOrderId = p.JobOrderId,
                PartName = p.Item!.ItemName,
                SKU = p.Item.SKU,
                Quantity = p.QtyUsed,
                UnitPrice = p.UnitPrice,
                LineTotal = p.QtyUsed * p.UnitPrice,
                UsedAt = p.JobOrder.UpdatedAt ?? p.JobOrder.CreatedAt
            })
            .ToListAsync();

        // Get available parts (all inventory — show out-of-stock too for visibility)
        var availableParts = await _db.InventoryItems
            .Where(i => i.ShopId == shopId && i.IsActive)
            .OrderBy(i => i.ItemName)
            .Select(i => new
            {
                i.ItemId,
                i.SKU,
                i.ItemName,
                i.QtyOnHand,
                i.UnitPrice,
                i.IsLowStock
            })
            .ToListAsync();

        // Compute stats
        var totalPartsUsed = usageHistory.Sum(u => u.Quantity);
        var totalValue = usageHistory.Sum(u => u.LineTotal);
        var lowStockCount = availableParts.Count(p => p.IsLowStock || p.QtyOnHand <= 0);

        ViewBag.UsageHistory = usageHistory;
        ViewBag.AvailableParts = availableParts;
        ViewBag.TotalPartsUsed = totalPartsUsed;
        ViewBag.TotalValue = totalValue;
        ViewBag.LowStockCount = lowStockCount;

        return View();
    }
}
