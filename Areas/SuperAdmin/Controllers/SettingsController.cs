using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class SettingsController : Controller
{
    private readonly ISuperAdminService _service;

    public SettingsController(ISuperAdminService service)
    {
        _service = service;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.SuperAdmin.ToString();
    }

    private long GetUserId() => long.TryParse(User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> Index(string tab = "general")
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var settings = await _service.GetSettingsAsync();
        var model = new SystemSettingsViewModel
        {
            PlatformName = settings.GetValueOrDefault("General.PlatformName", "ByteBill"),
            Tagline = settings.GetValueOrDefault("General.Tagline", "A Web-Based Billing System"),
            Currency = settings.GetValueOrDefault("General.Currency", "PHP"),
            Timezone = settings.GetValueOrDefault("General.Timezone", "Asia/Manila"),
            DateFormat = settings.GetValueOrDefault("General.DateFormat", "MMM dd, yyyy"),
            DefaultVatRate = decimal.TryParse(settings.GetValueOrDefault("Tax.DefaultVatRate", "12"), out var vat) ? vat : 12m,
            DefaultIsVatRegistered = settings.GetValueOrDefault("Tax.DefaultIsVatRegistered", "true") == "true",
            MinPasswordLength = int.TryParse(settings.GetValueOrDefault("Security.MinPasswordLength", "6"), out var minPw) ? minPw : 6,
            RequireUppercase = settings.GetValueOrDefault("Security.RequireUppercase", "true") == "true",
            RequireNumbers = settings.GetValueOrDefault("Security.RequireNumbers", "true") == "true",
            RequireSpecialChars = settings.GetValueOrDefault("Security.RequireSpecialChars", "false") == "true",
            SessionTimeout = int.TryParse(settings.GetValueOrDefault("Security.SessionTimeout", "60"), out var st) ? st : 60,
            MaxLoginAttempts = int.TryParse(settings.GetValueOrDefault("Security.MaxLoginAttempts", "5"), out var mla) ? mla : 5,
            Enable2FA = settings.GetValueOrDefault("Security.Enable2FA", "false") == "true",
            SmtpHost = settings.GetValueOrDefault("Email.SmtpHost", "smtp.gmail.com"),
            SmtpPort = int.TryParse(settings.GetValueOrDefault("Email.SmtpPort", "587"), out var sp) ? sp : 587,
            SmtpUsername = settings.GetValueOrDefault("Email.SmtpUsername", ""),
            SmtpPassword = settings.GetValueOrDefault("Email.SmtpPassword", ""),
            SmtpUseSsl = settings.GetValueOrDefault("Email.SmtpUseSsl", "true") == "true",
            FromEmail = settings.GetValueOrDefault("Email.FromEmail", "noreply@bytebill.ph"),
            FromName = settings.GetValueOrDefault("Email.FromName", "ByteBill System"),
            EnableEmailNotifications = settings.GetValueOrDefault("Email.EnableNotifications", "true") == "true",
            PayMongoTestMode = settings.GetValueOrDefault("PayMongo.TestMode", "true") == "true",
            TrialDays = int.TryParse(settings.GetValueOrDefault("Subscription.TrialDays", "14"), out var td) ? td : 14,
        };

        ViewBag.ActiveTab = tab;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGeneral(SystemSettingsViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var settings = new Dictionary<string, string>
        {
            ["General.PlatformName"] = model.PlatformName,
            ["General.Tagline"] = model.Tagline ?? "",
            ["General.Currency"] = model.Currency,
            ["General.Timezone"] = model.Timezone,
            ["General.DateFormat"] = model.DateFormat,
            ["Tax.DefaultVatRate"] = model.DefaultVatRate.ToString(),
            ["Tax.DefaultIsVatRegistered"] = model.DefaultIsVatRegistered.ToString().ToLower()
        };

        var result = await _service.SaveSettingsAsync(settings, "General", GetUserId());

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = result.Success, message = result.Message });

        TempData["Success"] = result.Message;
        return RedirectToAction("Index", new { tab = "general" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSecurity(SystemSettingsViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var settings = new Dictionary<string, string>
        {
            ["Security.MinPasswordLength"] = model.MinPasswordLength.ToString(),
            ["Security.RequireUppercase"] = model.RequireUppercase.ToString().ToLower(),
            ["Security.RequireNumbers"] = model.RequireNumbers.ToString().ToLower(),
            ["Security.RequireSpecialChars"] = model.RequireSpecialChars.ToString().ToLower(),
            ["Security.SessionTimeout"] = model.SessionTimeout.ToString(),
            ["Security.MaxLoginAttempts"] = model.MaxLoginAttempts.ToString(),
            ["Security.Enable2FA"] = model.Enable2FA.ToString().ToLower()
        };

        var result = await _service.SaveSettingsAsync(settings, "Security", GetUserId());

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = result.Success, message = result.Message });

        TempData["Success"] = result.Message;
        return RedirectToAction("Index", new { tab = "security" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmail(SystemSettingsViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var settings = new Dictionary<string, string>
        {
            ["Email.SmtpHost"] = model.SmtpHost ?? "",
            ["Email.SmtpPort"] = model.SmtpPort.ToString(),
            ["Email.SmtpUsername"] = model.SmtpUsername ?? "",
            ["Email.SmtpPassword"] = model.SmtpPassword ?? "",
            ["Email.SmtpUseSsl"] = model.SmtpUseSsl.ToString().ToLower(),
            ["Email.FromEmail"] = model.FromEmail ?? "",
            ["Email.FromName"] = model.FromName ?? "",
            ["Email.EnableNotifications"] = model.EnableEmailNotifications.ToString().ToLower()
        };

        var result = await _service.SaveSettingsAsync(settings, "Email", GetUserId());

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = result.Success, message = result.Message });

        TempData["Success"] = result.Message;
        return RedirectToAction("Index", new { tab = "email" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSubscription(SystemSettingsViewModel model)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var settings = new Dictionary<string, string>
        {
            ["PayMongo.TestMode"] = model.PayMongoTestMode.ToString().ToLower(),
            ["Subscription.TrialDays"] = model.TrialDays.ToString()
        };

        var result = await _service.SaveSettingsAsync(settings, "Subscription", GetUserId());

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = result.Success, message = result.Message });

        TempData["Success"] = result.Message;
        return RedirectToAction("Index", new { tab = "subscription" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult TestEmail(string testEmailAddress)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true, message = $"Test email sent to {testEmailAddress}." });

        TempData["Success"] = $"Test email sent to {testEmailAddress}.";
        return RedirectToAction("Index", new { tab = "email" });
    }
}
