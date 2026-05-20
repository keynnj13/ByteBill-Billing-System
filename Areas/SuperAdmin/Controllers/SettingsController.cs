using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;

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

    private static bool IsAjaxRequest(string? requestedWith)
        => string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private IActionResult AjaxOrRedirect(string? requestedWith, bool success, string message, string tab)
    {
        if (IsAjaxRequest(requestedWith))
        {
            return Json(new { success, message });
        }

        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction("Index", new { tab });
    }

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
            SessionTimeout = int.TryParse(settings.GetValueOrDefault("Security.SessionTimeout", "5"), out var st) ? st : 5,
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
    public async Task<IActionResult> SaveGeneral(SystemSettingsViewModel model, [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            var errorMessage = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            var message = string.IsNullOrWhiteSpace(errorMessage) ? "Invalid general settings." : errorMessage;
            return AjaxOrRedirect(requestedWith, false, message, "general");
        }

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
        return AjaxOrRedirect(requestedWith, result.Success, result.Message, "general");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSecurity(SystemSettingsViewModel model, [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            var errorMessage = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            var message = string.IsNullOrWhiteSpace(errorMessage) ? "Invalid security settings." : errorMessage;
            return AjaxOrRedirect(requestedWith, false, message, "security");
        }

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
        return AjaxOrRedirect(requestedWith, result.Success, result.Message, "security");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmail(SystemSettingsViewModel model, [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            var errorMessage = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            var message = string.IsNullOrWhiteSpace(errorMessage) ? "Invalid email/SMTP settings." : errorMessage;
            return AjaxOrRedirect(requestedWith, false, message, "email");
        }

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
        return AjaxOrRedirect(requestedWith, result.Success, result.Message, "email");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSubscription(SystemSettingsViewModel model, [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (!ModelState.IsValid)
        {
            var errorMessage = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            var message = string.IsNullOrWhiteSpace(errorMessage) ? "Invalid subscription settings." : errorMessage;
            return AjaxOrRedirect(requestedWith, false, message, "subscription");
        }

        var settings = new Dictionary<string, string>
        {
            ["PayMongo.TestMode"] = model.PayMongoTestMode.ToString().ToLower(),
            ["Subscription.TrialDays"] = model.TrialDays.ToString()
        };

        var result = await _service.SaveSettingsAsync(settings, "Subscription", GetUserId());
        return AjaxOrRedirect(requestedWith, result.Success, result.Message, "subscription");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestEmail(string testEmailAddress, [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        if (string.IsNullOrWhiteSpace(testEmailAddress) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(testEmailAddress))
        {
            return AjaxOrRedirect(requestedWith, false, "Please enter a valid recipient email address.", "email");
        }

        var settings = await _service.GetSettingsAsync("Email");
        var smtpHost = settings.GetValueOrDefault("Email.SmtpHost", string.Empty);
        var smtpPort = int.TryParse(settings.GetValueOrDefault("Email.SmtpPort", "587"), out var parsedPort) ? parsedPort : 587;
        var smtpUser = settings.GetValueOrDefault("Email.SmtpUsername", string.Empty);
        var smtpPass = settings.GetValueOrDefault("Email.SmtpPassword", string.Empty);
        var smtpUseSsl = settings.GetValueOrDefault("Email.SmtpUseSsl", "true") == "true";
        var fromEmail = settings.GetValueOrDefault("Email.FromEmail", string.Empty);
        var fromName = settings.GetValueOrDefault("Email.FromName", "ByteBill System");

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(fromEmail))
        {
            return AjaxOrRedirect(requestedWith, false, "SMTP host and From Email are required before sending test email.", "email");
        }

        if (!smtpUseSsl)
        {
            return AjaxOrRedirect(requestedWith, false, "SMTP test requires TLS (Enable SSL).", "email");
        }

        try
        {
            using var smtp = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(smtpUser))
            {
                smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "ByteBill SMTP Test Email",
                Body = "This is a test email from ByteBill System Settings.",
                IsBodyHtml = false
            };
            mail.To.Add(testEmailAddress.Trim());

            await smtp.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            return AjaxOrRedirect(requestedWith, false, $"SMTP test failed: {ex.Message}", "email");
        }

        return AjaxOrRedirect(requestedWith, true, $"Test email sent to {testEmailAddress}.", "email");
    }
}
