using ByteBill_BS.Data;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace ByteBill_BS.Controllers;

public class AuthController : Controller
{
    private readonly ApplicationDbContext _db;

    public AuthController(ApplicationDbContext db)
    {
        _db = db;
    }

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

        // ── Look up user in the database ─────────────────────────────
        var dbUser = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserName == key && u.IsActive);

        if (dbUser is not null && BCrypt.Net.BCrypt.Verify(model.Password, dbUser.PasswordHash))
        {
            // Success — clear any tracked attempts
            _loginAttempts.TryRemove(key, out _);

            var roleName = dbUser.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "Billing";
            if (!Enum.TryParse<UserRole>(roleName, out var userRole))
                userRole = UserRole.Billing;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, dbUser.UserId.ToString()),
                new Claim(ClaimTypes.Name, dbUser.FullName),
                new Claim("UserId", dbUser.UserId.ToString()),
                new Claim("FullName", dbUser.FullName),
                new Claim("FirstName", dbUser.FirstName),
                new Claim("LastName", dbUser.LastName),
                new Claim("Initials", dbUser.Initials),
                new Claim("Role", userRole.ToString()),
                new Claim("ShopId", dbUser.ShopId.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            // ── Audit log the login ──────────────────────────────────
            _db.AuditLogs.Add(new Models.AuditLog
            {
                ShopId = dbUser.ShopId,
                UserId = dbUser.UserId,
                Action = "Login",
                EntityName = "User",
                EntityId = dbUser.UserId,
                Details = $"User '{dbUser.UserName}' logged in successfully.",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToRoleDashboard(userRole);
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
        // ── Audit log the logout ─────────────────────────────────────
        var userIdStr = User.FindFirstValue("UserId");
        var shopIdStr = User.FindFirstValue("ShopId");
        if (long.TryParse(userIdStr, out var userId) && long.TryParse(shopIdStr, out var shopId))
        {
            _db.AuditLogs.Add(new Models.AuditLog
            {
                ShopId = shopId,
                UserId = userId,
                Action = "Logout",
                EntityName = "User",
                EntityId = userId,
                Details = $"User logged out.",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

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
