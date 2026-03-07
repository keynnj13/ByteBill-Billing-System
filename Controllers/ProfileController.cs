using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
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
            .AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Shop)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user is null) return RedirectToAction("Login", "Auth");

        var role = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Unknown";

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
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);

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
            TempData["Error"] = "First name and last name are required.";
            return RedirectToAction(nameof(Index));
        }

        if (!string.IsNullOrWhiteSpace(model.Phone) && !System.Text.RegularExpressions.Regex.IsMatch(model.Phone, @"^09\d{9}$"))
        {
            TempData["Error"] = "Phone must be 11 digits starting with 09.";
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

        // Re-sign in so navbar reflects updated name/initials
        await RefreshAuthCookieAsync(user);

        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── Change Password ────────────────────────────────────────────────
    [HttpPost("/Profile/ChangePassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest model)
    {
        var userId = User.GetUserId();
        var shopId = User.GetShopId();
        var user = await _db.Users.FindAsync(userId);

        if (user is null) return RedirectToAction("Login", "Auth");

        if (string.IsNullOrWhiteSpace(model.CurrentPassword) || string.IsNullOrWhiteSpace(model.NewPassword))
        {
            TempData["Error"] = "Both current and new password are required.";
            return RedirectToAction(nameof(Index));
        }

        if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
        {
            TempData["Error"] = "Current password is incorrect.";
            return RedirectToAction(nameof(Index));
        }

        if (model.NewPassword.Length < 6)
        {
            TempData["Error"] = "New password must be at least 6 characters.";
            return RedirectToAction(nameof(Index));
        }

        if (model.NewPassword != model.ConfirmNewPassword)
        {
            TempData["Error"] = "New password and confirmation do not match.";
            return RedirectToAction(nameof(Index));
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(shopId, userId, "Update", "User", userId,
            "User changed their password.", ip);

        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── Preferences Page ───────────────────────────────────────────────
    [HttpGet("/Profile/Preferences")]
    public async Task<IActionResult> Preferences()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user is null) return RedirectToAction("Login", "Auth");

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
        if (user is null)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = "Session expired." });
            return RedirectToAction("Login", "Auth");
        }

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
            Secure = true,
            Expires = DateTimeOffset.UtcNow.AddDays(365),
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(shopId, userId, "Update", "User", userId,
            $"Updated preferences: theme={theme}, email={model.EmailNotifications}, inApp={model.InAppNotifications}", ip);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = true });

        TempData["Success"] = "Preferences saved successfully.";
        return RedirectToAction(nameof(Preferences));
    }

    // ── Helper: re-issue auth cookie with updated claims ───────────────
    private async Task RefreshAuthCookieAsync(Models.User user)
    {
        var roleName = user.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
        if (!Enum.TryParse<UserRole>(roleName, out var userRole))
            userRole = UserRole.Billing;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim("UserId", user.UserId.ToString()),
            new Claim("FullName", user.FullName),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName),
            new Claim("Initials", user.Initials),
            new Claim("Role", userRole.ToString()),
            new Claim("ShopId", user.ShopId.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
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

    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Phone must be 11 digits starting with 09")]
    [StringLength(11)]
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

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
