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

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
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

    private bool IsAuthorized() => User.IsInRoles("Billing", "Admin", "SuperAdmin");

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : (parts.Length == 1 ? parts[0][..1].ToUpper() : "??");
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var result = await _customerService.GetListAsync(shopId, new PagedRequest
        {
            Page = page,
            PageSize = 10,
            Search = search
        });

        var viewModel = new CustomerListViewModel
        {
            SearchTerm = search,
            CurrentPage = result.Page,
            TotalCount = result.TotalCount,
            Customers = result.Items.Select(c => new CustomerItemViewModel
            {
                Id = c.CustomerId,
                FullName = c.FullName,
                Name = c.FullName,
                Initials = GetInitials(c.FullName),
                Email = c.Email ?? "",
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
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var customer = await _customerService.GetByIdAsync(shopId, id);
        if (customer == null) return NotFound();

        var outstandingBalance = await _db.Invoices
            .Where(i => i.CustomerId == id && i.ShopId == shopId && i.Balance > 0 &&
                        i.Status != InvoiceStatus.Void)
            .SumAsync(i => (decimal?)i.Balance) ?? 0;

        var recentOrders = await _db.JobOrders
            .Where(j => j.CustomerId == id && j.ShopId == shopId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(5)
            .Select(j => new OrderHistoryItem
            {
                Id = j.JobOrderId,
                JobNumber = j.JobOrderNo,
                OrderNumber = j.JobOrderNo,
                DeviceType = j.Device != null ? j.Device.DeviceType : "",
                Status = j.Status.ToString(),
                Date = j.CreatedAt,
                CreatedAt = j.CreatedAt
            })
            .ToListAsync();

        var model = new CustomerDetailViewModel
        {
            Id = customer.CustomerId,
            FullName = $"{customer.FirstName} {customer.LastName}",
            Name = $"{customer.FirstName} {customer.LastName}",
            Email = customer.Email ?? "",
            Phone = customer.Phone ?? "",
            Address = customer.Address ?? "",
            CreatedAt = customer.CreatedAt,
            TotalOrders = await _db.JobOrders.CountAsync(j => j.CustomerId == id && j.ShopId == shopId),
            TotalSpent = await _db.Payments
                .Where(p => p.CustomerId == id && p.ShopId == shopId &&
                            p.Status == PaymentStatus.Confirmed)
                .SumAsync(p => (decimal?)p.Amount) ?? 0,
            OutstandingBalance = outstandingBalance,
            RecentOrders = recentOrders
        };

        return View(model);
    }
}
