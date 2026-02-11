using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.JobOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class JobOrdersController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Billing.ToString();
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
                new() { Id = 1, OrderNumber = "JO-2024-0156", CustomerName = "Mike Johnson", CustomerInitials = "MJ", DeviceType = "MacBook Pro 2021", Status = JobOrderStatus.Completed, Priority = "High", CreatedAt = DateTime.Now.AddDays(-2), AssignedTechnicianName = "David Lee" },
                new() { Id = 2, OrderNumber = "JO-2024-0155", CustomerName = "Sarah Chen", CustomerInitials = "SC", DeviceType = "Dell XPS 15", Status = JobOrderStatus.Completed, Priority = "Normal", CreatedAt = DateTime.Now.AddDays(-3), AssignedTechnicianName = "Emily Chen" },
                new() { Id = 3, OrderNumber = "JO-2024-0154", CustomerName = "Bob Martinez", CustomerInitials = "BM", DeviceType = "iPhone 14 Pro", Status = JobOrderStatus.ReadyForPickup, Priority = "Normal", CreatedAt = DateTime.Now.AddDays(-4), AssignedTechnicianName = "David Lee" },
                new() { Id = 4, OrderNumber = "JO-2024-0153", CustomerName = "Alice Thompson", CustomerInitials = "AT", DeviceType = "HP Pavilion", Status = JobOrderStatus.InProgress, Priority = "Low", CreatedAt = DateTime.Now.AddDays(-5), AssignedTechnicianName = "Emily Chen" }
            }
        };
        
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        var model = new JobOrderDetailViewModel
        {
            Id = id,
            OrderNumber = "JO-2024-0156",
            CustomerId = 1,
            CustomerName = "Mike Johnson",
            CustomerEmail = "mike@email.com",
            CustomerPhone = "(555) 123-4567",
            DeviceType = "MacBook Pro 2021",
            DeviceSerial = "C02X1234HKGG",
            DeviceAccessories = "Charger, Laptop Bag",
            IssueDescription = "Screen flickering and random shutdowns. Customer reports issue started after macOS update.",
            Status = JobOrderStatus.Completed,
            Priority = "High",
            CreatedAt = DateTime.Now.AddDays(-2),
            AssignedTechnicianId = 3,
            AssignedTechnicianName = "David Lee",
            EstimatedCompletionDate = DateTime.Now.AddDays(-1),
            CompletedAt = DateTime.Now.AddHours(-6),
            TechnicianNotes = "Diagnosed as display cable issue. Replaced cable and updated graphics drivers. Ran stress test for 2 hours - no issues.",
            Timeline = new List<TimelineEventViewModel>
            {
                new() { Status = "Checked In", Description = "Device received from customer", Timestamp = DateTime.Now.AddDays(-2), CompletedBy = "Emily Brown" },
                new() { Status = "Assigned", Description = "Assigned to David Lee", Timestamp = DateTime.Now.AddDays(-2).AddHours(1), CompletedBy = "John Anderson" },
                new() { Status = "Diagnosis", Description = "Display cable issue identified", Timestamp = DateTime.Now.AddDays(-2).AddHours(3), CompletedBy = "David Lee" },
                new() { Status = "In Progress", Description = "Repair started", Timestamp = DateTime.Now.AddDays(-1), CompletedBy = "David Lee" },
                new() { Status = "Completed", Description = "Repair completed and tested", Timestamp = DateTime.Now.AddHours(-6), CompletedBy = "David Lee" }
            },
            LineItems = new List<JobOrderDetailViewModel.LineItem>
            {
                new() { Description = "Display Cable Replacement", Type = "Service", Quantity = 1, UnitPrice = 120.00m, Total = 120.00m },
                new() { Description = "Display Cable (MacBook Pro 2021)", Type = "Part", Quantity = 1, UnitPrice = 85.00m, Total = 85.00m },
                new() { Description = "System Diagnosis", Type = "Service", Quantity = 1, UnitPrice = 50.00m, Total = 50.00m }
            },
            Subtotal = 255.00m,
            TaxRate = 8.25m,
            TaxAmount = 21.04m,
            Total = 276.04m,
            InvoiceId = null
        };
        
        return View(model);
    }
}
