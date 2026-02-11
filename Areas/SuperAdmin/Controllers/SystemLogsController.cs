using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class SystemLogsController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        if (roleClaim != UserRole.SuperAdmin.ToString())
        {
            return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        }

        // Mock system logs
        var logs = new[]
        {
            new { Id = 1, Type = "Info", Message = "User login successful", User = "john@techfixpro.com", IP = "192.168.1.100", Timestamp = DateTime.Now.AddMinutes(-5) },
            new { Id = 2, Type = "Warning", Message = "Failed login attempt", User = "unknown@test.com", IP = "10.0.0.50", Timestamp = DateTime.Now.AddMinutes(-15) },
            new { Id = 3, Type = "Info", Message = "Invoice created: INV-2024-0145", User = "emily@techfixpro.com", IP = "192.168.1.101", Timestamp = DateTime.Now.AddMinutes(-30) },
            new { Id = 4, Type = "Error", Message = "Payment gateway timeout", User = "System", IP = "N/A", Timestamp = DateTime.Now.AddHours(-1) },
            new { Id = 5, Type = "Info", Message = "Shop settings updated", User = "mike@computermd.com", IP = "192.168.1.105", Timestamp = DateTime.Now.AddHours(-2) }
        };
        
        ViewBag.Logs = logs;
        return View();
    }
}
