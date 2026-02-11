using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class CustomersController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = new CustomerListViewModel
        {
            SearchTerm = search,
            CurrentPage = page,
            TotalCount = 48,
            Customers = new List<CustomerItemViewModel>
            {
                new() { Id = 1, FullName = "Alice Thompson", Initials = "AT", Email = "alice@email.com", Phone = "(555) 111-2222", TotalJobOrders = 12, TotalSpent = 2450.00m, CreatedAt = DateTime.Now.AddMonths(-8), IsActive = true },
                new() { Id = 2, FullName = "Bob Martinez", Initials = "BM", Email = "bob@email.com", Phone = "(555) 222-3333", TotalJobOrders = 8, TotalSpent = 1890.00m, CreatedAt = DateTime.Now.AddMonths(-5), IsActive = true },
                new() { Id = 3, FullName = "Carol White", Initials = "CW", Email = "carol@email.com", Phone = "(555) 333-4444", TotalJobOrders = 5, TotalSpent = 980.00m, CreatedAt = DateTime.Now.AddMonths(-3), IsActive = true },
                new() { Id = 4, FullName = "Dan Brown", Initials = "DB", Email = "dan@email.com", Phone = "(555) 444-5555", TotalJobOrders = 3, TotalSpent = 450.00m, CreatedAt = DateTime.Now.AddMonths(-1), IsActive = true },
                new() { Id = 5, FullName = "Emily Chen", Initials = "EC", Email = "emily@email.com", Phone = "(555) 555-6666", TotalJobOrders = 15, TotalSpent = 3200.00m, CreatedAt = DateTime.Now.AddYears(-1), IsActive = true }
            }
        };
        
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        return View(new CustomerFormViewModel());
    }

    [HttpGet]
    public IActionResult CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_CreateModal", new CustomerFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CustomerFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }
        
        TempData["Success"] = "Customer created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Customer created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = new CustomerFormViewModel
        {
            Id = id,
            FirstName = "Alice",
            LastName = "Thompson",
            Email = "alice@email.com",
            Phone = "(555) 111-2222",
            Address = "123 Main St, Anytown, USA 12345",
            Notes = "Preferred customer - 10% discount",
            IsActive = true
        };
        
        return View(model);
    }

    [HttpGet]
    public IActionResult EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = new CustomerFormViewModel
        {
            Id = id,
            FirstName = "Alice",
            LastName = "Thompson",
            Email = "alice@email.com",
            Phone = "(555) 111-2222",
            Address = "123 Main St, Anytown, USA 12345",
            Notes = "Preferred customer - 10% discount",
            IsActive = true
        };
        
        return PartialView("_EditModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(CustomerFormViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            return View(model);
        }
        
        TempData["Success"] = "Customer updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Customer updated successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = GetCustomerDetail(id);
        return View(model);
    }

    [HttpGet]
    public IActionResult DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = GetCustomerDetail(id);
        return PartialView("_DetailsModal", model);
    }

    private CustomerDetailViewModel GetCustomerDetail(long id)
    {
        return new CustomerDetailViewModel
        {
            Id = id,
            FullName = "Alice Thompson",
            Name = "Alice Thompson",
            Initials = "AT",
            Email = "alice@email.com",
            Phone = "(555) 111-2222",
            Address = "123 Main St, Anytown, USA 12345",
            Notes = "Preferred customer - 10% discount",
            IsActive = true,
            CreatedAt = DateTime.Now.AddMonths(-8),
            TotalJobOrders = 12,
            TotalSpent = 2450.00m,
            OutstandingBalance = 180.00m,
            RecentJobOrders = new List<CustomerJobOrderViewModel>
            {
                new() { Id = 1, JobNumber = "JO-2024-0089", DeviceType = "Laptop", Status = "Pending", StatusClass = "badge-pending", Total = 0, CreatedAt = DateTime.Now.AddDays(-1) },
                new() { Id = 2, JobNumber = "JO-2024-0075", DeviceType = "Desktop", Status = "Completed", StatusClass = "badge-completed", Total = 350.00m, CreatedAt = DateTime.Now.AddDays(-15) },
                new() { Id = 3, JobNumber = "JO-2024-0062", DeviceType = "Phone", Status = "Completed", StatusClass = "badge-completed", Total = 180.00m, CreatedAt = DateTime.Now.AddDays(-30) }
            }
        };
    }
}
