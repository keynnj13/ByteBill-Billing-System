using ByteBill_BS.Data;
using ByteBill_BS.DTOs.JobOrders;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Technician.Controllers;

[Area("Technician")]
[Authorize]
public class PartsUsageController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IJobOrderService _jobOrderService;

    public PartsUsageController(ApplicationDbContext db, IJobOrderService jobOrderService)
    {
        _db = db;
        _jobOrderService = jobOrderService;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Technician.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index(long? jobOrderId)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        // Get parts usage history for this technician's job orders
        var usageHistory = await _db.JobOrderParts
            .Where(p => p.JobOrder!.ShopId == shopId && p.JobOrder.AssignedTechUserId == userId)
            .OrderByDescending(p => p.JobOrder!.UpdatedAt ?? p.JobOrder.CreatedAt)
            .Take(20)
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

        // Get available parts (in-stock inventory)
        var availableParts = await _db.InventoryItems
            .Where(i => i.ShopId == shopId && i.IsActive && i.QtyOnHand > 0)
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

        // Get active job orders for the technician (for the "Record Usage" dropdown)
        var activeJobOrders = await _db.JobOrders
            .Where(j => j.ShopId == shopId
                && j.AssignedTechUserId == userId
                && !j.IsArchived
                && j.Status != JobOrderStatus.Completed
                && j.Status != JobOrderStatus.Cancelled)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new
            {
                j.JobOrderId,
                j.JobOrderNo,
                CustomerName = j.Customer!.FirstName + " " + j.Customer.LastName,
                DeviceSummary = j.Device!.DeviceType + " - " + j.Device.Brand + " " + j.Device.Model
            })
            .ToListAsync();

        ViewBag.UsageHistory = usageHistory;
        ViewBag.AvailableParts = availableParts;
        ViewBag.ActiveJobOrders = activeJobOrders;
        ViewBag.JobOrderId = jobOrderId;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordUsage(long jobOrderId, long partId, int quantity)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        // Verify technician owns this job order
        var jobOrder = await _db.JobOrders
            .FirstOrDefaultAsync(j => j.ShopId == shopId
                && j.JobOrderId == jobOrderId
                && j.AssignedTechUserId == userId);

        if (jobOrder == null)
        {
            TempData["Error"] = "Job order not found or not assigned to you.";
            return RedirectToAction(nameof(Index));
        }

        // Get the inventory item price
        var item = await _db.InventoryItems
            .FirstOrDefaultAsync(i => i.ItemId == partId && i.ShopId == shopId);

        if (item == null)
        {
            TempData["Error"] = "Part not found.";
            return RedirectToAction(nameof(Index));
        }

        var dto = new AddPartLineDto
        {
            ItemId = partId,
            QtyUsed = quantity,
            UnitPrice = item.UnitPrice
        };

        var result = await _jobOrderService.AddPartLineAsync(shopId, userId, jobOrderId, dto);

        if (!result.Success)
        {
            TempData["Error"] = result.Message ?? "Failed to record parts usage.";
        }
        else
        {
            TempData["Success"] = $"Added {quantity}x {item.ItemName} to {jobOrder.JobOrderNo}";
        }

        return RedirectToAction(nameof(Index));
    }
}
