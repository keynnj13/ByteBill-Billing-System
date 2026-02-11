using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class UsersController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        if (roleClaim != UserRole.SuperAdmin.ToString())
        {
            return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        }

        // Mock users data
        var users = new[]
        {
            new { Id = 1, Name = "John Anderson", Email = "john@techfixpro.com", Shop = "TechFix Pro", Role = "Admin", Status = "Active", LastLogin = DateTime.Now.AddHours(-2) },
            new { Id = 2, Name = "Sarah Miller", Email = "sarah@quickrepairs.com", Shop = "QuickRepairs", Role = "Admin", Status = "Active", LastLogin = DateTime.Now.AddDays(-1) },
            new { Id = 3, Name = "Mike Chen", Email = "mike@computermd.com", Shop = "ComputerMD", Role = "Admin", Status = "Active", LastLogin = DateTime.Now.AddHours(-5) },
            new { Id = 4, Name = "Emily Brown", Email = "emily@techfixpro.com", Shop = "TechFix Pro", Role = "Billing", Status = "Active", LastLogin = DateTime.Now.AddHours(-1) },
            new { Id = 5, Name = "David Lee", Email = "david@computermd.com", Shop = "ComputerMD", Role = "Technician", Status = "Active", LastLogin = DateTime.Now.AddMinutes(-30) }
        };
        
        ViewBag.Users = users;
        return View();
    }
}
