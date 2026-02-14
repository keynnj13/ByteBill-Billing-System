using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class UsersController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.SuperAdmin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, UserRole? role, string? shop, string? status, int page = 1)
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
        if (!string.IsNullOrEmpty(shop))
            allUsers = allUsers.Where(u => u.ShopName == shop).ToList();
        if (!string.IsNullOrEmpty(status))
            allUsers = allUsers.Where(u => (status == "Active") == u.IsActive).ToList();

        var viewModel = new GlobalUserListViewModel
        {
            SearchTerm = search,
            RoleFilter = role,
            ShopFilter = shop,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = allUsers.Count,
            ActiveCount = allUsers.Count(u => u.IsActive),
            AdminCount = allUsers.Count(u => u.Role == UserRole.Admin),
            SuperAdminCount = allUsers.Count(u => u.Role == UserRole.SuperAdmin),
            ShopCount = allUsers.Select(u => u.ShopName).Distinct().Count(),
            Users = allUsers.Skip((page - 1) * 10).Take(10).ToList(),
            AvailableShops = GetDemoUsers().Select(u => u.ShopName).Distinct().OrderBy(s => s).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        var model = new GlobalUserFormViewModel
        {
            AvailableShops = GetShopDropdown()
        };
        return PartialView("_CreateModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(GlobalUserFormViewModel model)
    {
        if (!IsAuthorized()) return Forbid();

        if (model.Id == 0 && string.IsNullOrEmpty(model.Password))
            ModelState.AddModelError("Password", "Password is required for new users");

        if (!ModelState.IsValid)
        {
            model.AvailableShops = GetShopDropdown();
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "User created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "User created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_EditModal", GetEditModel(id));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(GlobalUserFormViewModel model)
    {
        if (!IsAuthorized()) return Forbid();

        ModelState.Remove("Password");
        ModelState.Remove("ConfirmPassword");

        if (!ModelState.IsValid)
        {
            model.AvailableShops = GetShopDropdown();
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "User updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "User updated successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_DetailsModal", GetDetailModel(id));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleStatus(long id)
    {
        if (!IsAuthorized()) return Forbid();
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "User status updated!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Impersonate(long id)
    {
        if (!IsAuthorized()) return Forbid();
        // In production: create impersonation claims and sign in as the target user
        TempData["Success"] = "Now impersonating user. Click 'Stop Impersonating' to return.";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Impersonation started. Redirecting...", redirectUrl = "/" });
        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResetPassword(long id)
    {
        if (!IsAuthorized()) return Forbid();
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Password reset email sent!" });
        return RedirectToAction(nameof(Index));
    }

    // ── Demo Data ──────────────────────────────────────────────
    private static List<GlobalUserItemViewModel> GetDemoUsers() => new()
    {
        new() { Id = 1,  FullName = "John Anderson",   Initials = "JA", Email = "john@techfixpro.com",    Phone = "0917-123-4567", ShopName = "TechFix Pro",  Role = UserRole.Admin,      RoleName = "Admin",         IsActive = true,  LastLoginAt = DateTime.Now.AddHours(-2),    CreatedAt = DateTime.Now.AddMonths(-6) },
        new() { Id = 2,  FullName = "Emily Brown",     Initials = "EB", Email = "emily@techfixpro.com",   Phone = "0918-234-5678", ShopName = "TechFix Pro",  Role = UserRole.Billing,    RoleName = "Billing Staff", IsActive = true,  LastLoginAt = DateTime.Now.AddHours(-1),    CreatedAt = DateTime.Now.AddMonths(-5) },
        new() { Id = 3,  FullName = "David Lee",       Initials = "DL", Email = "david@techfixpro.com",   Phone = "0919-345-6789", ShopName = "TechFix Pro",  Role = UserRole.Technician, RoleName = "Technician",    IsActive = true,  LastLoginAt = DateTime.Now.AddMinutes(-30), CreatedAt = DateTime.Now.AddMonths(-4) },
        new() { Id = 4,  FullName = "Sarah Miller",    Initials = "SM", Email = "sarah@quickrepairs.com", Phone = "0920-456-7890", ShopName = "QuickRepairs", Role = UserRole.Admin,      RoleName = "Admin",         IsActive = true,  LastLoginAt = DateTime.Now.AddDays(-1),     CreatedAt = DateTime.Now.AddMonths(-4) },
        new() { Id = 5,  FullName = "Mike Chen",       Initials = "MC", Email = "mike@computermd.com",    Phone = "0921-567-8901", ShopName = "ComputerMD",   Role = UserRole.Admin,      RoleName = "Admin",         IsActive = true,  LastLoginAt = DateTime.Now.AddHours(-5),    CreatedAt = DateTime.Now.AddYears(-1) },
        new() { Id = 6,  FullName = "Lisa Wang",       Initials = "LW", Email = "lisa@computermd.com",    Phone = "0922-678-9012", ShopName = "ComputerMD",   Role = UserRole.Billing,    RoleName = "Billing Staff", IsActive = true,  LastLoginAt = DateTime.Now.AddHours(-3),    CreatedAt = DateTime.Now.AddMonths(-10) },
        new() { Id = 7,  FullName = "James Rodriguez", Initials = "JR", Email = "james@techfixpro.com",   Phone = "0923-789-0123", ShopName = "TechFix Pro",  Role = UserRole.Technician, RoleName = "Technician",    IsActive = true,  LastLoginAt = DateTime.Now.AddHours(-5),    CreatedAt = DateTime.Now.AddMonths(-1) },
        new() { Id = 8,  FullName = "Bob Wilson",      Initials = "BW", Email = "bob@oldtech.com",        Phone = "0924-890-1234", ShopName = "OldTech Solutions", Role = UserRole.Admin,  RoleName = "Admin",         IsActive = false, LastLoginAt = DateTime.Now.AddDays(-14),    CreatedAt = DateTime.Now.AddMonths(-8) },
        new() { Id = 9,  FullName = "Ana Reyes",       Initials = "AR", Email = "ana@gadgetcareph.com",   Phone = "0925-901-2345", ShopName = "GadgetCare PH", Role = UserRole.Admin,     RoleName = "Admin",         IsActive = true,  LastLoginAt = DateTime.Now.AddHours(-1),    CreatedAt = DateTime.Now.AddDays(-15) },
        new() { Id = 10, FullName = "Robert Taylor",   Initials = "RT", Email = "robert@techfixpro.com",  Phone = "0926-012-3456", ShopName = "TechFix Pro",  Role = UserRole.Auditor,    RoleName = "Auditor",       IsActive = true,  LastLoginAt = DateTime.Now.AddDays(-3),     CreatedAt = DateTime.Now.AddMonths(-2) },
    };

    private static List<ShopDropdownItem> GetShopDropdown() => new()
    {
        new() { Id = 1, Name = "TechFix Pro" },
        new() { Id = 2, Name = "QuickRepairs" },
        new() { Id = 3, Name = "ComputerMD" },
        new() { Id = 4, Name = "OldTech Solutions" },
        new() { Id = 5, Name = "GadgetCare PH" },
    };

    private GlobalUserFormViewModel GetEditModel(long id) => new()
    {
        Id = id, FirstName = "Emily", MiddleName = "Grace", LastName = "Brown",
        Email = "emily@techfixpro.com", Phone = "0918-234-5678",
        ShopId = 1, Role = UserRole.Billing, IsActive = true,
        AvailableShops = GetShopDropdown()
    };

    private GlobalUserDetailViewModel GetDetailModel(long id) => new()
    {
        Id = id, FullName = "Emily Brown", Initials = "EB",
        Email = "emily@techfixpro.com", Phone = "0918-234-5678",
        ShopName = "TechFix Pro", Role = UserRole.Billing, RoleName = "Billing Staff",
        IsActive = true, CreatedAt = DateTime.Now.AddMonths(-5),
        LastLoginAt = DateTime.Now.AddHours(-1), LastIpAddress = "192.168.1.15",
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
