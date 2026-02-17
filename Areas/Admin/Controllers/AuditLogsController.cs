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
    public async Task<IActionResult> Index(string? search, string? entity, string? action, DateTime? dateFrom, DateTime? dateTo, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        const int pageSize = 20;

        var query = _db.AuditLogs
            .Include(a => a.User)
            .Where(a => a.ShopId == shopId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a =>
                a.EntityName.ToLower().Contains(s) ||
                a.Action.ToLower().Contains(s) ||
                (a.User != null && (a.User.FirstName + " " + a.User.LastName).ToLower().Contains(s)) ||
                (a.Details != null && a.Details.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(entity))
            query = query.Where(a => a.EntityName == entity);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (dateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= dateTo.Value.AddDays(1));

        var totalCount = await query.CountAsync();

        // Get stats from full (filtered) dataset
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = todayStart.AddDays(-7);

        var allShopLogs = _db.AuditLogs.Where(a => a.ShopId == shopId);
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
                UserName = a.User != null ? a.User.FirstName + " " + a.User.LastName : "System",
                UserInitials = a.User != null
                    ? (a.User.FirstName.Length > 0 ? a.User.FirstName.Substring(0, 1) : "") +
                      (a.User.LastName.Length > 0 ? a.User.LastName.Substring(0, 1) : "")
                    : "SY",
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        // Get distinct entity names and action types for filter dropdowns
        var entityNames = await _db.AuditLogs
            .Where(a => a.ShopId == shopId)
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();

        var actionTypes = await _db.AuditLogs
            .Where(a => a.ShopId == shopId)
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        var viewModel = new AuditLogListViewModel
        {
            SearchTerm = search,
            EntityFilter = entity,
            ActionFilter = action,
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
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var shopId = User.GetShopId();
        var log = await _db.AuditLogs
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.AuditLogId == id && a.ShopId == shopId);

        if (log is null) return NotFound();

        var detail = new AuditLogDetailViewModel
        {
            Id = log.AuditLogId,
            Action = log.Action,
            EntityName = log.EntityName,
            EntityId = log.EntityId,
            Details = log.Details,
            UserName = log.User != null ? $"{log.User.FirstName} {log.User.LastName}" : "System",
            UserEmail = log.User?.Email ?? "—",
            CreatedAt = log.CreatedAt,
            IpAddress = log.IpAddress,
            OldValues = log.OldValues,
            NewValues = log.NewValues
        };

        return PartialView("_DetailsModal", detail);
    }
}
