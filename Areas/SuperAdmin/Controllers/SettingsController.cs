using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class SettingsController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.SuperAdmin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string tab = "general")
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        // In production, load from database/config store
        var model = new SystemSettingsViewModel();
        ViewBag.ActiveTab = tab;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveGeneral(SystemSettingsViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        // Validate only general fields
        ModelState.Remove("SmtpHost");
        ModelState.Remove("SmtpUsername");
        ModelState.Remove("SmtpPassword");
        ModelState.Remove("FromEmail");
        ModelState.Remove("FromName");

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            // In production, save to database/config store
            return Json(new { success = true, message = "General settings saved successfully." });
        }

        TempData["SuccessMessage"] = "General settings saved successfully.";
        return RedirectToAction("Index", new { tab = "general" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveSecurity(SystemSettingsViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, message = "Security settings saved successfully." });
        }

        TempData["SuccessMessage"] = "Security settings saved successfully.";
        return RedirectToAction("Index", new { tab = "security" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SaveEmail(SystemSettingsViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, message = "Email settings saved successfully." });
        }

        TempData["SuccessMessage"] = "Email settings saved successfully.";
        return RedirectToAction("Index", new { tab = "email" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult TestEmail(string testEmailAddress)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            // In production, send a test email via SMTP
            return Json(new { success = true, message = $"Test email sent to {testEmailAddress}." });
        }

        TempData["SuccessMessage"] = $"Test email sent to {testEmailAddress}.";
        return RedirectToAction("Index", new { tab = "email" });
    }
}
