using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IEmailSecurityService _emailSecurity;

    public UsersController(ApplicationDbContext db, IAuditService audit, IEmailSecurityService emailSecurity)
    {
        _db = db;
        _audit = audit;
        _emailSecurity = emailSecurity;
    }

    private bool IsAuthorized() => User.IsInRoles("Admin", "SuperAdmin");

    private static bool IsAjaxRequest(string? requestedWith)
        => string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private static string GetInitials(string firstName, string lastName)
    {
        var f = firstName.Length > 0 ? firstName[0].ToString().ToUpper() : "";
        var l = lastName.Length > 0 ? lastName[0].ToString().ToUpper() : "";
        return $"{f}{l}".Trim();
    }

    // ─── INDEX ──────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index(string? search, UserRole? role, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid filters.";
            return RedirectToAction(nameof(Index));
        }

        var shopId = User.GetShopId();
        var currentUserId = User.GetUserId();
        var pageSize = 10;

        var query = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ShopId == shopId)
            .Where(u => u.UserId != currentUserId) // Hide admin's own account
            .Where(u => !u.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin")) // Hide SuperAdmin users
            .Where(u => u.IsActive) // Deactivated users only in Archive
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            var termHash = _emailSecurity.ComputeHash(term);
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).ToLower().Contains(term) ||
                (u.EmailHash != null && u.EmailHash == termHash) ||
                u.UserName.ToLower().Contains(term));
        }

        if (role.HasValue)
        {
            var roleName = role.Value.ToString();
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role!.RoleName == roleName));
        }

        var totalCount = await query.CountAsync();

        // Stats from full dataset — single query with conditional counts
        var allUsersQuery = _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ShopId == shopId)
            .Where(u => u.UserId != currentUserId)
            .Where(u => !u.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"))
            .AsNoTracking();

        var stats = await allUsersQuery
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ActiveCount = g.Count(u => u.IsActive),
                AdminCount = g.Count(u => u.UserRoles.Any(ur => ur.Role!.RoleName == "Admin")),
                BillingCount = g.Count(u => u.UserRoles.Any(ur => ur.Role!.RoleName == "Billing")),
                TechnicianCount = g.Count(u => u.UserRoles.Any(ur => ur.Role!.RoleName == "Technician"))
            })
            .FirstOrDefaultAsync();

        var activeCount = stats?.ActiveCount ?? 0;
        var adminCount = stats?.AdminCount ?? 0;
        var billingCount = stats?.BillingCount ?? 0;
        var technicianCount = stats?.TechnicianCount ?? 0;

        var users = await query
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get last login times from audit logs
        var userIds = users.Select(u => u.UserId).ToList();
        var lastLogins = await _db.AuditLogs
            .Where(a => a.Action == "Login" && a.EntityName == "User" && a.UserId.HasValue && userIds.Contains(a.UserId.Value))
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { UserId = g.Key, LastLogin = g.Max(a => a.CreatedAt) })
            .ToDictionaryAsync(g => g.UserId, g => g.LastLogin);

        var viewModel = new UserListViewModel
        {
            SearchTerm = search,
            RoleFilter = role,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            ActiveCount = activeCount,
            AdminCount = adminCount,
            BillingCount = billingCount,
            TechnicianCount = technicianCount,
            Users = users.Select(u =>
            {
                var roleName = u.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
                _ = Enum.TryParse<UserRole>(roleName, out var parsedRole);
                lastLogins.TryGetValue(u.UserId, out var lastLogin);
                return new UserItemViewModel
                {
                    Id = u.UserId,
                    FullName = u.FullName,
                    Initials = GetInitials(u.FirstName, u.LastName),
                    Email = u.Email ?? "",
                    Phone = u.Phone,
                    Role = parsedRole,
                    RoleName = roleName,
                    IsActive = u.IsActive,
                    LastLoginAt = lastLogin,
                    CreatedAt = u.CreatedAt
                };
            }).ToList()
        };

        return View(viewModel);
    }

    // ─── CREATE ─────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        return View(new UserFormViewModel());
    }

    [HttpGet]
    public IActionResult CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_CreateModal", new UserFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        UserFormViewModel model,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        // Prevent creating Admin or SuperAdmin users
        if (model.Role == UserRole.Admin || model.Role == UserRole.SuperAdmin)
        {
            ModelState.AddModelError("Role", "You cannot create Admin or SuperAdmin users.");
        }

        if (model.Id == 0 && string.IsNullOrEmpty(model.Password))
            ModelState.AddModelError("Password", "Password is required for new users.");

        if (!string.IsNullOrEmpty(model.UserName))
        {
            var shopId = User.GetShopId();
            var exists = await _db.Users.AnyAsync(u => u.ShopId == shopId && u.UserName == model.UserName.Trim().ToLower());
            if (exists)
                ModelState.AddModelError("UserName", "This username is already taken.");
        }

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest(requestedWith))
                return PartialView("_CreateModal", model);
            return View(model);
        }

        var currentShopId = User.GetShopId();
        var currentUserId = User.GetUserId();

        // Create user
        var user = new User
        {
            ShopId = currentShopId,
            FirstName = model.FirstName.Trim(),
            MiddleName = model.MiddleName?.Trim(),
            LastName = model.LastName.Trim(),
            UserName = model.UserName!.Trim().ToLower(),
            Email = model.Email,
            Phone = model.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: 12),
            IsActive = model.IsActive
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Assign role
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == model.Role.ToString());
        if (role != null)
        {
            _db.UserRoles.Add(new UserRoleAssignment
            {
                UserId = user.UserId,
                RoleId = role.RoleId,
                AssignedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        // Audit log
        await _audit.LogAsync(currentShopId, currentUserId, "Create", "User", user.UserId,
            $"Created user '{user.UserName}' with role '{model.Role}'",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = "User created successfully!";
        if (IsAjaxRequest(requestedWith))
            return Json(new { success = true, message = "User created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    // ─── EDIT ───────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        if (!ModelState.IsValid) return BadRequest();
        var model = await GetEditModelAsync(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        if (!ModelState.IsValid) return BadRequest();
        var model = await GetEditModelAsync(id);
        if (model == null) return NotFound();
        return PartialView("_EditModal", model);
    }

    private async Task<UserFormViewModel?> GetEditModelAsync(long id)
    {
        var shopId = User.GetShopId();
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ShopId == shopId && u.UserId == id)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (user == null) return null;

        var roleName = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
        _ = Enum.TryParse<UserRole>(roleName, out var parsedRole);

        return new UserFormViewModel
        {
            Id = user.UserId,
            FirstName = user.FirstName,
            MiddleName = user.MiddleName,
            LastName = user.LastName,
            UserName = user.UserName,
            Email = user.Email ?? "",
            Phone = user.Phone,
            Role = parsedRole,
            IsActive = user.IsActive
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        UserFormViewModel model,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        ModelState.Remove("Password");
        ModelState.Remove("ConfirmPassword");
        ModelState.Remove("UserName");

        // Prevent assigning Admin or SuperAdmin role
        if (model.Role == UserRole.Admin || model.Role == UserRole.SuperAdmin)
        {
            ModelState.AddModelError("Role", "You cannot assign Admin or SuperAdmin role.");
        }

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest(requestedWith))
                return PartialView("_EditModal", model);
            return View(model);
        }

        var shopId = User.GetShopId();
        var userId = User.GetUserId();

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .Where(u => u.ShopId == shopId && u.UserId == model.Id)
            .FirstOrDefaultAsync();

        if (user == null) return NotFound();

        user.FirstName = model.FirstName.Trim();
        user.MiddleName = model.MiddleName?.Trim();
        user.LastName = model.LastName.Trim();
        user.Email = model.Email;
        user.Phone = model.Phone;
        user.IsActive = model.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        // Update password if provided
        if (!string.IsNullOrEmpty(model.Password))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: 12);
        }

        // Update role if changed
        var currentRoleName = user.UserRoles.FirstOrDefault()?.Role?.RoleName;
        if (currentRoleName != model.Role.ToString())
        {
            // Remove existing role assignments
            _db.UserRoles.RemoveRange(user.UserRoles);
            await _db.SaveChangesAsync();

            // Add new role
            var newRole = await _db.Roles.FirstOrDefaultAsync(r => r.RoleName == model.Role.ToString());
            if (newRole != null)
            {
                _db.UserRoles.Add(new UserRoleAssignment
                {
                    UserId = user.UserId,
                    RoleId = newRole.RoleId,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync(shopId, userId, "Update", "User", user.UserId,
            $"Updated user '{user.UserName}'",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        TempData["Success"] = "User updated successfully!";
        if (IsAjaxRequest(requestedWith))
            return Json(new { success = true, message = "User updated successfully!" });
        return RedirectToAction(nameof(Index));
    }

    // ─── DETAILS ────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        if (!ModelState.IsValid) return BadRequest();
        var model = await GetDetailModelAsync(id);
        if (model == null) return NotFound();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        if (!ModelState.IsValid) return BadRequest();
        var model = await GetDetailModelAsync(id);
        if (model == null) return NotFound();
        return PartialView("_DetailsModal", model);
    }

    private async Task<UserDetailViewModel?> GetDetailModelAsync(long id)
    {
        var shopId = User.GetShopId();
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Where(u => u.ShopId == shopId && u.UserId == id)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (user == null) return null;

        var roleName = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
        _ = Enum.TryParse<UserRole>(roleName, out var parsedRole);

        // Get last login
        var lastLogin = await _db.AuditLogs
            .Where(a => a.Action == "Login" && a.EntityName == "User" && a.UserId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        // Get activity stats – role-specific
        int jobOrdersHandled = 0, partsUsed = 0, invoicesCreated = 0, paymentsProcessed = 0,
            logsReviewed = 0, reportsGenerated = 0, usersManagedCount = 0, totalActivityCount = 0;

        switch (parsedRole)
        {
            case UserRole.Technician:
                jobOrdersHandled = await _db.JobOrders.CountAsync(j => j.ShopId == shopId && j.AssignedTechUserId == id);
                partsUsed = await _db.JobOrderParts.CountAsync(p => p.JobOrder!.ShopId == shopId && p.JobOrder.AssignedTechUserId == id);
                break;
            case UserRole.Billing:
                invoicesCreated = await _db.AuditLogs.CountAsync(a => a.UserId == id && a.EntityName == "Invoice" && a.Action == "Create");
                paymentsProcessed = await _db.AuditLogs.CountAsync(a => a.UserId == id && a.EntityName == "Payment");
                break;
            case UserRole.Auditor:
                logsReviewed = await _db.AuditLogs.CountAsync(a => a.UserId == id);
                reportsGenerated = await _db.AuditLogs.CountAsync(a => a.UserId == id && a.EntityName == "Report");
                break;
            case UserRole.Admin:
            case UserRole.SuperAdmin:
                usersManagedCount = await _db.AuditLogs.CountAsync(a => a.UserId == id && a.EntityName == "User");
                totalActivityCount = await _db.AuditLogs.CountAsync(a => a.UserId == id);
                break;
        }

        // Get recent activity
        var recentActivity = await _db.AuditLogs
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new UserActivityItem
            {
                Action = a.Action,
                Description = a.Details ?? "",
                Timestamp = a.CreatedAt
            })
            .ToListAsync();

        return new UserDetailViewModel
        {
            Id = user.UserId,
            FullName = user.FullName,
            Initials = GetInitials(user.FirstName, user.LastName),
            Email = user.Email ?? "",
            Phone = user.Phone,
            Role = parsedRole,
            RoleName = roleName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = lastLogin == default ? null : lastLogin,
            JobOrdersHandled = jobOrdersHandled,
            PartsUsed = partsUsed,
            InvoicesCreated = invoicesCreated,
            PaymentsProcessed = paymentsProcessed,
            LogsReviewed = logsReviewed,
            ReportsGenerated = reportsGenerated,
            UsersManagedCount = usersManagedCount,
            TotalActivityCount = totalActivityCount,
            RecentActivity = recentActivity
        };
    }

    // ─── TOGGLE STATUS ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(
        long id,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();
        if (!ModelState.IsValid) return BadRequest();

        var shopId = User.GetShopId();
        var user = await _db.Users
            .Where(u => u.ShopId == shopId && u.UserId == id)
            .FirstOrDefaultAsync();

        if (user == null) return NotFound();

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var statusText = user.IsActive ? "activated" : "deactivated";
        await _audit.LogAsync(shopId, User.GetUserId(), "Update", "User", user.UserId,
            $"User '{user.UserName}' {statusText}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        if (IsAjaxRequest(requestedWith))
            return Json(new { success = true, message = $"User {statusText} successfully!" });
        return RedirectToAction(nameof(Index));
    }
}
