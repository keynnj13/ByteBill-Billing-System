using System.Security.Cryptography;
using System.Text;
using ByteBill_BS.Data;
using ByteBill_BS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ByteBill_BS.Services;

public class PasswordResetSettings
{
    public int TokenExpiryMinutes { get; set; } = 60;
    public int MaxRequestsPerHour { get; set; } = 3;
    public string BaseUrl { get; set; } = "https://localhost:7048";
}

public enum PasswordResetRequestResult
{
    Processed,
    DeniedLowPrivilege
}

public interface IPasswordResetService
{
    Task<PasswordResetRequestResult> RequestResetAsync(string email, string? requestIp);
    Task<bool> ValidateTokenAsync(string email, string token);
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword, string? requestIp);
}

public class PasswordResetService : IPasswordResetService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IAuditService _audit;
    private readonly IEmailSecurityService _emailSecurity;
    private readonly PasswordResetSettings _settings;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        ApplicationDbContext db,
        IEmailService emailService,
        IAuditService audit,
        IEmailSecurityService emailSecurity,
        IOptions<PasswordResetSettings> settings,
        ILogger<PasswordResetService> logger)
    {
        _db = db;
        _emailService = emailService;
        _audit = audit;
        _emailSecurity = emailSecurity;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<PasswordResetRequestResult> RequestResetAsync(string email, string? requestIp)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var emailHash = _emailSecurity.ComputeHash(normalizedEmail);

        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.IsActive && u.EmailHash != null && u.EmailHash == emailHash);

        // Keep response generic in caller; we silently exit when account does not exist.
        if (user is null)
        {
            return PasswordResetRequestResult.Processed;
        }

        var canSelfReset = user.UserRoles.Any(ur => ur.Role != null &&
            (ur.Role.RoleName == "Admin" || ur.Role.RoleName == "SuperAdmin"));
        if (!canSelfReset)
        {
            _logger.LogInformation("Password reset self-service denied for low-privilege UserId {UserId}", user.UserId);
            return PasswordResetRequestResult.DeniedLowPrivilege;
        }

        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var recentCount = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.UserId && t.CreatedAt >= oneHourAgo)
            .CountAsync();

        if (recentCount >= _settings.MaxRequestsPerHour)
        {
            _logger.LogWarning("Password reset request limit hit for UserId {UserId}", user.UserId);
            return PasswordResetRequestResult.Processed;
        }

        var rawToken = GenerateSecureToken();
        var tokenHash = ComputeSha256(rawToken);

        var expiresAt = DateTime.UtcNow.AddMinutes(Math.Max(5, _settings.TokenExpiryMinutes));

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            RequestedIp = requestIp,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var resetUrl = BuildResetUrl(user.Email!, rawToken);
        await _emailService.SendPasswordResetLinkAsync(user.Email!, user.FullName, resetUrl, expiresAt);

        await _audit.LogAsync(
            user.ShopId,
            user.UserId,
            "PasswordResetRequested",
            "User",
            user.UserId,
            "Password reset link requested.",
            requestIp);

        return PasswordResetRequestResult.Processed;
    }

    public async Task<bool> ValidateTokenAsync(string email, string token)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var emailHash = _emailSecurity.ComputeHash(normalizedEmail);
        var tokenHash = ComputeSha256(token.Trim());

        var tokenRow = await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (tokenRow is null || tokenRow.User is null)
        {
            return false;
        }

        if (tokenRow.User.EmailHash != emailHash)
        {
            return false;
        }

        if (!tokenRow.User.IsActive)
        {
            return false;
        }

        var roleNames = await _db.UserRoles
            .Where(ur => ur.UserId == tokenRow.User.UserId)
            .Select(ur => ur.Role!.RoleName)
            .ToListAsync();

        var canSelfReset = roleNames.Any(r => r == "Admin" || r == "SuperAdmin");
        if (!canSelfReset)
        {
            return false;
        }

        if (tokenRow.UsedAt.HasValue)
        {
            return false;
        }

        return DateTime.UtcNow <= tokenRow.ExpiresAt;
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword, string? requestIp)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var emailHash = _emailSecurity.ComputeHash(normalizedEmail);
        var tokenHash = ComputeSha256(token.Trim());

        var tokenRow = await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (tokenRow is null || tokenRow.User is null)
        {
            return false;
        }

        var user = tokenRow.User;
        if (user.EmailHash != emailHash)
        {
            return false;
        }

        if (!user.IsActive || tokenRow.UsedAt.HasValue || DateTime.UtcNow > tokenRow.ExpiresAt)
        {
            return false;
        }

        var roleNames = await _db.UserRoles
            .Where(ur => ur.UserId == user.UserId)
            .Select(ur => ur.Role!.RoleName)
            .ToListAsync();

        var canSelfReset = roleNames.Any(r => r == "Admin" || r == "SuperAdmin");
        if (!canSelfReset)
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.AuthVersion += 1;
        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;
        user.LockoutCycleCount = 0;
        user.IsPermanentlyLocked = false;
        user.PermanentlyLockedAt = null;
        user.LockoutReason = null;
        user.EmailOtpHash = null;
        user.EmailOtpExpiresAt = null;
        user.EmailOtpFailedAttempts = 0;
        user.MustChangePassword = false;
        user.TemporaryPasswordIssuedAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        tokenRow.UsedAt = DateTime.UtcNow;

        // Invalidate all other active reset tokens for this user.
        var activeTokens = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.UserId && !t.UsedAt.HasValue && t.PasswordResetTokenId != tokenRow.PasswordResetTokenId)
            .ToListAsync();

        foreach (var activeToken in activeTokens)
        {
            activeToken.UsedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            user.ShopId,
            user.UserId,
            "PasswordResetCompleted",
            "User",
            user.UserId,
            "Password reset completed by recovery flow.",
            requestIp);

        return true;
    }

    private string BuildResetUrl(string email, string token)
    {
        var baseUrl = (_settings.BaseUrl ?? string.Empty).TrimEnd('/');
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedToken = Uri.EscapeDataString(token);
        return $"{baseUrl}/Auth/ResetPassword?email={encodedEmail}&token={encodedToken}";
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
