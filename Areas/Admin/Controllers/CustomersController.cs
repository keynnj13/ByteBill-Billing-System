using ByteBill_BS.Data;
using ByteBill_BS.DTOs.Common;
using ByteBill_BS.DTOs.Customers;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class CustomersController : Controller
{
    private readonly ICustomerService _customerService;
    private readonly ApplicationDbContext _db;

    public CustomersController(ICustomerService customerService, ApplicationDbContext db)
    {
        _customerService = customerService;
        _db = db;
    }

    private bool IsAuthorized() => User.IsInRoles("Admin", "SuperAdmin");

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var result = await _customerService.GetListAsync(shopId, new PagedRequest { Search = search, Page = page, PageSize = 10 });

        var viewModel = new CustomerListViewModel
        {
            SearchTerm = search,
            CurrentPage = page,
            TotalCount = result.TotalCount,
            Customers = result.Items.Select(c => new CustomerItemViewModel
            {
                Id = c.CustomerId,
                FullName = c.FullName,
                Name = c.FullName,
                Initials = c.Initials,
                Email = c.Email,
                Phone = c.Phone ?? "",
                TotalOrders = c.Orders,
                TotalJobOrders = c.Orders,
                TotalSpent = c.TotalSpent,
                CreatedAt = c.CreatedAt,
                IsActive = c.IsActive
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        return View(new CustomerFormViewModel());
    }

    [HttpGet]
    public IActionResult CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_CreateModal", new CustomerFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        try
        {
            var shopId = User.GetShopId();
            var userId = User.GetUserId();

            await _customerService.CreateAsync(shopId, userId, new CreateCustomerRequest
            {
                FirstName = model.FirstName,
                MiddleName = model.MiddleName,
                LastName = model.LastName,
                Email = model.Email,
                Phone = model.Phone,
                Address = model.Address
            });

            TempData["Success"] = "Customer created successfully!";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = "Customer created successfully!" });
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var customer = await _customerService.GetByIdAsync(shopId, id);
        if (customer == null) return NotFound();

        return View(MapToForm(customer));
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var shopId = User.GetShopId();
        var customer = await _customerService.GetByIdAsync(shopId, id);
        if (customer == null) return NotFound();

        return PartialView("_EditModal", MapToForm(customer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }

        try
        {
            var shopId = User.GetShopId();
            var userId = User.GetUserId();

            var result = await _customerService.UpdateAsync(shopId, userId, model.Id, new UpdateCustomerRequest
            {
                FirstName = model.FirstName,
                MiddleName = model.MiddleName,
                LastName = model.LastName,
                Email = model.Email,
                Phone = model.Phone,
                Address = model.Address
            });

            if (result == null) return NotFound();

            TempData["Success"] = "Customer updated successfully!";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = "Customer updated successfully!" });
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var model = await GetCustomerDetailAsync(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var model = await GetCustomerDetailAsync(id);
        if (model == null) return NotFound();
        return PartialView("_DetailsModal", model);
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private static CustomerFormViewModel MapToForm(CustomerDetailDto c) => new()
    {
        Id = c.CustomerId,
        FirstName = c.FirstName,
        MiddleName = c.MiddleName,
        LastName = c.LastName,
        Email = c.Email,
        Phone = c.Phone,
        Address = c.Address,
        IsActive = c.IsActive
    };

    private async Task<CustomerDetailViewModel?> GetCustomerDetailAsync(long id)
    {
        var shopId = User.GetShopId();
        var customer = await _customerService.GetByIdAsync(shopId, id);
        if (customer == null) return null;

        var outstandingBalance = await _db.Invoices
            .Where(i => i.ShopId == shopId && i.CustomerId == id && i.Balance > 0 && i.Status != InvoiceStatus.Void)
            .SumAsync(i => (decimal?)i.Balance) ?? 0;

        var recentJobOrders = await _db.JobOrders
            .Where(j => j.ShopId == shopId && j.CustomerId == id)
            .OrderByDescending(j => j.CreatedAt)
            .Take(5)
            .Select(j => new CustomerJobOrderViewModel
            {
                Id = j.JobOrderId,
                JobNumber = j.JobOrderNo,
                DeviceType = j.Device!.DeviceType + " " + j.Device.Brand + " " + j.Device.Model,
                Status = j.Status.ToString(),
                StatusClass = "badge-" + j.Status.ToString().ToLower(),
                Total = j.JobOrderServices.Sum(s => s.LineTotal) + j.JobOrderParts.Sum(p => p.LineTotal),
                CreatedAt = j.CreatedAt
            })
            .AsNoTracking()
            .ToListAsync();

        return new CustomerDetailViewModel
        {
            Id = customer.CustomerId,
            FullName = customer.FullName,
            Name = customer.FullName,
            Initials = customer.Initials,
            Email = customer.Email,
            Phone = customer.Phone ?? "",
            Address = customer.Address,
            IsActive = customer.IsActive,
            CreatedAt = customer.CreatedAt,
            TotalOrders = customer.Orders,
            TotalJobOrders = customer.Orders,
            TotalSpent = customer.TotalSpent,
            OutstandingBalance = outstandingBalance,
            RecentJobOrders = recentJobOrders
        };
    }
}
