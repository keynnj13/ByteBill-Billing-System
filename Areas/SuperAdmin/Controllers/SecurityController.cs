using ByteBill_BS.Data;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class SecurityController : Controller
{
    private static readonly string[] ProtectedSuperAdminUserNames = ["vkpadao", "vkbackup"];

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public SecurityController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private bool IsAuthorized()
    {
        var role = User.FindFirst("Role")?.Value;
        return string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var actorUserId = long.TryParse(User.FindFirst("UserId")?.Value, out var parsedActor) ? parsedActor : 0;
        var actorUserName = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserId == actorUserId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(actorUserName) || !IsProtectedSuperAdmin(actorUserName))
        {
            TempData["Error"] = "Only protected SuperAdmin accounts can access this security control.";
            return RedirectToAction("Index", "Dashboard", new { area = "SuperAdmin" });
        }
        var actorUserNameNormalized = actorUserName.ToLower();

        var lockedUsers = await _db.Users
            .AsNoTracking()
            .Where(u => u.IsPermanentlyLocked)
            .OrderByDescending(u => u.PermanentlyLockedAt)
            .Select(u => new
            {
                u.UserId,
                u.UserName,
                u.Email,
                u.PermanentlyLockedAt,
                u.LockoutReason
            })
            .ToListAsync();

        var superAdmins = await _db.Users
            .AsNoTracking()
            .Where(u => u.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin")
                && ProtectedSuperAdminUserNames.Contains(u.UserName)
                && u.UserName.ToLower() != actorUserNameNormalized)
            .OrderBy(u => u.UserName)
            .Select(u => new SuperAdminSecurityAccountItem
            {
                UserId = u.UserId,
                UserName = u.UserName,
                Email = u.Email,
                IsActive = u.IsActive,
                IsPermanentlyLocked = u.IsPermanentlyLocked,
                LastLoginAt = u.LastLoginAt,
                LockoutReason = u.LockoutReason
            })
            .ToListAsync();

        var model = new SuperAdminSecurityViewModel
        {
            ProtectedSuperAdmins = superAdmins,
            LockedUsers = lockedUsers.Select(u => new SuperAdminLockedUserItem
            {
                UserId = u.UserId,
                UserName = u.UserName,
                Email = u.Email,
                PermanentlyLockedAt = u.PermanentlyLockedAt,
                LockoutReason = u.LockoutReason
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockUser(
        long id,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            return HandlePostResult(false, "Invalid request.", requestedWith, 400);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (user is null)
        {
            return HandlePostResult(false, "User not found.", requestedWith, 404);
        }

        user.IsPermanentlyLocked = false;
        user.PermanentlyLockedAt = null;
        user.LockoutEndAt = null;
        user.LockoutCycleCount = 0;
        user.FailedLoginAttempts = 0;
        user.LockoutReason = null;
        user.AuthVersion += 1;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var actorUserId = long.TryParse(User.FindFirst("UserId")?.Value, out var parsed) ? parsed : 0;
        await _audit.LogAsync(user.ShopId, actorUserId, "SecurityUnlockUser", "User", user.UserId,
            $"User '{user.UserName}' unlocked by Super Admin security control.", HttpContext.Connection.RemoteIpAddress?.ToString());

        return HandlePostResult(true, "User unlocked successfully.", requestedWith);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendSuperAdmin(
        long id,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            return HandlePostResult(false, "Invalid request.", requestedWith, 400);
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user is null)
        {
            return HandlePostResult(false, "User not found.", requestedWith, 404);
        }

        var actorUserId = long.TryParse(User.FindFirst("UserId")?.Value, out var parsed) ? parsed : 0;
        var actorUserName = await _db.Users
            .Where(u => u.UserId == actorUserId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(actorUserName) || !IsProtectedSuperAdmin(actorUserName))
        {
            return HandlePostResult(false, "Only protected SuperAdmin accounts can perform this action.", requestedWith, 403);
        }

        if (actorUserId == user.UserId)
        {
            return HandlePostResult(false, "You cannot suspend your own account.", requestedWith, 400);
        }

        var isSuperAdmin = user.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin");
        if (!isSuperAdmin)
        {
            return HandlePostResult(false, "Target user is not a SuperAdmin.", requestedWith, 400);
        }

        if (!IsProtectedSuperAdmin(user.UserName))
        {
            return HandlePostResult(false, "Only the protected main/backup SuperAdmin accounts can be suspended here.", requestedWith, 400);
        }

        user.IsActive = false;
        user.AuthVersion += 1;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(user.ShopId, actorUserId, "SecuritySuspendSuperAdmin", "User", user.UserId,
            $"SuperAdmin '{user.UserName}' access suspended by Super Admin security control.", HttpContext.Connection.RemoteIpAddress?.ToString());

        return HandlePostResult(true, "SuperAdmin suspended successfully.", requestedWith);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreSuperAdmin(
        long id,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            return HandlePostResult(false, "Invalid request.", requestedWith, 400);
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user is null)
        {
            return HandlePostResult(false, "User not found.", requestedWith, 404);
        }

        var isSuperAdmin = user.UserRoles.Any(ur => ur.Role!.RoleName == "SuperAdmin");
        if (!isSuperAdmin)
        {
            return HandlePostResult(false, "Target user is not a SuperAdmin.", requestedWith, 400);
        }

        if (!IsProtectedSuperAdmin(user.UserName))
        {
            return HandlePostResult(false, "Only the protected main/backup SuperAdmin accounts can be restored here.", requestedWith, 400);
        }

        var actorUserId = long.TryParse(User.FindFirst("UserId")?.Value, out var parsed) ? parsed : 0;
        var actorUserName = await _db.Users
            .Where(u => u.UserId == actorUserId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(actorUserName) || !IsProtectedSuperAdmin(actorUserName))
        {
            return HandlePostResult(false, "Only protected SuperAdmin accounts can perform this action.", requestedWith, 403);
        }

        user.IsActive = true;
        user.AuthVersion += 1;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(user.ShopId, actorUserId, "SecurityRestoreSuperAdmin", "User", user.UserId,
            $"SuperAdmin '{user.UserName}' access restored by Super Admin security control.", HttpContext.Connection.RemoteIpAddress?.ToString());

        return HandlePostResult(true, "SuperAdmin restored successfully.", requestedWith);
    }

    private IActionResult HandlePostResult(bool success, string message, string? requestedWith, int statusCode = 200)
    {
        if (requestedWith == "XMLHttpRequest")
        {
            Response.StatusCode = statusCode;
            return Json(new { success, message });
        }

        if (success)
        {
            TempData["Success"] = message;
        }
        else
        {
            TempData["Error"] = message;
        }

        return RedirectToAction(nameof(Index));
    }

    private static bool IsProtectedSuperAdmin(string userName)
    {
        return ProtectedSuperAdminUserNames.Contains(userName, StringComparer.OrdinalIgnoreCase);
    }

    public sealed class SuperAdminSecurityViewModel
    {
        public List<SuperAdminSecurityAccountItem> ProtectedSuperAdmins { get; set; } = new();
        public List<SuperAdminLockedUserItem> LockedUsers { get; set; } = new();
    }

    public sealed class SuperAdminSecurityAccountItem
    {
        public long UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public bool IsPermanentlyLocked { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? LockoutReason { get; set; }
    }

    public sealed class SuperAdminLockedUserItem
    {
        public long UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public DateTime? PermanentlyLockedAt { get; set; }
        public string? LockoutReason { get; set; }
    }
}
