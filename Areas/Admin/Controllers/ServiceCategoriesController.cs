using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class ServiceCategoriesController : Controller
{
    private readonly ApplicationDbContext _db;

    public ServiceCategoriesController(ApplicationDbContext db) => _db = db;

    private bool IsAuthorized() => User.GetRole() is "Admin" or "SuperAdmin";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        var categories = await _db.ServiceCategories
            .Where(c => c.ShopId == shopId && !c.IsArchived)
            .OrderBy(c => c.CategoryName)
            .Select(c => new
            {
                c.ServiceCategoryId,
                c.CategoryName,
                c.Description,
                ServiceCount = c.Services.Count(s => s.IsActive)
            })
            .ToListAsync();

        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string categoryName, string? description)
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();

        if (string.IsNullOrWhiteSpace(categoryName))
            return Json(new { success = false, message = "Category name is required." });

        var exists = await _db.ServiceCategories
            .AnyAsync(c => c.ShopId == shopId && c.CategoryName == categoryName.Trim());
        if (exists)
            return Json(new { success = false, message = "A category with this name already exists." });

        _db.ServiceCategories.Add(new ServiceCategory
        {
            ShopId = shopId,
            CategoryName = categoryName.Trim(),
            Description = description?.Trim()
        });
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Category created." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, string categoryName, string? description)
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();

        if (string.IsNullOrWhiteSpace(categoryName))
            return Json(new { success = false, message = "Category name is required." });

        var cat = await _db.ServiceCategories.FirstOrDefaultAsync(c => c.ShopId == shopId && c.ServiceCategoryId == id);
        if (cat == null) return NotFound();

        var duplicate = await _db.ServiceCategories
            .AnyAsync(c => c.ShopId == shopId && c.CategoryName == categoryName.Trim() && c.ServiceCategoryId != id);
        if (duplicate)
            return Json(new { success = false, message = "A category with this name already exists." });

        cat.CategoryName = categoryName.Trim();
        cat.Description = description?.Trim();
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Category updated." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();

        var cat = await _db.ServiceCategories
            .Include(c => c.Services)
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.ServiceCategoryId == id);
        if (cat == null) return NotFound();

        if (cat.Services.Any(s => s.IsActive))
            return Json(new { success = false, message = $"Cannot archive — {cat.Services.Count(s => s.IsActive)} active service(s) use this category. Reassign them first." });

        cat.IsArchived = true;
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Category archived." });
    }
}
