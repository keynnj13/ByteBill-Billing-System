using System.Diagnostics;
using ByteBill_BS.Models;
using ByteBill_BS.Models.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            // Redirect authenticated users to their role-specific dashboard
            if (User.Identity?.IsAuthenticated ?? false)
            {
                return RedirectToRoleDashboard();
            }
            
            // Redirect unauthenticated users to landing page
            return RedirectToAction("Index", "Landing");
        }

        private IActionResult RedirectToRoleDashboard()
        {
            var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
            var userRole = Enum.TryParse<UserRole>(roleClaim, out var role) ? role : UserRole.Billing;
            
            return userRole switch
            {
                UserRole.SuperAdmin => RedirectToAction("Index", "Dashboard", new { area = "SuperAdmin" }),
                UserRole.Admin => RedirectToAction("Index", "Dashboard", new { area = "Admin" }),
                UserRole.Billing => RedirectToAction("Index", "Dashboard", new { area = "Billing" }),
                UserRole.Technician => RedirectToAction("Index", "Dashboard", new { area = "Technician" }),
                UserRole.Auditor => RedirectToAction("Index", "Dashboard", new { area = "Auditor" }),
                _ => RedirectToAction("Login", "Auth")
            };
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
