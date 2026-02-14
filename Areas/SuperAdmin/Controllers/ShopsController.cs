using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class ShopsController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.SuperAdmin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, string? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var allShops = GetDemoShops();

        if (!string.IsNullOrWhiteSpace(search))
            allShops = allShops
                .Where(s => s.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || s.Owner.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || s.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (!string.IsNullOrEmpty(status))
            allShops = allShops.Where(s => s.Status == status).ToList();

        var viewModel = new ShopListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = allShops.Count,
            ActiveCount = allShops.Count(s => s.Status == "Active"),
            SuspendedCount = allShops.Count(s => s.Status == "Suspended"),
            NewThisMonth = allShops.Count(s => s.CreatedAt >= DateTime.Now.AddDays(-30)),
            Shops = allShops.Skip((page - 1) * 10).Take(10).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_CreateModal", new ShopFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ShopFormViewModel model)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Shop created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Shop created successfully!" });
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
    public IActionResult Edit(ShopFormViewModel model)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Shop updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Shop updated successfully!" });
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
            return Json(new { success = true, message = "Shop status updated!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(long id)
    {
        if (!IsAuthorized()) return Forbid();
        TempData["Success"] = "Shop deleted successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Shop deleted successfully!" });
        return RedirectToAction(nameof(Index));
    }

    // ── Demo Data ──────────────────────────────────────────────
    private static List<ShopItemViewModel> GetDemoShops() => new()
    {
        new() { Id = 1, Name = "TechFix Pro",       Initials = "TP", Owner = "John Anderson",  Email = "john@techfixpro.com",    Phone = "0917-123-4567", UserCount = 5, JobOrderCount = 245, Status = "Active",    CreatedAt = DateTime.Now.AddMonths(-6) },
        new() { Id = 2, Name = "QuickRepairs",      Initials = "QR", Owner = "Sarah Miller",   Email = "sarah@quickrepairs.com", Phone = "0918-234-5678", UserCount = 3, JobOrderCount = 189, Status = "Active",    CreatedAt = DateTime.Now.AddMonths(-4) },
        new() { Id = 3, Name = "ComputerMD",        Initials = "CM", Owner = "Mike Chen",      Email = "mike@computermd.com",    Phone = "0919-345-6789", UserCount = 8, JobOrderCount = 512, Status = "Active",    CreatedAt = DateTime.Now.AddYears(-1) },
        new() { Id = 4, Name = "OldTech Solutions",  Initials = "OT", Owner = "Bob Wilson",    Email = "bob@oldtech.com",         Phone = "0920-456-7890", UserCount = 2, JobOrderCount = 45,  Status = "Suspended", CreatedAt = DateTime.Now.AddMonths(-8) },
        new() { Id = 5, Name = "GadgetCare PH",     Initials = "GC", Owner = "Ana Reyes",     Email = "ana@gadgetcareph.com",    Phone = "0921-567-8901", UserCount = 4, JobOrderCount = 167, Status = "Active",    CreatedAt = DateTime.Now.AddDays(-15) },
    };

    private ShopFormViewModel GetEditModel(long id) => new()
    {
        Id = id, Name = "TechFix Pro", Owner = "John Anderson",
        Email = "john@techfixpro.com", Phone = "0917-123-4567",
        Address = "123 Rizal Avenue, Makati City, Metro Manila",
        Status = "Active", Notes = "Premium partner shop"
    };

    private ShopDetailViewModel GetDetailModel(long id) => new()
    {
        Id = id, Name = "TechFix Pro", Initials = "TP",
        Owner = "John Anderson", Email = "john@techfixpro.com",
        Phone = "0917-123-4567", Address = "123 Rizal Avenue, Makati City, Metro Manila",
        Status = "Active", CreatedAt = DateTime.Now.AddMonths(-6),
        Notes = "Premium partner shop",
        UserCount = 5, JobOrderCount = 245, TotalRevenue = 156780.00m, ActiveJobOrders = 12,
        RecentUsers = new()
        {
            new() { Id = 1, FullName = "John Anderson",   Email = "john@techfixpro.com",   RoleName = "Admin",         RoleClass = "badge-primary", IsActive = true },
            new() { Id = 2, FullName = "Emily Brown",     Email = "emily@techfixpro.com",  RoleName = "Billing Staff", RoleClass = "badge-success", IsActive = true },
            new() { Id = 3, FullName = "David Lee",       Email = "david@techfixpro.com",  RoleName = "Technician",    RoleClass = "badge-info",    IsActive = true },
            new() { Id = 7, FullName = "James Rodriguez", Email = "james@techfixpro.com",  RoleName = "Technician",    RoleClass = "badge-info",    IsActive = true },
            new() { Id = 5, FullName = "Robert Taylor",   Email = "robert@techfixpro.com", RoleName = "Auditor",       RoleClass = "badge-warning", IsActive = true },
        }
    };
}
