using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ByteBill_BS.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public ProfileController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ── Profile Settings Page ──────────────────────────────────────────
    [HttpGet("/Profile")]
    public async Task<IActionResult> Index()
    {
        var userId = User.GetUserId();
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Shop)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user is null) return RedirectToAction("Login", "Auth");

        var role = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Unknown";

        ViewBag.SuccessMessage = TempData["SuccessMessage"];
        ViewBag.ErrorMessage = TempData["ErrorMessage"];

        return View(new ProfileViewModel
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            MiddleName = user.MiddleName,
            LastName = user.LastName,
            UserName = user.UserName,
            Email = user.Email,
            Phone = user.Phone,
            RoleName = role,
            ShopName = user.Shop?.ShopName ?? "—",
            Initials = user.Initials,
            CreatedAt = user.CreatedAt
        });
    }

    [HttpPost("/Profile/UpdateProfile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(ProfileUpdateRequest model)
    {
        var userId = User.GetUserId();
        var shopId = User.GetShopId();
        var user = await _db.Users.FindAsync(userId);

        if (user is null) return RedirectToAction("Login", "Auth");

        // Capture old values for audit
        var oldValues = JsonSerializer.Serialize(new
        {
            user.FirstName,
            user.MiddleName,
            user.LastName,
            user.Email,
            user.Phone
        });

        // Validate
        if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.LastName))
        {
            TempData["ErrorMessage"] = "First name and last name are required.";
            return RedirectToAction(nameof(Index));
        }

        if (!string.IsNullOrWhiteSpace(model.Phone) && !System.Text.RegularExpressions.Regex.IsMatch(model.Phone, @"^09\d{9}$"))
        {
            TempData["ErrorMessage"] = "Phone must be 11 digits starting with 09.";
            return RedirectToAction(nameof(Index));
        }

        user.FirstName = model.FirstName.Trim();
        user.MiddleName = string.IsNullOrWhiteSpace(model.MiddleName) ? null : model.MiddleName.Trim();
        user.LastName = model.LastName.Trim();
        user.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        user.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        var newValues = JsonSerializer.Serialize(new
        {
            user.FirstName,
            user.MiddleName,
            user.LastName,
            user.Email,
            user.Phone
        });

        await _db.SaveChangesAsync();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(shopId, userId, "Update", "User", userId,
            "User updated their profile.", ip, oldValues, newValues);

        TempData["SuccessMessage"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── Preferences Page ───────────────────────────────────────────────
    [HttpGet("/Profile/Preferences")]
    public async Task<IActionResult> Preferences()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return RedirectToAction("Login", "Auth");

        ViewBag.SuccessMessage = TempData["SuccessMessage"];

        return View(new PreferencesViewModel
        {
            ThemePreference = user.ThemePreference,
            EmailNotifications = user.EmailNotifications,
            InAppNotifications = user.InAppNotifications
        });
    }

    [HttpPost("/Profile/UpdatePreferences")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePreferences(PreferencesUpdateRequest model)
    {
        var userId = User.GetUserId();
        var shopId = User.GetShopId();
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return RedirectToAction("Login", "Auth");

        var theme = model.ThemePreference == "dark" ? "dark" : "light";
        user.ThemePreference = theme;
        user.EmailNotifications = model.EmailNotifications;
        user.InAppNotifications = model.InAppNotifications;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // ── Update theme cookie ────────────────────────────────────
        Response.Cookies.Append("ByteBillTheme", theme, new CookieOptions
        {
            HttpOnly = false,
            Expires = DateTimeOffset.UtcNow.AddDays(365),
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(shopId, userId, "Update", "User", userId,
            $"Updated preferences: theme={theme}, email={model.EmailNotifications}, inApp={model.InAppNotifications}", ip);

        TempData["SuccessMessage"] = "Preferences saved successfully.";
        return RedirectToAction(nameof(Preferences));
    }
}

// ── View Models ────────────────────────────────────────────────────────
public class ProfileViewModel
{
    public long UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ProfileUpdateRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class PreferencesViewModel
{
    public string ThemePreference { get; set; } = "light";
    public bool EmailNotifications { get; set; }
    public bool InAppNotifications { get; set; }
}

public class PreferencesUpdateRequest
{
    public string ThemePreference { get; set; } = "light";
    public bool EmailNotifications { get; set; }
    public bool InAppNotifications { get; set; }
}
