using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
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

    public ArchiveController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
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
        int usrPage = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth");

        var shopId = User.GetShopId();
        const int pageSize = 10;

        // ── Job Orders ──
        var joQuery = _db.JobOrders
            .Where(j => j.ShopId == shopId && j.IsArchived)
            .AsNoTracking();

        if (joStatus.HasValue)
            joQuery = joQuery.Where(j => j.Status == joStatus.Value);

        if (tab == "joborders" && !string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            joQuery = joQuery.Where(j =>
                j.JobOrderNo.ToLower().Contains(term) ||
                (j.Customer!.FirstName + " " + j.Customer.LastName).ToLower().Contains(term));
        }

        var joTotal = await joQuery.CountAsync();
        var joItems = await joQuery
            .OrderByDescending(j => j.ArchivedDate)
            .Skip((joPage - 1) * pageSize)
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

        // ── Invoices ──
        var invQuery = _db.Invoices
            .Where(i => i.ShopId == shopId && i.IsArchived)
            .AsNoTracking();

        if (invStatus.HasValue)
            invQuery = invQuery.Where(i => i.Status == invStatus.Value);

        if (tab == "invoices" && !string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            invQuery = invQuery.Where(i =>
                i.InvoiceNo.ToLower().Contains(term) ||
                (i.Customer!.FirstName + " " + i.Customer.LastName).ToLower().Contains(term));
        }

        var invTotal = await invQuery.CountAsync();
        var invItems = await invQuery
            .OrderByDescending(i => i.ArchivedDate)
            .Skip((invPage - 1) * pageSize)
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

        ViewBag.ActiveTab = tab;
        ViewBag.Search = search;

        ViewBag.JoStatusFilter = joStatus;
        ViewBag.InvStatusFilter = invStatus;

        ViewBag.JobOrders = new JobOrderListViewModel
        {
            SearchTerm = tab == "joborders" ? search : null,
            StatusFilter = joStatus,
            CurrentPage = joPage,
            TotalCount = joTotal,
            PageSize = pageSize,
            JobOrders = joItems
        };

        ViewBag.Invoices = new InvoiceListViewModel
        {
            SearchTerm = tab == "invoices" ? search : null,
            StatusFilter = invStatus,
            CurrentPage = invPage,
            TotalCount = invTotal,
            PageSize = pageSize,
            Invoices = invItems
        };

        // ── Deactivated Users ──
        var usrQuery = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ShopId == shopId && !u.IsActive)
            .Where(u => !u.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"))
            .AsNoTracking();

        if (tab == "users" && !string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            usrQuery = usrQuery.Where(u =>
                (u.FirstName + " " + u.LastName).ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        var usrTotal = await usrQuery.CountAsync();
        var usrItems = await usrQuery
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Skip((usrPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.DeactivatedUsers = new UserListViewModel
        {
            SearchTerm = tab == "users" ? search : null,
            CurrentPage = usrPage,
            TotalCount = usrTotal,
            PageSize = pageSize,
            Users = usrItems.Select(u =>
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

        return View();
    }

    [HttpPost("/Archive/RestoreJobOrder/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreJobOrder(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth");
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
}
