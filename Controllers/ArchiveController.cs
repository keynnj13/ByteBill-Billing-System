using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Admin;
using ByteBill_BS.ViewModels.Invoices;
using ByteBill_BS.ViewModels.JobOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Controllers;

[Authorize]
public class ArchiveController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IEmailSecurityService _emailSecurity;

    public ArchiveController(ApplicationDbContext db, IAuditService audit, IEmailSecurityService emailSecurity)
    {
        _db = db;
        _audit = audit;
        _emailSecurity = emailSecurity;
    }

    private bool IsAuthorized() => User.IsInRoles("Admin", "Billing", "SuperAdmin");

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : (parts.Length == 1 ? parts[0][..1].ToUpper() : "??");
    }

    [HttpGet("/Archive")]
    [HttpGet("/Archive/Index")]
    public async Task<IActionResult> Index(
        string tab = "joborders",
        string? search = null,
        JobOrderStatus? joStatus = null,
        InvoiceStatus? invStatus = null,
        int joPage = 1,
        int invPage = 1,
        int usrPage = 1,
        int custPage = 1,
        int svcPage = 1,
        int invtPage = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth");
        if (!ModelState.IsValid) return BadRequest();

        var shopId = User.GetShopId();
        const int pageSize = 10;

        var isJobOrdersTab = tab == "joborders";
        var isInvoicesTab = tab == "invoices";
        var isUsersTab = tab == "users";
        var isCustomersTab = tab == "customers";
        var isServicesTab = tab == "services";
        var isInventoryTab = tab == "inventory";
        ViewBag.ActiveTab = tab;
        ViewBag.Search = search;
        ViewBag.JoStatusFilter = joStatus;
        ViewBag.InvStatusFilter = invStatus;

        ViewBag.JobOrders = await BuildArchivedJobOrdersAsync(shopId, search, joStatus, joPage, pageSize, isJobOrdersTab);
        ViewBag.Invoices = await BuildArchivedInvoicesAsync(shopId, search, invStatus, invPage, pageSize, isInvoicesTab);
        ViewBag.DeactivatedUsers = await BuildDeactivatedUsersAsync(shopId, search, usrPage, pageSize, isUsersTab);

        var (customers, customersTotal) = await BuildArchivedCustomersAsync(shopId, search, custPage, pageSize, isCustomersTab);
        ViewBag.ArchivedCustomers = customers;
        ViewBag.ArchivedCustomersTotal = customersTotal;
        ViewBag.ArchivedCustomersPage = custPage;

        var (services, servicesTotal) = await BuildArchivedServicesAsync(shopId, search, svcPage, pageSize, isServicesTab);
        ViewBag.ArchivedServices = services;
        ViewBag.ArchivedServicesTotal = servicesTotal;
        ViewBag.ArchivedServicesPage = svcPage;

        var (inventoryItems, inventoryTotal) = await BuildArchivedInventoryAsync(shopId, search, invtPage, pageSize, isInventoryTab);
        ViewBag.ArchivedInventory = inventoryItems;
        ViewBag.ArchivedInventoryTotal = inventoryTotal;
        ViewBag.ArchivedInventoryPage = invtPage;
        ViewBag.PageSize = pageSize;

        return View();
    }

    private async Task<JobOrderListViewModel> BuildArchivedJobOrdersAsync(
        long shopId,
        string? search,
        JobOrderStatus? status,
        int page,
        int pageSize,
        bool applySearch)
    {
        var query = _db.JobOrders
            .Where(j => j.ShopId == shopId && j.IsArchived)
            .AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        if (applySearch && !string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(j =>
                j.JobOrderNo.ToLower().Contains(term) ||
                (j.Customer!.FirstName + " " + j.Customer.LastName).ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(j => j.ArchivedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobOrderItemViewModel
            {
                Id = j.JobOrderId,
                JobNumber = j.JobOrderNo,
                JobOrderNumber = j.JobOrderNo,
                OrderNumber = j.JobOrderNo,
                CustomerName = j.Customer!.FirstName + " " + j.Customer.LastName,
                CustomerInitials = GetInitials(j.Customer!.FirstName + " " + j.Customer.LastName),
                DeviceType = j.Device!.DeviceType,
                DeviceInfo = j.Device.DeviceType + " - " + j.Device.Brand + " " + j.Device.Model,
                DeviceBrand = j.Device.Brand,
                DeviceModel = j.Device.Model,
                Status = j.Status,
                TechnicianName = j.AssignedTechUser != null
                    ? j.AssignedTechUser.FirstName + " " + j.AssignedTechUser.LastName
                    : null,
                CreatedAt = j.CreatedAt
            })
            .ToListAsync();

        return new JobOrderListViewModel
        {
            SearchTerm = applySearch ? search : null,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = totalCount,
            PageSize = pageSize,
            JobOrders = items
        };
    }

    private async Task<InvoiceListViewModel> BuildArchivedInvoicesAsync(
        long shopId,
        string? search,
        InvoiceStatus? status,
        int page,
        int pageSize,
        bool applySearch)
    {
        var query = _db.Invoices
            .Where(i => i.ShopId == shopId && i.IsArchived)
            .AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        if (applySearch && !string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceNo.ToLower().Contains(term) ||
                (i.Customer!.FirstName + " " + i.Customer.LastName).ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(i => i.ArchivedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceItemViewModel
            {
                Id = i.InvoiceId,
                InvoiceNumber = i.InvoiceNo,
                CustomerName = i.Customer!.FirstName + " " + i.Customer.LastName,
                CustomerInitials = GetInitials(i.Customer!.FirstName + " " + i.Customer.LastName),
                Total = i.TotalAmount,
                AmountPaid = i.AmountPaid,
                Balance = i.Balance,
                Status = i.Status,
                CreatedAt = i.InvoiceDate,
                DueDate = i.DueDate
            })
            .ToListAsync();

        return new InvoiceListViewModel
        {
            SearchTerm = applySearch ? search : null,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = totalCount,
            PageSize = pageSize,
            Invoices = items
        };
    }

    private async Task<UserListViewModel> BuildDeactivatedUsersAsync(
        long shopId,
        string? search,
        int page,
        int pageSize,
        bool applySearch)
    {
        var query = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ShopId == shopId && !u.IsActive)
            .Where(u => !u.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"))
            .AsNoTracking();

        if (applySearch && !string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            var termHash = _emailSecurity.ComputeHash(term);
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).ToLower().Contains(term) ||
                (u.EmailHash != null && u.EmailHash == termHash));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new UserListViewModel
        {
            SearchTerm = applySearch ? search : null,
            CurrentPage = page,
            TotalCount = totalCount,
            PageSize = pageSize,
            Users = items.Select(u =>
            {
                var roleName = u.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
                _ = Enum.TryParse<UserRole>(roleName, out var parsedRole);
                return new UserItemViewModel
                {
                    Id = u.UserId,
                    FullName = u.FullName,
                    Initials = GetInitials(u.FullName),
                    Email = u.Email ?? "",
                    Phone = u.Phone,
                    Role = parsedRole,
                    RoleName = roleName,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                };
            }).ToList()
        };
    }

    private async Task<(List<Customer> Items, int Total)> BuildArchivedCustomersAsync(
        long shopId,
        string? search,
        int page,
        int pageSize,
        bool applySearch)
    {
        var query = _db.Customers
            .Where(c => c.ShopId == shopId && !c.IsActive)
            .AsNoTracking();

        if (applySearch && !string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            var termHash = _emailSecurity.ComputeHash(term);
            query = query.Where(c =>
                (c.FirstName + " " + c.LastName).ToLower().Contains(term) ||
                (c.EmailHash != null && c.EmailHash == termHash));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(c => c.FirstName).ThenBy(c => c.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    private async Task<(List<ServiceCatalog> Items, int Total)> BuildArchivedServicesAsync(
        long shopId,
        string? search,
        int page,
        int pageSize,
        bool applySearch)
    {
        var query = _db.ServiceCatalogs
            .Include(s => s.ServiceCategory)
            .Where(s => s.ShopId == shopId && !s.IsActive)
            .AsNoTracking();

        if (applySearch && !string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s => s.ServiceName.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(s => s.ServiceName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    private async Task<(List<InventoryItem> Items, int Total)> BuildArchivedInventoryAsync(
        long shopId,
        string? search,
        int page,
        int pageSize,
        bool applySearch)
    {
        var query = _db.InventoryItems
            .Include(i => i.InventoryCategory)
            .Where(i => i.ShopId == shopId && !i.IsActive)
            .AsNoTracking();

        if (applySearch && !string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i =>
                i.ItemName.ToLower().Contains(term) ||
                i.SKU.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(i => i.ItemName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    [HttpPost("/Archive/RestoreJobOrder/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreJobOrder(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth");
        if (!ModelState.IsValid) return BadRequest();
        var shopId = User.GetShopId();
        var jo = await _db.JobOrders.FirstOrDefaultAsync(j => j.ShopId == shopId && j.JobOrderId == id);
        if (jo == null) return NotFound();

        jo.IsArchived = false;
        jo.ArchivedDate = null;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Restore", "JobOrder", jo.JobOrderId,
            $"Restored job order {jo.JobOrderNo} from archive",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Job order {jo.JobOrderNo} restored.";
        return RedirectToAction(nameof(Index), new { tab = "joborders" });
    }

    [HttpPost("/Archive/RestoreInvoice/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreInvoice(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth");
        if (!ModelState.IsValid) return BadRequest();
        var shopId = User.GetShopId();
        var inv = await _db.Invoices.FirstOrDefaultAsync(i => i.ShopId == shopId && i.InvoiceId == id);
        if (inv == null) return NotFound();

        inv.IsArchived = false;
        inv.ArchivedDate = null;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Restore", "Invoice", inv.InvoiceId,
            $"Restored invoice {inv.InvoiceNo} from archive",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Invoice {inv.InvoiceNo} restored.";
        return RedirectToAction(nameof(Index), new { tab = "invoices" });
    }

    [HttpPost("/Archive/ReactivateUser/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateUser(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth");
        if (!ModelState.IsValid) return BadRequest();
        var shopId = User.GetShopId();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.ShopId == shopId && u.UserId == id);
        if (user == null) return NotFound();

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Reactivate", "User", user.UserId,
            $"Reactivated user '{user.UserName}' from archive",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"User {user.FirstName} {user.LastName} reactivated.";
        return RedirectToAction(nameof(Index), new { tab = "users" });
    }

    [HttpPost("/Archive/RestoreCustomer/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreCustomer(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth");
        if (!ModelState.IsValid) return BadRequest();
        var shopId = User.GetShopId();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.ShopId == shopId && c.CustomerId == id);
        if (customer == null) return NotFound();

        customer.IsActive = true;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Restore", "Customer", customer.CustomerId,
            $"Restored customer '{customer.FullName}' from archive",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Customer {customer.FullName} restored.";
        return RedirectToAction(nameof(Index), new { tab = "customers" });
    }

    [HttpPost("/Archive/RestoreService/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreService(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth");
        if (!ModelState.IsValid) return BadRequest();
        var shopId = User.GetShopId();
        var svc = await _db.ServiceCatalogs.FirstOrDefaultAsync(s => s.ShopId == shopId && s.ServiceId == id);
        if (svc == null) return NotFound();

        svc.IsActive = true;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Restore", "Service", svc.ServiceId,
            $"Restored discontinued service '{svc.ServiceName}'",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Service {svc.ServiceName} restored.";
        return RedirectToAction(nameof(Index), new { tab = "services" });
    }

    [HttpPost("/Archive/RestoreInventory/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreInventory(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth");
        if (!ModelState.IsValid) return BadRequest();
        var shopId = User.GetShopId();
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.ShopId == shopId && i.ItemId == id);
        if (item == null) return NotFound();

        item.IsActive = true;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, User.GetUserId(), "Restore", "InventoryItem", item.ItemId,
            $"Restored inventory item '{item.ItemName}' from archive",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = $"Item {item.ItemName} restored.";
        return RedirectToAction(nameof(Index), new { tab = "inventory" });
    }
}
