using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class ShopsController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        if (roleClaim != UserRole.SuperAdmin.ToString())
        {
            return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        }

        // Mock shops data
        var shops = new[]
        {
            new { Id = 1, Name = "TechFix Pro", Owner = "John Anderson", Email = "john@techfixpro.com", Phone = "(555) 123-4567", Users = 5, JobOrders = 245, Status = "Active", CreatedAt = DateTime.Now.AddMonths(-6) },
            new { Id = 2, Name = "QuickRepairs", Owner = "Sarah Miller", Email = "sarah@quickrepairs.com", Phone = "(555) 234-5678", Users = 3, JobOrders = 189, Status = "Active", CreatedAt = DateTime.Now.AddMonths(-4) },
            new { Id = 3, Name = "ComputerMD", Owner = "Mike Chen", Email = "mike@computermd.com", Phone = "(555) 345-6789", Users = 8, JobOrders = 512, Status = "Active", CreatedAt = DateTime.Now.AddYears(-1) },
            new { Id = 4, Name = "OldTech Solutions", Owner = "Bob Wilson", Email = "bob@oldtech.com", Phone = "(555) 456-7890", Users = 2, JobOrders = 45, Status = "Suspended", CreatedAt = DateTime.Now.AddMonths(-8) }
        };
        
        ViewBag.Shops = shops;
        return View();
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Edit(long id)
    {
        ViewBag.ShopId = id;
        return View();
    }

    [HttpGet]
    public IActionResult Details(long id)
    {
        ViewBag.ShopId = id;
        return View();
    }
}
