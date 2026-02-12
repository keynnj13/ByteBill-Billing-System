using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class UsersController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    // ─── INDEX ──────────────────────────────────────────────
    [HttpGet]
    public IActionResult Index(string? search, UserRole? role, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var allUsers = GetDemoUsers();

        if (!string.IsNullOrWhiteSpace(search))
            allUsers = allUsers
                .Where(u => u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        if (role.HasValue)
            allUsers = allUsers.Where(u => u.Role == role.Value).ToList();

        var viewModel = new UserListViewModel
        {
            SearchTerm = search,
            RoleFilter = role,
            CurrentPage = page,
            TotalCount = allUsers.Count,
            ActiveCount = allUsers.Count(u => u.IsActive),
            AdminCount = allUsers.Count(u => u.Role == UserRole.Admin),
            TechnicianCount = allUsers.Count(u => u.Role == UserRole.Technician),
            Users = allUsers.Skip((page - 1) * 10).Take(10).ToList()
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
    public IActionResult Create(UserFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (model.Id == 0 && string.IsNullOrEmpty(model.Password))
            ModelState.AddModelError("Password", "Password is required for new users");

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }

        TempData["Success"] = "User created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "User created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    // ─── EDIT ───────────────────────────────────────────────
    [HttpGet]
    public IActionResult Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        return View(GetEditModel(id));
    }

    [HttpGet]
    public IActionResult EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_EditModal", GetEditModel(id));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(UserFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        ModelState.Remove("Password");
        ModelState.Remove("ConfirmPassword");

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }

        TempData["Success"] = "User updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "User updated successfully!" });
        return RedirectToAction(nameof(Index));
    }

    // ─── DETAILS ────────────────────────────────────────────
    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        return View(GetDetailModel(id));
    }

    [HttpGet]
    public IActionResult DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_DetailsModal", GetDetailModel(id));
    }

    // ─── TOGGLE STATUS ──────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleStatus(long id)
    {
        if (!IsAuthorized()) return Forbid();
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "User status updated!" });
        return RedirectToAction(nameof(Index));
    }

    // ═════════════════════════════════════════════════════════
    //  DEMO DATA
    // ═════════════════════════════════════════════════════════

    private static List<UserItemViewModel> GetDemoUsers() => new()
    {
        new() { Id = 1, FullName = "John Anderson",   Initials = "JA", Email = "john@techfixpro.com",   Phone = "0917-123-4567", Role = UserRole.Admin,      RoleName = "Admin",      IsActive = true,  LastLoginAt = DateTime.Now.AddHours(-2),    CreatedAt = DateTime.Now.AddMonths(-6) },
        new() { Id = 2, FullName = "Emily Brown",     Initials = "EB", Email = "emily@techfixpro.com",  Phone = "0918-234-5678", Role = UserRole.Billing,    RoleName = "Billing",    IsActive = true,  LastLoginAt = DateTime.Now.AddHours(-1),    CreatedAt = DateTime.Now.AddMonths(-5) },
        new() { Id = 3, FullName = "David Lee",       Initials = "DL", Email = "david@techfixpro.com",  Phone = "0919-345-6789", Role = UserRole.Technician, RoleName = "Technician", IsActive = true,  LastLoginAt = DateTime.Now.AddMinutes(-30), CreatedAt = DateTime.Now.AddMonths(-4) },
        new() { Id = 4, FullName = "Emily Chen",      Initials = "EC", Email = "echen@techfixpro.com",  Phone = "0920-456-7890", Role = UserRole.Technician, RoleName = "Technician", IsActive = true,  LastLoginAt = DateTime.Now.AddDays(-1),     CreatedAt = DateTime.Now.AddMonths(-3) },
        new() { Id = 5, FullName = "Robert Taylor",   Initials = "RT", Email = "robert@techfixpro.com", Phone = "0921-567-8901", Role = UserRole.Auditor,    RoleName = "Auditor",    IsActive = true,  LastLoginAt = DateTime.Now.AddDays(-3),     CreatedAt = DateTime.Now.AddMonths(-2) },
        new() { Id = 6, FullName = "Maria Santos",    Initials = "MS", Email = "maria@techfixpro.com",  Phone = "0922-678-9012", Role = UserRole.Billing,    RoleName = "Billing",    IsActive = false, LastLoginAt = DateTime.Now.AddDays(-14),    CreatedAt = DateTime.Now.AddMonths(-8) },
        new() { Id = 7, FullName = "James Rodriguez", Initials = "JR", Email = "james@techfixpro.com",  Phone = "0923-789-0123", Role = UserRole.Technician, RoleName = "Technician", IsActive = true,  LastLoginAt = DateTime.Now.AddHours(-5),    CreatedAt = DateTime.Now.AddMonths(-1) },
    };

    private UserFormViewModel GetEditModel(long id) => new()
    {
        Id = id, FirstName = "Emily", MiddleName = "Grace", LastName = "Brown",
        Email = "emily@techfixpro.com", Phone = "0918-234-5678",
        Role = UserRole.Billing, IsActive = true
    };

    private UserDetailViewModel GetDetailModel(long id) => new()
    {
        Id = id, FullName = "Emily Brown", Initials = "EB",
        Email = "emily@techfixpro.com", Phone = "0918-234-5678",
        Role = UserRole.Billing, RoleName = "Billing",
        IsActive = true, CreatedAt = DateTime.Now.AddMonths(-5),
        LastLoginAt = DateTime.Now.AddHours(-1),
        JobOrdersHandled = 0, PaymentsProcessed = 47,
        RecentActivity = new()
        {
            new() { Action = "Payment Recorded", Description = "Recorded ₱1,200.00 payment for INV-2025-0042", Timestamp = DateTime.Now.AddHours(-1) },
            new() { Action = "Payment Recorded", Description = "Recorded ₱850.00 payment for INV-2025-0041",   Timestamp = DateTime.Now.AddHours(-3) },
            new() { Action = "Login",            Description = "Logged in from 192.168.1.15",                   Timestamp = DateTime.Now.AddHours(-1) },
            new() { Action = "Invoice Viewed",   Description = "Viewed INV-2025-0040",                          Timestamp = DateTime.Now.AddDays(-1) },
            new() { Action = "Payment Recorded", Description = "Recorded ₱2,500.00 payment for INV-2025-0039", Timestamp = DateTime.Now.AddDays(-1) },
        }
    };
}
