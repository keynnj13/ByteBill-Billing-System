using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class UsersController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var users = new[]
        {
            new { Id = 1, Name = "John Anderson", Email = "john@techfixpro.com", Role = "Admin", Status = "Active", LastLogin = DateTime.Now.AddHours(-2) },
            new { Id = 2, Name = "Emily Brown", Email = "emily@techfixpro.com", Role = "Billing", Status = "Active", LastLogin = DateTime.Now.AddHours(-1) },
            new { Id = 3, Name = "David Lee", Email = "david@techfixpro.com", Role = "Technician", Status = "Active", LastLogin = DateTime.Now.AddMinutes(-30) },
            new { Id = 4, Name = "Emily Chen", Email = "echen@techfixpro.com", Role = "Technician", Status = "Active", LastLogin = DateTime.Now.AddDays(-1) },
            new { Id = 5, Name = "Robert Taylor", Email = "robert@techfixpro.com", Role = "Auditor", Status = "Active", LastLogin = DateTime.Now.AddDays(-3) }
        };
        
        ViewBag.Users = users;
        return View();
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        return View();
    }

    [HttpGet]
    public IActionResult Edit(long id)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        ViewBag.UserId = id;
        return View();
    }
}
