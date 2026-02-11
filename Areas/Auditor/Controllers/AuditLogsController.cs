using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Auditor.Controllers;

[Area("Auditor")]
[Authorize]
public class AuditLogsController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Auditor.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? action, string? user, DateTime? startDate, DateTime? endDate, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var logs = new[]
        {
            new { Id = 1, Action = "Invoice Created", Entity = "Invoice", EntityId = "INV-2024-0143", User = "Emily Brown", Role = "Billing", IpAddress = "192.168.1.105", Timestamp = DateTime.Now.AddMinutes(-30), Details = "Created invoice for Sarah Chen - $320.00" },
            new { Id = 2, Action = "Payment Received", Entity = "Payment", EntityId = "PAY-2024-0142", User = "Emily Brown", Role = "Billing", IpAddress = "192.168.1.105", Timestamp = DateTime.Now.AddMinutes(-5), Details = "Recorded payment $450.00 via Credit Card for INV-2024-0142" },
            new { Id = 3, Action = "Invoice Voided", Entity = "Invoice", EntityId = "INV-2024-0138", User = "John Anderson", Role = "Admin", IpAddress = "192.168.1.100", Timestamp = DateTime.Now.AddHours(-1), Details = "Voided invoice - Reason: Customer dispute" },
            new { Id = 4, Action = "Job Order Updated", Entity = "JobOrder", EntityId = "JO-2024-0156", User = "David Lee", Role = "Technician", IpAddress = "192.168.1.110", Timestamp = DateTime.Now.AddHours(-2), Details = "Status changed from InProgress to Completed" },
            new { Id = 5, Action = "Discount Applied", Entity = "Invoice", EntityId = "INV-2024-0140", User = "Emily Brown", Role = "Billing", IpAddress = "192.168.1.105", Timestamp = DateTime.Now.AddDays(-1), Details = "Applied $50.00 discount - loyal customer" },
            new { Id = 6, Action = "User Login", Entity = "Auth", EntityId = "-", User = "John Anderson", Role = "Admin", IpAddress = "192.168.1.100", Timestamp = DateTime.Now.AddDays(-1), Details = "Successful login" },
            new { Id = 7, Action = "Customer Created", Entity = "Customer", EntityId = "CUST-0157", User = "Emily Brown", Role = "Billing", IpAddress = "192.168.1.105", Timestamp = DateTime.Now.AddDays(-2), Details = "Created customer: Sarah Chen" }
        };
        
        ViewBag.Logs = logs;
        ViewBag.ActionFilter = action;
        ViewBag.UserFilter = user;
        ViewBag.StartDate = startDate;
        ViewBag.EndDate = endDate;
        ViewBag.CurrentPage = page;
        ViewBag.TotalCount = 1250;
        
        var actions = new[] { "Invoice Created", "Invoice Voided", "Payment Received", "Payment Voided", "Job Order Created", "Job Order Updated", "Customer Created", "Customer Updated", "User Login", "User Logout", "Discount Applied", "Refund Processed" };
        var users = new[] { "John Anderson", "Emily Brown", "David Lee", "Emily Chen", "Robert Taylor" };
        
        ViewBag.Actions = actions;
        ViewBag.Users = users;
        
        return View();
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        
        ViewBag.Log = new
        {
            Id = id,
            Action = "Invoice Voided",
            Entity = "Invoice",
            EntityId = "INV-2024-0138",
            User = "John Anderson",
            Role = "Admin",
            IpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0",
            Timestamp = DateTime.Now.AddHours(-1),
            Details = "Voided invoice - Reason: Customer dispute",
            OldValues = "{ \"Status\": \"Sent\", \"Balance\": 150.00 }",
            NewValues = "{ \"Status\": \"Void\", \"Balance\": 0.00, \"VoidReason\": \"Customer dispute\" }"
        };
        
        return View();
    }
}
