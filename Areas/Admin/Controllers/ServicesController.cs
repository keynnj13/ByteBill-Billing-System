using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class ServicesController : Controller
{
    private readonly IServiceCatalogService _service;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public ServicesController(IServiceCatalogService service, ApplicationDbContext db, IAuditService audit)
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
    public async Task<IActionResult> Index(string? search, string? category, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var result = await _service.GetListAsync(shopId, new PagedRequest { Page = page, PageSize = 10, Search = search }, category);
        var categories = await _service.GetCategoriesAsync(shopId);

        var viewModel = new ServiceListViewModel
        {
            SearchTerm = search,
            CategoryFilter = category,
            CurrentPage = result.Page,
            TotalCount = result.TotalCount,
            Categories = categories,
            Services = result.Items.Select(s => new ServiceItemViewModel
            {
                Id = s.ServiceId,
                Name = s.ServiceName,
                ServiceName = s.ServiceName,
                Category = s.CategoryName,
                Description = s.Description,
                Price = s.BasePrice,
                BasePrice = s.BasePrice,
                EstimatedDuration = s.EstimatedDuration > 0 ? $"{s.EstimatedDuration} min" : null,
                IsActive = s.IsActive,
                UsageCount = s.UsageCount
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        return View(new ServiceFormViewModel
        {
            ExistingCategories = await _service.GetCategoriesAsync(shopId)
        });
    }

    [HttpGet]
    public async Task<IActionResult> CreateModal()
    {
        if (!IsAuthorized()) return Forbid();

        var shopId = User.GetShopId();
        return PartialView("_CreateModal", new ServiceFormViewModel
        {
            ExistingCategories = await _service.GetCategoriesAsync(shopId)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            var shopId2 = User.GetShopId();
            model.ExistingCategories = await _service.GetCategoriesAsync(shopId2);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        var shopId = User.GetShopId();
        await _service.CreateAsync(shopId, new CreateServiceRequest
        {
            ServiceName = model.Name,
            CategoryName = model.Category,
            CategoryId = model.CategoryId,
            Description = model.Description,
            BasePrice = model.Price,
            EstimatedDuration = model.EstimatedDuration,
            IsActive = model.IsActive
        });

        TempData["Success"] = "Service created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Service created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var model = await GetServiceEditModel(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var model = await GetServiceEditModel(id);
        if (model == null) return NotFound();
        return PartialView("_EditModal", model);
    }

    private async Task<ServiceFormViewModel?> GetServiceEditModel(long id)
    {
        var shopId = User.GetShopId();
        var detail = await _service.GetDetailAsync(shopId, id);
        if (detail == null) return null;

        return new ServiceFormViewModel
        {
            Id = detail.ServiceId,
            Name = detail.ServiceName,
            Category = detail.CategoryName,
            CategoryId = detail.ServiceCategoryId,
            Description = detail.Description,
            Price = detail.BasePrice,
            EstimatedDuration = detail.EstimatedDuration,
            IsActive = detail.IsActive,
            ExistingCategories = await _service.GetCategoriesAsync(shopId)
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ServiceFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            var shopId2 = User.GetShopId();
            model.ExistingCategories = await _service.GetCategoriesAsync(shopId2);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }

        var shopId = User.GetShopId();
        var result = await _service.UpdateAsync(shopId, model.Id, new UpdateServiceRequest
        {
            ServiceName = model.Name,
            CategoryName = model.Category,
            CategoryId = model.CategoryId,
            Description = model.Description,
            BasePrice = model.Price,
            EstimatedDuration = model.EstimatedDuration,
            IsActive = model.IsActive
        });

        if (result == null) return NotFound();

        TempData["Success"] = "Service updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Service updated successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var model = await GetServiceDetail(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var model = await GetServiceDetail(id);
        if (model == null) return NotFound();
        return PartialView("_DetailsModal", model);
    }

    private async Task<ServiceDetailViewModel?> GetServiceDetail(long id)
    {
        var shopId = User.GetShopId();
        var detail = await _service.GetDetailAsync(shopId, id);
        if (detail == null) return null;

        return new ServiceDetailViewModel
        {
            Id = detail.ServiceId,
            Name = detail.ServiceName,
            Category = detail.CategoryName,
            Description = detail.Description,
            Price = detail.BasePrice,
            IsActive = detail.IsActive,
            UsageCount = detail.UsageCount,
            TotalRevenue = detail.TotalRevenue
        };
    }

    // ─── ARCHIVE ────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var shopId = User.GetShopId();
        var svc = await _db.ServiceCatalogs.FirstOrDefaultAsync(s => s.ShopId == shopId && s.ServiceId == id);
        if (svc == null) return NotFound();

        svc.IsActive = false;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Archive", "Service", svc.ServiceId,
            $"Archived service '{svc.ServiceName}'",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Service archived successfully!" });
        TempData["Success"] = "Service archived.";
        return RedirectToAction(nameof(Index));
    }
}
