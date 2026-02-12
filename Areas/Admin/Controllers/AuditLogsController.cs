using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class AuditLogsController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, string? entity, string? action, DateTime? dateFrom, DateTime? dateTo, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var allLogs = GetDemoLogs();

        if (!string.IsNullOrWhiteSpace(search))
            allLogs = allLogs.Where(l =>
                l.EntityName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                l.Action.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                l.UserName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (l.Details ?? "").Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (!string.IsNullOrWhiteSpace(entity))
            allLogs = allLogs.Where(l => l.EntityName == entity).ToList();

        if (!string.IsNullOrWhiteSpace(action))
            allLogs = allLogs.Where(l => l.Action == action).ToList();

        if (dateFrom.HasValue)
            allLogs = allLogs.Where(l => l.CreatedAt >= dateFrom.Value).ToList();

        if (dateTo.HasValue)
            allLogs = allLogs.Where(l => l.CreatedAt <= dateTo.Value.AddDays(1)).ToList();

        var viewModel = new AuditLogListViewModel
        {
            SearchTerm = search,
            EntityFilter = entity,
            ActionFilter = action,
            DateFrom = dateFrom,
            DateTo = dateTo,
            CurrentPage = page,
            TotalCount = allLogs.Count,
            TodayCount = allLogs.Count(l => l.CreatedAt.Date == DateTime.Today),
            ThisWeekCount = allLogs.Count(l => l.CreatedAt >= DateTime.Today.AddDays(-7)),
            EntityNames = new() { "Customer", "Invoice", "Payment", "JobOrder", "Service", "Inventory", "User" },
            ActionTypes = new() { "Create", "Update", "Delete", "Login", "Logout" },
            Logs = allLogs.Skip((page - 1) * 20).Take(20).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var detail = new AuditLogDetailViewModel
        {
            Id = id,
            Action = "Update",
            EntityName = "Invoice",
            EntityId = 42,
            Details = "Updated invoice status from 'Unpaid' to 'Partial'. Amount paid: ₱1,200.00. Balance: ₱1,300.00.",
            UserName = "Emily Brown",
            UserEmail = "emily@techfixpro.com",
            CreatedAt = DateTime.Now.AddHours(-1),
            IpAddress = "192.168.1.15"
        };

        return PartialView("_DetailsModal", detail);
    }

    // ═══════════════════════════════════════════════════════
    //  DEMO DATA
    // ═══════════════════════════════════════════════════════

    private static List<AuditLogItemViewModel> GetDemoLogs() => new()
    {
        new() { Id = 1,  Action = "Login",  EntityName = "User",      EntityId = 2,  Details = "User logged in successfully",                                     UserName = "Emily Brown",     UserInitials = "EB", CreatedAt = DateTime.Now.AddMinutes(-15) },
        new() { Id = 2,  Action = "Create", EntityName = "Payment",   EntityId = 58, Details = "Created payment PAY-000058 for ₱1,200.00 (Cash)",                  UserName = "Emily Brown",     UserInitials = "EB", CreatedAt = DateTime.Now.AddMinutes(-30) },
        new() { Id = 3,  Action = "Update", EntityName = "Invoice",   EntityId = 42, Details = "Updated invoice status from 'Unpaid' to 'Partial'",                UserName = "Emily Brown",     UserInitials = "EB", CreatedAt = DateTime.Now.AddMinutes(-31) },
        new() { Id = 4,  Action = "Create", EntityName = "JobOrder",  EntityId = 35, Details = "Created job order JO-2025-0035 for customer Alice Thompson",        UserName = "David Lee",       UserInitials = "DL", CreatedAt = DateTime.Now.AddHours(-1) },
        new() { Id = 5,  Action = "Update", EntityName = "JobOrder",  EntityId = 34, Details = "Updated job order status from 'In Progress' to 'Completed'",        UserName = "David Lee",       UserInitials = "DL", CreatedAt = DateTime.Now.AddHours(-2) },
        new() { Id = 6,  Action = "Create", EntityName = "Customer",  EntityId = 15, Details = "Created new customer: Carlos Rivera, carlos@email.com",             UserName = "John Anderson",   UserInitials = "JA", CreatedAt = DateTime.Now.AddHours(-3) },
        new() { Id = 7,  Action = "Update", EntityName = "Inventory", EntityId = 8,  Details = "Stock adjusted: LCD Screen 15.6\" qty changed from 12 to 10 (-2)", UserName = "David Lee",       UserInitials = "DL", CreatedAt = DateTime.Now.AddHours(-4) },
        new() { Id = 8,  Action = "Create", EntityName = "Invoice",   EntityId = 43, Details = "Created invoice INV-2025-0043 for ₱3,500.00",                      UserName = "Emily Brown",     UserInitials = "EB", CreatedAt = DateTime.Now.AddHours(-5) },
        new() { Id = 9,  Action = "Delete", EntityName = "Service",   EntityId = 12, Details = "Deactivated service: Network Cable Installation",                   UserName = "John Anderson",   UserInitials = "JA", CreatedAt = DateTime.Now.AddHours(-6) },
        new() { Id = 10, Action = "Login",  EntityName = "User",      EntityId = 3,  Details = "User logged in successfully",                                      UserName = "David Lee",       UserInitials = "DL", CreatedAt = DateTime.Now.AddHours(-8) },
        new() { Id = 11, Action = "Update", EntityName = "Customer",  EntityId = 3,  Details = "Updated customer email: bob.smith@email.com → bob.s@email.com",    UserName = "John Anderson",   UserInitials = "JA", CreatedAt = DateTime.Now.AddDays(-1) },
        new() { Id = 12, Action = "Create", EntityName = "Payment",   EntityId = 57, Details = "Created payment PAY-000057 for ₱850.00 (GCash)",                   UserName = "Emily Brown",     UserInitials = "EB", CreatedAt = DateTime.Now.AddDays(-1) },
        new() { Id = 13, Action = "Logout", EntityName = "User",      EntityId = 5,  Details = "User logged out",                                                  UserName = "Robert Taylor",   UserInitials = "RT", CreatedAt = DateTime.Now.AddDays(-1) },
        new() { Id = 14, Action = "Update", EntityName = "Service",   EntityId = 2,  Details = "Updated base price from ₱500.00 to ₱550.00",                       UserName = "John Anderson",   UserInitials = "JA", CreatedAt = DateTime.Now.AddDays(-2) },
        new() { Id = 15, Action = "Create", EntityName = "Inventory", EntityId = 22, Details = "Added new item: Thermal Paste MX-4 (SKU: TP-MX4-001)",             UserName = "John Anderson",   UserInitials = "JA", CreatedAt = DateTime.Now.AddDays(-2) },
    };
}
