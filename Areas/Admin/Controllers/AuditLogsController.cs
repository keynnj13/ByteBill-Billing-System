using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class AuditLogsController : Controller
{
    private readonly ApplicationDbContext _db;
    private static readonly string[] SecurityActions =
    [
        "Login",
        "Logout",
        "FailedLogin",
        "SecurityLockedLoginAttempt",
        "SecurityPermanentLockLoginAttempt",
        "PasswordResetRequested",
        "PasswordResetCompleted",
        "SecuritySuspendSuperAdmin",
        "SecurityRestoreSuperAdmin",
        "SecurityUnlockUser"
    ];

    public AuditLogsController(ApplicationDbContext db)
    {
        _db = db;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? entity, string? logAction, DateTime? dateFrom, DateTime? dateTo, string logType = "transaction", int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid filters.";
            return RedirectToAction(nameof(Index));
        }

        var shopId = User.GetShopId();
        const int pageSize = 5;

        var query = _db.AuditLogs
            .Where(a => a.ShopId == shopId)
            .Where(a => a.UserId == null || !a.User!.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"))
            .AsQueryable();

        query = ApplyLogTypeFilter(query, logType);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a =>
                a.EntityName.ToLower().Contains(s) ||
                a.Action.ToLower().Contains(s) ||
                (a.Details != null && a.Details.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(entity))
            query = query.Where(a => a.EntityName == entity);

        if (!string.IsNullOrWhiteSpace(logAction))
            query = query.Where(a => a.Action == logAction);

        if (dateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= dateTo.Value.AddDays(1));

        var totalCount = await query.CountAsync();

        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = todayStart.AddDays(-7);

        var allShopLogs = _db.AuditLogs.Where(a => a.ShopId == shopId)
            .Where(a => a.UserId == null || !a.User!.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"));
        allShopLogs = ApplyLogTypeFilter(allShopLogs, logType);
        var todayCount = await allShopLogs.CountAsync(a => a.CreatedAt >= todayStart);
        var weekCount = await allShopLogs.CountAsync(a => a.CreatedAt >= weekStart);

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogItemViewModel
            {
                Id = a.AuditLogId,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Details = a.Details,
                IpAddress = a.IpAddress,
                UserName = a.User != null ? a.User.FirstName + " " + a.User.LastName : "System",
                UserInitials = a.User != null
                    ? (a.User.FirstName.Length > 0 ? a.User.FirstName.Substring(0, 1) : "") +
                      (a.User.LastName.Length > 0 ? a.User.LastName.Substring(0, 1) : "")
                    : "SY",
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        // Get distinct entity names and action types for filter dropdowns
        var filteredEntitiesQuery = _db.AuditLogs
            .Where(a => a.ShopId == shopId)
            .Where(a => a.UserId == null || !a.User!.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"))
            .AsQueryable();
        filteredEntitiesQuery = ApplyLogTypeFilter(filteredEntitiesQuery, logType);

        var entityNames = await filteredEntitiesQuery
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();

        var filteredActionsQuery = _db.AuditLogs
            .Where(a => a.ShopId == shopId)
            .Where(a => a.UserId == null || !a.User!.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"))
            .AsQueryable();
        filteredActionsQuery = ApplyLogTypeFilter(filteredActionsQuery, logType);

        var actionTypes = await filteredActionsQuery
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        var viewModel = new AuditLogListViewModel
        {
            LogType = string.Equals(logType, "security", StringComparison.OrdinalIgnoreCase) ? "security" : "transaction",
            SearchTerm = search,
            EntityFilter = entity,
            ActionFilter = logAction,
            DateFrom = dateFrom,
            DateTo = dateTo,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TodayCount = todayCount,
            ThisWeekCount = weekCount,
            EntityNames = entityNames,
            ActionTypes = actionTypes,
            Logs = logs
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Poll(string? search, string? entity, string? logAction, DateTime? dateFrom, DateTime? dateTo, string logType = "transaction", int page = 1)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid filters." });

        var shopId = User.GetShopId();
        const int pageSize = 5;

        var query = _db.AuditLogs.Where(a => a.ShopId == shopId)
            .Where(a => a.UserId == null || !a.User!.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin"))
            .AsQueryable();

        query = ApplyLogTypeFilter(query, logType);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a =>
                a.EntityName.ToLower().Contains(s) ||
                a.Action.ToLower().Contains(s) ||
                (a.Details != null && a.Details.ToLower().Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(entity))
            query = query.Where(a => a.EntityName == entity);
        if (!string.IsNullOrWhiteSpace(logAction))
            query = query.Where(a => a.Action == logAction);
        if (dateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= dateTo.Value.AddDays(1));

        var totalCount = await query.CountAsync();
        var now = DateTime.UtcNow;
        var allShopLogs = _db.AuditLogs.Where(a => a.ShopId == shopId);
        allShopLogs = ApplyLogTypeFilter(allShopLogs, logType);
        var todayCount = await allShopLogs.CountAsync(a => a.CreatedAt >= now.Date);
        var weekCount = await allShopLogs.CountAsync(a => a.CreatedAt >= now.Date.AddDays(-7));

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                id = a.AuditLogId,
                action = a.Action,
                entityName = a.EntityName,
                entityId = a.EntityId,
                details = a.Details ?? "—",
                ipAddress = a.IpAddress ?? "—",
                userName = a.User != null ? a.User.FirstName + " " + a.User.LastName : "System",
                createdAt = a.CreatedAt.ToString("MMM dd, h:mm tt"),
                year = a.CreatedAt.ToString("yyyy"),
                actionClass = GetActionClass(a.Action)
            })
            .ToListAsync();

        return Json(new { totalCount, todayCount, weekCount, logs, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize), currentPage = page });
    }

    private static string GetActionClass(string action) => action switch
    {
        "Login" or "Logout" => "status-info",
        "FailedLogin" or "SecurityLockedLoginAttempt" or "SecurityPermanentLockLoginAttempt" => "status-danger",
        "Create" => "status-success",
        "Update" or "Adjustment" => "status-warning",
        "Archive" or "Deactivate" or "Delete" => "status-danger",
        "Restore" or "Activate" => "status-purple",
        "StatusChange" or "AssignTechnician" => "status-primary",
        _ => "status-default"
    };

    private static bool IsSecurityAction(string action)
    {
        return SecurityActions.Contains(action);
    }

    private static IQueryable<ByteBill_BS.Models.AuditLog> ApplyLogTypeFilter(IQueryable<ByteBill_BS.Models.AuditLog> query, string logType)
    {
        var isSecurity = string.Equals(logType, "security", StringComparison.OrdinalIgnoreCase);
        return isSecurity
            ? query.Where(a => SecurityActions.Contains(a.Action))
            : query.Where(a => !SecurityActions.Contains(a.Action));
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        if (!ModelState.IsValid) return BadRequest();

        var shopId = User.GetShopId();
        var log = await _db.AuditLogs
            .Where(a => a.AuditLogId == id && a.ShopId == shopId)
            .Select(a => new AuditLogDetailViewModel
            {
                Id = a.AuditLogId,
                Action = a.Action,
                EntityName = a.EntityName,
                EntityId = a.EntityId,
                Details = a.Details,
                UserName = a.User != null ? a.User.FirstName + " " + a.User.LastName : "System",
                UserEmail = a.User != null ? (a.User.Email ?? "—") : "—",
                CreatedAt = a.CreatedAt,
                IpAddress = a.IpAddress,
                OldValues = a.OldValues,
                NewValues = a.NewValues
            })
            .FirstOrDefaultAsync();

        if (log is null) return NotFound();

        return PartialView("_DetailsModal", log);
    }
}
