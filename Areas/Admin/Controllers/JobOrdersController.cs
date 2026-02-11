using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.JobOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class JobOrdersController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, JobOrderStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = new JobOrderListViewModel
        {
            SearchTerm = search,
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = 89,
            JobOrders = new List<JobOrderItemViewModel>
            {
                new() { Id = 1, JobNumber = "JO-2024-0089", CustomerName = "Alice Thompson", CustomerInitials = "AT", DeviceType = "Laptop", DeviceBrand = "Dell", DeviceModel = "XPS 15", Status = JobOrderStatus.Pending, TechnicianName = null, EstimatedCost = 250.00m, CreatedAt = DateTime.Now.AddMinutes(-20) },
                new() { Id = 2, JobNumber = "JO-2024-0088", CustomerName = "Bob Martinez", CustomerInitials = "BM", DeviceType = "Desktop", DeviceBrand = "HP", DeviceModel = "Pavilion", Status = JobOrderStatus.InProgress, TechnicianName = "David Lee", EstimatedCost = 180.00m, CreatedAt = DateTime.Now.AddHours(-2), DueDate = DateTime.Now.AddDays(2) },
                new() { Id = 3, JobNumber = "JO-2024-0087", CustomerName = "Carol White", CustomerInitials = "CW", DeviceType = "Phone", DeviceBrand = "Apple", DeviceModel = "iPhone 14", Status = JobOrderStatus.AwaitingApproval, TechnicianName = "David Lee", EstimatedCost = 350.00m, CreatedAt = DateTime.Now.AddHours(-4) },
                new() { Id = 4, JobNumber = "JO-2024-0086", CustomerName = "Dan Brown", CustomerInitials = "DB", DeviceType = "Tablet", DeviceBrand = "Samsung", DeviceModel = "Galaxy Tab S8", Status = JobOrderStatus.Diagnosed, TechnicianName = "Emily Chen", EstimatedCost = 120.00m, CreatedAt = DateTime.Now.AddHours(-6) },
                new() { Id = 5, JobNumber = "JO-2024-0085", CustomerName = "Frank Wilson", CustomerInitials = "FW", DeviceType = "Desktop", DeviceBrand = "Custom", DeviceModel = "Gaming PC", Status = JobOrderStatus.Completed, TechnicianName = "David Lee", EstimatedCost = 95.00m, CreatedAt = DateTime.Now.AddDays(-1) }
            }
        };
        
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = GetJobOrderCreateModel();
        return View(model);
    }

    [HttpGet]
    public IActionResult CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = GetJobOrderCreateModel();
        return PartialView("_CreateModal", model);
    }

    private JobOrderCreateViewModel GetJobOrderCreateModel()
    {
        return new JobOrderCreateViewModel
        {
            CurrentStep = 1,
            AvailableCustomers = new List<CustomerSelectItem>
            {
                new() { Id = 1, FullName = "Alice Thompson", Phone = "(555) 111-2222" },
                new() { Id = 2, FullName = "Bob Martinez", Phone = "(555) 222-3333" },
                new() { Id = 3, FullName = "Carol White", Phone = "(555) 333-4444" },
                new() { Id = 4, FullName = "Dan Brown", Phone = "(555) 444-5555" }
            },
            AvailableTechnicians = new List<TechnicianSelectItem>
            {
                new() { Id = 1, FullName = "David Lee", ActiveJobOrders = 3 },
                new() { Id = 2, FullName = "Emily Chen", ActiveJobOrders = 2 }
            }
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(JobOrderCreateViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid)
        {
            // Re-populate dropdowns
            model.AvailableCustomers = new List<CustomerSelectItem>
            {
                new() { Id = 1, FullName = "Alice Thompson", Phone = "(555) 111-2222" },
                new() { Id = 2, FullName = "Bob Martinez", Phone = "(555) 222-3333" }
            };
            model.AvailableTechnicians = new List<TechnicianSelectItem>
            {
                new() { Id = 1, FullName = "David Lee", ActiveJobOrders = 3 },
                new() { Id = 2, FullName = "Emily Chen", ActiveJobOrders = 2 }
            };
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_CreateModal", model);
            return View(model);
        }
        
        TempData["Success"] = "Job order created successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Job order created successfully!" });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = GetJobOrderDetail(id);
        return View(model);
    }

    [HttpGet]
    public IActionResult DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = GetJobOrderDetail(id);
        return PartialView("_DetailsModal", model);
    }

    private JobOrderDetailViewModel GetJobOrderDetail(long id)
    {
        return new JobOrderDetailViewModel
        {
            Id = id,
            JobNumber = "JO-2024-0088",
            CustomerId = 2,
            CustomerName = "Bob Martinez",
            CustomerInitials = "BM",
            CustomerPhone = "(555) 222-3333",
            CustomerEmail = "bob@email.com",
            DeviceType = "Desktop",
            DeviceBrand = "HP",
            DeviceModel = "Pavilion",
            SerialNumber = "HP-2024-XYZ123",
            Status = JobOrderStatus.InProgress,
            TechnicianId = 1,
            TechnicianName = "David Lee",
            ProblemDescription = "Computer running slow, takes 10+ minutes to boot. Customer reports frequent freezing and blue screens.",
            DiagnosisNotes = "Found malware infection and failing HDD. Recommended SSD upgrade and full system cleanup.",
            EstimatedCost = 180.00m,
            FinalCost = 0,
            CreatedAt = DateTime.Now.AddHours(-2),
            DiagnosedAt = DateTime.Now.AddHours(-1),
            DueDate = DateTime.Now.AddDays(2),
            Items = new List<JobOrderItemLineViewModel>
            {
                new() { Id = 1, Description = "System Diagnosis", Quantity = 1, UnitPrice = 50.00m, Total = 50.00m, IsService = true },
                new() { Id = 2, Description = "Malware Removal", Quantity = 1, UnitPrice = 75.00m, Total = 75.00m, IsService = true },
                new() { Id = 3, Description = "500GB SSD", Quantity = 1, UnitPrice = 65.00m, Total = 65.00m, IsService = false }
            },
            Timeline = new List<TimelineEventViewModel>
            {
                new() { Title = "Job Order Created", Description = "Created by Emily Brown", Timestamp = DateTime.Now.AddHours(-2), Icon = "plus", IsCompleted = true },
                new() { Title = "Assigned to Technician", Description = "Assigned to David Lee", Timestamp = DateTime.Now.AddHours(-1).AddMinutes(-45), Icon = "user", IsCompleted = true },
                new() { Title = "Diagnosis Completed", Description = "Malware and HDD issues identified", Timestamp = DateTime.Now.AddHours(-1), Icon = "search", IsCompleted = true },
                new() { Title = "Repair In Progress", Description = "Currently being worked on", Timestamp = DateTime.Now.AddMinutes(-30), Icon = "wrench", IsCompleted = false }
            }
        };
    }

    [HttpGet]
    public IActionResult Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = GetJobOrderEditModel(id);
        ViewBag.JobOrderNumber = "JO-2024-0088";
        return View(model);
    }

    [HttpGet]
    public IActionResult EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        
        var model = GetJobOrderEditModel(id);
        return PartialView("_EditModal", model);
    }

    private JobOrderCreateViewModel GetJobOrderEditModel(long id)
    {
        return new JobOrderCreateViewModel
        {
            Id = id,
            CustomerId = 2,
            DeviceType = "Desktop",
            Brand = "HP",
            Model = "Pavilion",
            SerialNumber = "HP-2024-XYZ123",
            ProblemDescription = "Computer running slow, takes 10+ minutes to boot. Customer reports frequent freezing and blue screens.",
            Priority = "Normal",
            AssignedTechnicianId = 1,
            EstimatedCompletionDate = DateTime.Now.AddDays(2),
            AvailableCustomers = new List<CustomerSelectItem>
            {
                new() { Id = 1, FullName = "Alice Thompson", Phone = "(555) 111-2222" },
                new() { Id = 2, FullName = "Bob Martinez", Phone = "(555) 222-3333" },
                new() { Id = 3, FullName = "Carol White", Phone = "(555) 333-4444" },
                new() { Id = 4, FullName = "Dan Brown", Phone = "(555) 444-5555" }
            },
            AvailableTechnicians = new List<TechnicianSelectItem>
            {
                new() { Id = 1, FullName = "David Lee", ActiveJobOrders = 3 },
                new() { Id = 2, FullName = "Emily Chen", ActiveJobOrders = 2 }
            }
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(JobOrderCreateViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        if (!ModelState.IsValid)
        {
            model.AvailableCustomers = new List<CustomerSelectItem>
            {
                new() { Id = 1, FullName = "Alice Thompson", Phone = "(555) 111-2222" },
                new() { Id = 2, FullName = "Bob Martinez", Phone = "(555) 222-3333" }
            };
            model.AvailableTechnicians = new List<TechnicianSelectItem>
            {
                new() { Id = 1, FullName = "David Lee", ActiveJobOrders = 3 },
                new() { Id = 2, FullName = "Emily Chen", ActiveJobOrders = 2 }
            };
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditModal", model);
            ViewBag.JobOrderNumber = "JO-2024-0088";
            return View(model);
        }
        
        TempData["Success"] = "Job order updated successfully!";
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = "Job order updated successfully!" });
        return RedirectToAction(nameof(Index));
    }
}
