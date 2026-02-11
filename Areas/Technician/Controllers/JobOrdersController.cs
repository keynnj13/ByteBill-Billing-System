using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.JobOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Technician.Controllers;

[Area("Technician")]
[Authorize]
public class JobOrdersController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Technician.ToString();
    }

    [HttpGet]
    public IActionResult Index(JobOrderStatus? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        // Technicians see only their assigned jobs
        var viewModel = new JobOrderListViewModel
        {
            StatusFilter = status,
            CurrentPage = page,
            TotalCount = 12,
            JobOrders = new List<JobOrderItemViewModel>
            {
                new() { Id = 157, OrderNumber = "JO-2024-0157", CustomerName = "Sarah Chen", CustomerInitials = "SC", DeviceType = "Dell XPS 15", Status = JobOrderStatus.CheckedIn, Priority = "High", CreatedAt = DateTime.Now.AddHours(-2), AssignedTechnicianName = "You" },
                new() { Id = 154, OrderNumber = "JO-2024-0154", CustomerName = "Bob Martinez", CustomerInitials = "BM", DeviceType = "iPhone 14 Pro", Status = JobOrderStatus.InProgress, Priority = "Normal", CreatedAt = DateTime.Now.AddDays(-1), AssignedTechnicianName = "You" },
                new() { Id = 153, OrderNumber = "JO-2024-0153", CustomerName = "Alice Thompson", CustomerInitials = "AT", DeviceType = "HP Pavilion", Status = JobOrderStatus.WaitingForParts, Priority = "Low", CreatedAt = DateTime.Now.AddDays(-2), AssignedTechnicianName = "You" },
                new() { Id = 156, OrderNumber = "JO-2024-0156", CustomerName = "Mike Johnson", CustomerInitials = "MJ", DeviceType = "MacBook Pro", Status = JobOrderStatus.Completed, Priority = "High", CreatedAt = DateTime.Now.AddDays(-2), AssignedTechnicianName = "You" }
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
            OrderNumber = "JO-2024-0157",
            CustomerId = 2,
            CustomerName = "Sarah Chen",
            CustomerEmail = "sarah@email.com",
            CustomerPhone = "(555) 234-5678",
            DeviceType = "Dell XPS 15 9520",
            DeviceSerial = "DL9520ABC123",
            DeviceAccessories = "Power adapter",
            IssueDescription = "Laptop overheating during normal use. Fan running constantly. Customer reports thermal throttling during video calls.",
            Status = JobOrderStatus.CheckedIn,
            Priority = "High",
            CreatedAt = DateTime.Now.AddHours(-2),
            AssignedTechnicianId = 3,
            AssignedTechnicianName = "David Lee",
            Timeline = new List<TimelineEventViewModel>
            {
                new() { Status = "Checked In", Description = "Device received from customer", Timestamp = DateTime.Now.AddHours(-2), CompletedBy = "Emily Brown" },
                new() { Status = "Assigned", Description = "Assigned to David Lee", Timestamp = DateTime.Now.AddHours(-1), CompletedBy = "John Anderson" }
            },
            LineItems = new List<JobOrderDetailViewModel.LineItem>(),
            Subtotal = 0,
            TaxRate = 8.25m,
            TaxAmount = 0,
            Total = 0,
            InvoiceId = null
        };
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdateStatus(long id, JobOrderStatus status, string? notes)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        TempData["Success"] = $"Job order status updated to {status}";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddNotes(long id, string notes)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        TempData["Success"] = "Notes added successfully";
        return RedirectToAction(nameof(Details), new { id });
    }
}
