namespace ByteBill_BS.Models;

public class User
{
    public long UserId { get; set; }
    public long ShopId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? EmailHash { get; set; }
    public string? Phone { get; set; }
    public string? PhoneHash { get; set; }
    public string ThemePreference { get; set; } = "light";
    public bool EmailNotifications { get; set; } = true;
    public bool InAppNotifications { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int AuthVersion { get; set; } = 1;
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutEndAt { get; set; }
    public int LockoutCycleCount { get; set; } = 0;
    public bool IsPermanentlyLocked { get; set; } = false;
    public DateTime? PermanentlyLockedAt { get; set; }
    public string? LockoutReason { get; set; }
    public DateTime? LastFailedLoginAt { get; set; }

    public bool IsMfaEnabled { get; set; } = false;
    public string? MfaType { get; set; }
    public string? TotpSecretKey { get; set; }
    public string? EmailOtpHash { get; set; }
    public DateTime? EmailOtpExpiresAt { get; set; }
    public int EmailOtpFailedAttempts { get; set; } = 0;
    public DateTime? LastMfaAt { get; set; }
    public bool MustChangePassword { get; set; } = false;
    public DateTime? TemporaryPasswordIssuedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public string? LastIpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Shop? Shop { get; set; }
    public ICollection<UserRoleAssignment> UserRoles { get; set; } = new List<UserRoleAssignment>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    // Computed
    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{(FirstName.Length > 0 ? FirstName[0] : ' ')}{(LastName.Length > 0 ? LastName[0] : ' ')}".Trim().ToUpper();
}
