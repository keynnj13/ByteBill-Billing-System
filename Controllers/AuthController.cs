using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace ByteBill_BS.Controllers;

public class AuthController : Controller
{
    // ── Demo users for testing ──────────────────────────────────────────
    private static readonly Dictionary<string, (string Password, string FirstName, string LastName, UserRole Role, long ShopId)> DemoUsers = new()
    {
        ["superadmin"] = ("Password123!", "Super", "Admin", UserRole.SuperAdmin, 1),
        ["admin"]      = ("Password123!", "Shop", "Owner", UserRole.Admin, 1),
        ["billing"]    = ("Password123!", "Billing", "Staff", UserRole.Billing, 1),
        ["tech"]       = ("Password123!", "Tech", "Support", UserRole.Technician, 1),
        ["auditor"]    = ("Password123!", "External", "Auditor", UserRole.Auditor, 1)
    };

    // ── Account lockout tracking (in-memory) ────────────────────────────
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private static readonly ConcurrentDictionary<string, (int Attempts, DateTime? LockedUntil)> _loginAttempts = new();

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated ?? false)
        {
            return RedirectToRoleDashboard();
        }

        ViewData["HideNavigation"] = true;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["HideNavigation"] = true;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var key = model.UserName.ToLowerInvariant().Trim();

        // ── Check lockout ────────────────────────────────────────────
        if (_loginAttempts.TryGetValue(key, out var state) && state.LockedUntil.HasValue)
        {
            if (DateTime.UtcNow < state.LockedUntil.Value)
            {
                var remaining = (int)Math.Ceiling((state.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
                ViewBag.LockoutMessage = $"Account locked. Try again in {remaining} minute{(remaining != 1 ? "s" : "")}.";
                return View(model);
            }
            // Lockout expired — reset
            _loginAttempts.TryRemove(key, out _);
        }

        // ── Validate credentials ─────────────────────────────────────
        if (DemoUsers.TryGetValue(key, out var user) && user.Password == model.Password)
        {
            // Success — clear any tracked attempts
            _loginAttempts.TryRemove(key, out _);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, model.UserName),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim("FullName", $"{user.FirstName} {user.LastName}"),
                new Claim("FirstName", user.FirstName),
                new Claim("LastName", user.LastName),
                new Claim("Initials", $"{user.FirstName[0]}{user.LastName[0]}"),
                new Claim("Role", user.Role.ToString()),
                new Claim("ShopId", user.ShopId.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToRoleDashboard(user.Role);
        }

        // ── Failed attempt — increment counter ───────────────────────
        var current = _loginAttempts.GetOrAdd(key, _ => (0, null));
        var attempts = current.Attempts + 1;

        if (attempts >= MaxFailedAttempts)
        {
            _loginAttempts[key] = (attempts, DateTime.UtcNow.Add(LockoutDuration));
            ViewBag.LockoutMessage = $"Too many failed attempts. Account locked for {(int)LockoutDuration.TotalMinutes} minutes.";
        }
        else
        {
            _loginAttempts[key] = (attempts, null);
            var remaining = MaxFailedAttempts - attempts;
            ModelState.AddModelError(string.Empty, $"Invalid username or password. {remaining} attempt{(remaining != 1 ? "s" : "")} remaining.");
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToRoleDashboard(UserRole? role = null)
    {
        var userRole = role ?? GetUserRole();
        
        return userRole switch
        {
            UserRole.SuperAdmin => RedirectToAction("Index", "Dashboard", new { area = "SuperAdmin" }),
            UserRole.Admin => RedirectToAction("Index", "Dashboard", new { area = "Admin" }),
            UserRole.Billing => RedirectToAction("Index", "Dashboard", new { area = "Billing" }),
            UserRole.Technician => RedirectToAction("Index", "Dashboard", new { area = "Technician" }),
            UserRole.Auditor => RedirectToAction("Index", "Dashboard", new { area = "Auditor" }),
            _ => RedirectToAction(nameof(Login))
        };
    }

    private UserRole GetUserRole()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return Enum.TryParse<UserRole>(roleClaim, out var role) ? role : UserRole.Billing;
    }
}
