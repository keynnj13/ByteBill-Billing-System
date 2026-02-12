using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class InventoryController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, string? category, bool? lowStock, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = new InventoryListViewModel
        {
            SearchTerm = search,
            CategoryFilter = category,
            LowStockOnly = lowStock,
            CurrentPage = page,
            TotalCount = 68,
            LowStockCount = 5,
            Categories = new List<string> { "Storage", "Memory", "Cables", "Peripherals", "Components", "Tools" },
            Items = new List<InventoryItemViewModel>
            {
                new() { Id = 1, SKU = "SSD-500-SAM", Name = "Samsung 870 EVO 500GB SSD", Category = "Storage", Brand = "Samsung", QuantityInStock = 12, ReorderLevel = 5, CostPrice = 45.00m, SellingPrice = 65.00m, IsLowStock = false, IsActive = true },
                new() { Id = 2, SKU = "RAM-16-COR", Name = "Corsair Vengeance 16GB DDR4", Category = "Memory", Brand = "Corsair", QuantityInStock = 8, ReorderLevel = 5, CostPrice = 55.00m, SellingPrice = 89.00m, IsLowStock = false, IsActive = true },
                new() { Id = 3, SKU = "HDD-1TB-WD", Name = "WD Blue 1TB HDD", Category = "Storage", Brand = "Western Digital", QuantityInStock = 3, ReorderLevel = 5, CostPrice = 35.00m, SellingPrice = 55.00m, IsLowStock = true, IsActive = true },
                new() { Id = 4, SKU = "CBL-HDMI-2M", Name = "HDMI Cable 2m", Category = "Cables", Brand = "Generic", QuantityInStock = 25, ReorderLevel = 10, CostPrice = 3.50m, SellingPrice = 12.00m, IsLowStock = false, IsActive = true },
                new() { Id = 5, SKU = "PST-THRM-NT", Name = "Noctua NT-H1 Thermal Paste", Category = "Components", Brand = "Noctua", QuantityInStock = 2, ReorderLevel = 5, CostPrice = 8.00m, SellingPrice = 15.00m, IsLowStock = true, IsActive = true },
                new() { Id = 6, SKU = "KBD-LOGI-K120", Name = "Logitech K120 Keyboard", Category = "Peripherals", Brand = "Logitech", QuantityInStock = 6, ReorderLevel = 5, CostPrice = 12.00m, SellingPrice = 25.00m, IsLowStock = false, IsActive = true }
            }
        };
        
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        return View(new InventoryFormViewModel
        {
            ExistingCategories = new List<string> { "Storage", "Memory", "Cables", "Peripherals", "Components", "Tools" },
            ExistingBrands = new List<string> { "Samsung", "Corsair", "Western Digital", "Logitech", "Noctua", "Generic" }
        });
    }

    [HttpGet]
    public IActionResult CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        
        return PartialView("_CreateModal", new InventoryFormViewModel
        {
            ExistingCategories = new List<string> { "Storage", "Memory", "Cables", "Peripherals", "Components", "Tools" },
            ExistingBrands = new List<string> { "Samsung", "Corsair", "Western Digital", "Logitech", "Noctua", "Generic" }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(InventoryFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid)
        {
            model.ExistingCategories = new List<string> { "Storage", "Memory", "Cables", "Peripherals", "Components", "Tools" };
            model.ExistingBrands = new List<string> { "Samsung", "Corsair", "Western Digital", "Logitech", "Noctua", "Generic" };
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }
        
        TempData["Success"] = "Inventory item created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Inventory item created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = GetInventoryEditModel(id);
        return View(model);
    }

    [HttpGet]
    public IActionResult EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = GetInventoryEditModel(id);
        return PartialView("_EditModal", model);
    }

    private InventoryFormViewModel GetInventoryEditModel(long id)
    {
        return new InventoryFormViewModel
        {
            Id = id,
            SKU = "SSD-500-SAM",
            Name = "Samsung 870 EVO 500GB SSD",
            Description = "High-performance SATA SSD for system upgrades",
            Category = "Storage",
            Brand = "Samsung",
            QuantityInStock = 12,
            ReorderLevel = 5,
            CostPrice = 45.00m,
            SellingPrice = 65.00m,
            IsActive = true,
            ExistingCategories = new List<string> { "Storage", "Memory", "Cables", "Peripherals", "Components", "Tools" },
            ExistingBrands = new List<string> { "Samsung", "Corsair", "Western Digital", "Logitech", "Noctua", "Generic" }
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(InventoryFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid)
        {
            model.ExistingCategories = new List<string> { "Storage", "Memory", "Cables", "Peripherals", "Components", "Tools" };
            model.ExistingBrands = new List<string> { "Samsung", "Corsair", "Western Digital", "Logitech", "Noctua", "Generic" };
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }
        
        TempData["Success"] = "Inventory item updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Inventory item updated successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = GetInventoryDetail(id);
        return View(model);
    }

    [HttpGet]
    public IActionResult DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = GetInventoryDetail(id);
        return PartialView("_DetailsModal", model);
    }

    private InventoryDetailViewModel GetInventoryDetail(long id)
    {
        return new InventoryDetailViewModel
        {
            Id = id,
            SKU = "SSD-500-SAM",
            Name = "Samsung 870 EVO 500GB SSD",
            Category = "Storage",
            Brand = "Samsung",
            Unit = "pcs",
            UnitCost = 45.00m,
            UnitPrice = 65.00m,
            QtyOnHand = 12,
            ReorderLevel = 5,
            IsActive = true,
            RecentTransactions = new List<InventoryTxnItemViewModel>
            {
                new() { Id = 1, TxnType = "In", Quantity = 10, Remarks = "Purchase Order #PO-2024-015", CreatedAt = DateTime.Now.AddDays(-14) },
                new() { Id = 2, TxnType = "Out", Quantity = 2, Remarks = "Job Order JO-2024-0079", CreatedAt = DateTime.Now.AddDays(-7) },
                new() { Id = 3, TxnType = "In", Quantity = 5, Remarks = "Purchase Order #PO-2024-018", CreatedAt = DateTime.Now.AddDays(-3) },
                new() { Id = 4, TxnType = "Out", Quantity = 1, Remarks = "Job Order JO-2024-0085", CreatedAt = DateTime.Now.AddDays(-1) }
            }
        };
    }
}
