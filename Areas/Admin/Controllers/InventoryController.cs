using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class InventoryController : Controller
{
    private readonly IInventoryService _service;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public InventoryController(IInventoryService service, ApplicationDbContext db, IAuditService audit)
    {
        _service = service;
        _db = db;
        _audit = audit;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? category, bool? lowStock, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var result = await _service.GetListAsync(shopId, new PagedRequest { Page = page, PageSize = 10, Search = search }, category, lowStock);
        var categories = await _service.GetCategoriesAsync(shopId);

        var viewModel = new InventoryListViewModel
        {
            SearchTerm = search,
            CategoryFilter = category,
            LowStockOnly = lowStock,
            CurrentPage = result.Page,
            TotalCount = result.TotalCount,
            LowStockCount = result.LowStockCount,
            Categories = categories,
            Items = result.Items.Select(i => new InventoryItemViewModel
            {
                Id = i.ItemId,
                SKU = i.SKU,
                Name = i.ItemName,
                ItemName = i.ItemName,
                Category = i.CategoryName ?? "",
                Unit = i.Unit,
                UnitCost = i.UnitCost,
                UnitPrice = i.UnitPrice,
                CostPrice = i.UnitCost,
                SellingPrice = i.UnitPrice,
                QtyOnHand = i.QtyOnHand,
                QuantityInStock = i.QtyOnHand,
                ReorderLevel = i.ReorderLevel,
                IsActive = i.IsActive,
                IsLowStock = i.IsLowStock
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        return View(new InventoryFormViewModel { ExistingCategories = await _service.GetCategoriesAsync(shopId) });
    }

    [HttpGet]
    public async Task<IActionResult> CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        return PartialView("_CreateModal", new InventoryFormViewModel { ExistingCategories = await _service.GetCategoriesAsync(shopId) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        var shopId = User.GetShopId();
        await _service.CreateAsync(shopId, new CreateInventoryItemRequest
        {
            SKU = model.SKU,
            ItemName = model.Name,
            CategoryName = model.Category,
            Unit = model.Unit,
            UnitCost = model.CostPrice,
            UnitPrice = model.SellingPrice,
            QtyOnHand = model.QuantityInStock,
            ReorderLevel = model.ReorderLevel,
            IsActive = model.IsActive
        });

        TempData["Success"] = "Inventory item created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Inventory item created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var model = await GetInventoryEditModel(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var model = await GetInventoryEditModel(id);
        if (model == null) return NotFound();
        return PartialView("_EditModal", model);
    }

    private async Task<InventoryFormViewModel?> GetInventoryEditModel(long id)
    {
        var shopId = User.GetShopId();
        var detail = await _service.GetDetailAsync(shopId, id);
        if (detail == null) return null;

        return new InventoryFormViewModel
        {
            Id = detail.ItemId,
            SKU = detail.SKU,
            Name = detail.ItemName,
            Category = detail.CategoryName ?? "",
            Unit = detail.Unit,
            CostPrice = detail.UnitCost,
            SellingPrice = detail.UnitPrice,
            QuantityInStock = detail.QtyOnHand,
            ReorderLevel = detail.ReorderLevel,
            IsActive = detail.IsActive,
            ExistingCategories = await _service.GetCategoriesAsync(shopId)
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InventoryFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }

        var shopId = User.GetShopId();
        var result = await _service.UpdateAsync(shopId, model.Id, new UpdateInventoryItemRequest
        {
            SKU = model.SKU,
            ItemName = model.Name,
            CategoryName = model.Category,
            Unit = model.Unit,
            UnitCost = model.CostPrice,
            UnitPrice = model.SellingPrice,
            QtyOnHand = model.QuantityInStock,
            ReorderLevel = model.ReorderLevel,
            IsActive = model.IsActive
        });

        if (result == null) return NotFound();

        TempData["Success"] = "Inventory item updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Inventory item updated successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var model = await GetInventoryDetail(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var model = await GetInventoryDetail(id);
        if (model == null) return NotFound();
        return PartialView("_DetailsModal", model);
    }

    private async Task<InventoryDetailViewModel?> GetInventoryDetail(long id)
    {
        var shopId = User.GetShopId();
        var detail = await _service.GetDetailAsync(shopId, id);
        if (detail == null) return null;

        return new InventoryDetailViewModel
        {
            Id = detail.ItemId,
            SKU = detail.SKU,
            Name = detail.ItemName,
            Category = detail.CategoryName ?? "",
            Unit = detail.Unit,
            UnitCost = detail.UnitCost,
            UnitPrice = detail.UnitPrice,
            QtyOnHand = detail.QtyOnHand,
            ReorderLevel = detail.ReorderLevel,
            IsActive = detail.IsActive,
            RecentTransactions = detail.RecentTransactions.Select(t => new InventoryTxnItemViewModel
            {
                Id = t.Id,
                TxnType = t.TxnType,
                Quantity = t.Quantity,
                ReferenceType = t.ReferenceType,
                ReferenceId = t.ReferenceId,
                Remarks = t.Remarks,
                CreatedAt = t.CreatedAt
            }).ToList()
        };
    }

    // ─── QUICK RESTOCK ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restock(long id, int quantity, string? remarks)
    {
        if (!IsAuthorized()) return Forbid();
        if (quantity <= 0)
            return Json(new { success = false, message = "Quantity must be greater than zero." });

        var shopId = User.GetShopId();
        var result = await _service.AdjustStockAsync(shopId, id, new AdjustStockRequest
        {
            TxnType = Models.Enums.InventoryTxnType.IN,
            Quantity = quantity,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? "Quick restock" : remarks.Trim()
        });

        if (!result)
            return Json(new { success = false, message = "Item not found." });

        TempData["Success"] = "Stock restocked successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Stock restocked successfully!" });
        return RedirectToAction(nameof(Index));
    }

    // ─── ARCHIVE ────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.ShopId == shopId && i.ItemId == id);
        if (item == null) return NotFound();

        item.IsActive = false;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Archive", "InventoryItem", item.ItemId,
            $"Archived inventory item '{item.ItemName}'",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Inventory item archived!" });
        TempData["Success"] = "Item archived.";
        return RedirectToAction(nameof(Index));
    }
}
