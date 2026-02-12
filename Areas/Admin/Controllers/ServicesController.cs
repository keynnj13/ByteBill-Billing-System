using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class ServicesController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, string? category, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = new ServiceListViewModel
        {
            SearchTerm = search,
            CategoryFilter = category,
            CurrentPage = page,
            TotalCount = 24,
            Categories = new List<string> { "Diagnosis", "Repair", "Installation", "Maintenance", "Data Recovery" },
            Services = new List<ServiceItemViewModel>
            {
                new() { Id = 1, Name = "System Diagnosis", Description = "Full hardware and software diagnostic assessment", Category = "Diagnosis", Price = 50.00m, EstimatedDuration = "30 min", IsActive = true },
                new() { Id = 2, Name = "Virus/Malware Removal", Description = "Complete malware scan and removal with system cleanup", Category = "Repair", Price = 75.00m, EstimatedDuration = "1h 30m", IsActive = true },
                new() { Id = 3, Name = "Screen Replacement", Description = "Laptop or phone screen replacement service", Category = "Repair", Price = 120.00m, EstimatedDuration = "1h", IsActive = true },
                new() { Id = 4, Name = "OS Installation", Description = "Clean installation of Windows, macOS, or Linux", Category = "Installation", Price = 80.00m, EstimatedDuration = "2h", IsActive = true },
                new() { Id = 5, Name = "Data Recovery", Description = "Recovery of data from damaged or corrupted drives", Category = "Data Recovery", Price = 150.00m, EstimatedDuration = "4h", IsActive = true },
                new() { Id = 6, Name = "Hardware Cleanup", Description = "Internal cleaning and thermal paste replacement", Category = "Maintenance", Price = 45.00m, EstimatedDuration = "45 min", IsActive = true }
            }
        };
        
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        return View(new ServiceFormViewModel
        {
            ExistingCategories = new List<string> { "Diagnosis", "Repair", "Installation", "Maintenance", "Data Recovery" }
        });
    }

    [HttpGet]
    public IActionResult CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        
        return PartialView("_CreateModal", new ServiceFormViewModel
        {
            ExistingCategories = new List<string> { "Diagnosis", "Repair", "Installation", "Maintenance", "Data Recovery" }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ServiceFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid)
        {
            model.ExistingCategories = new List<string> { "Diagnosis", "Repair", "Installation", "Maintenance", "Data Recovery" };
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }
        
        TempData["Success"] = "Service created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Service created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = GetServiceEditModel(id);
        return View(model);
    }

    [HttpGet]
    public IActionResult EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = GetServiceEditModel(id);
        return PartialView("_EditModal", model);
    }

    private ServiceFormViewModel GetServiceEditModel(long id)
    {
        return new ServiceFormViewModel
        {
            Id = id,
            Name = "Virus/Malware Removal",
            Description = "Complete malware scan and removal with system cleanup",
            Category = "Repair",
            Price = 75.00m,
            EstimatedDuration = 90,
            IsActive = true,
            ExistingCategories = new List<string> { "Diagnosis", "Repair", "Installation", "Maintenance", "Data Recovery" }
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(ServiceFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid)
        {
            model.ExistingCategories = new List<string> { "Diagnosis", "Repair", "Installation", "Maintenance", "Data Recovery" };
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }
        
        TempData["Success"] = "Service updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Service updated successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = GetServiceDetail(id);
        return View(model);
    }

    [HttpGet]
    public IActionResult DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = GetServiceDetail(id);
        return PartialView("_DetailsModal", model);
    }

    private ServiceDetailViewModel GetServiceDetail(long id)
    {
        return new ServiceDetailViewModel
        {
            Id = id,
            Name = "Virus/Malware Removal",
            Description = "Complete malware scan and removal with system cleanup",
            Category = "Repair",
            Price = 75.00m,
            IsActive = true,
            UsageCount = 34,
            TotalRevenue = 2550.00m,
            AverageRating = 4.7m
        };
    }
}
