using ByteBill_BS.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.SuperAdmin;

// ============================================================
//  SHOP  VIEW-MODELS
// ============================================================

public class ShopListViewModel
{
    public List<ShopItemViewModel> Shops { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Stats
    public int ActiveCount { get; set; }
    public int SuspendedCount { get; set; }
    public int NewThisMonth { get; set; }
}

public class ShopItemViewModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int UserCount { get; set; }
    public int JobOrderCount { get; set; }
    public string Status { get; set; } = "Active";
    public string StatusClass => Status switch
    {
        "Active"    => "status-success",
        "Suspended" => "status-danger",
        "Pending"   => "status-warning",
        _           => "status-muted"
    };
    public DateTime CreatedAt { get; set; }
}

public class ShopFormViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Shop name is required")]
    [StringLength(100)]
    [Display(Name = "Shop Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Owner name is required")]
    [StringLength(100)]
    [Display(Name = "Owner Name")]
    public string Owner { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [StringLength(100)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [StringLength(300)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Display(Name = "Status")]
    public string Status { get; set; } = "Active";

    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }
}

public class ShopDetailViewModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "Active";
    public string StatusClass => Status switch
    {
        "Active"    => "status-success",
        "Suspended" => "status-danger",
        "Pending"   => "status-warning",
        _           => "status-muted"
    };
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }

    // Stats
    public int UserCount { get; set; }
    public int JobOrderCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public int ActiveJobOrders { get; set; }

    // Recent Users
    public List<ShopUserItem> RecentUsers { get; set; } = new();
}

public class ShopUserItem
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string RoleClass { get; set; } = "status-muted";
    public bool IsActive { get; set; }
}

// ============================================================
//  GLOBAL USER  VIEW-MODELS  (SuperAdmin scope)
// ============================================================

public class GlobalUserListViewModel
{
    public List<GlobalUserItemViewModel> Users { get; set; } = new();
    public string? SearchTerm { get; set; }
    public UserRole? RoleFilter { get; set; }
    public string? ShopFilter { get; set; }
    public string? StatusFilter { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Stats
    public int ActiveCount { get; set; }
    public int AdminCount { get; set; }
    public int SuperAdminCount { get; set; }
    public int ShopCount { get; set; }

    // Filter options
    public List<string> AvailableShops { get; set; } = new();
}

public class GlobalUserItemViewModel
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleClass => Role switch
    {
        UserRole.SuperAdmin  => "status-purple",
        UserRole.Admin       => "status-primary",
        UserRole.Billing     => "status-success",
        UserRole.Technician  => "status-info",
        UserRole.Auditor     => "status-warning",
        _                    => "status-muted"
    };
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GlobalUserFormViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "First name is required")]
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Middle Name")]
    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [StringLength(100)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Shop is required")]
    [Display(Name = "Shop")]
    public long ShopId { get; set; }

    [Required(ErrorMessage = "Role is required")]
    [Display(Name = "Role")]
    public UserRole Role { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Compare("Password", ErrorMessage = "Passwords do not match")]
    [Display(Name = "Confirm Password")]
    public string? ConfirmPassword { get; set; }

    // Dropdown options
    public List<ShopDropdownItem> AvailableShops { get; set; } = new();
}

public class ShopDropdownItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GlobalUserDetailViewModel
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleClass => Role switch
    {
        UserRole.SuperAdmin  => "status-purple",
        UserRole.Admin       => "status-primary",
        UserRole.Billing     => "status-success",
        UserRole.Technician  => "status-info",
        UserRole.Auditor     => "status-warning",
        _                    => "status-muted"
    };
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastIpAddress { get; set; }

    public int JobOrdersHandled { get; set; }
    public int PaymentsProcessed { get; set; }
    public List<UserActivityLogItem> RecentActivity { get; set; } = new();
}

public class UserActivityLogItem
{
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

// ============================================================
//  SYSTEM LOG  VIEW-MODELS
// ============================================================

public class SystemLogListViewModel
{
    public List<SystemLogItemViewModel> Logs { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? TypeFilter { get; set; }
    public string? ShopFilter { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Stats
    public int TodayCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }

    // Filter options
    public List<string> AvailableShops { get; set; } = new();
    public List<string> LogTypes { get; set; } = new() { "Info", "Warning", "Error", "Critical" };
}

public class SystemLogItemViewModel
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string TypeClass => Type switch
    {
        "Info"     => "status-info",
        "Warning"  => "status-warning",
        "Error"    => "status-danger",
        "Critical" => "status-danger",
        _          => "status-muted"
    };
    public string TypeIcon => Type switch
    {
        "Info"     => "info",
        "Warning"  => "alert-triangle",
        "Error"    => "x-circle",
        "Critical" => "alert-octagon",
        _          => "file-text"
    };
    public string Message { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? ShopName { get; set; }
    public string? IpAddress { get; set; }
    public string? Source { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SystemLogDetailViewModel
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string TypeClass => Type switch
    {
        "Info"     => "status-info",
        "Warning"  => "status-warning",
        "Error"    => "status-danger",
        "Critical" => "status-danger",
        _          => "status-muted"
    };
    public string Message { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? ShopName { get; set; }
    public string? IpAddress { get; set; }
    public string? Source { get; set; }
    public string? StackTrace { get; set; }
    public string? RequestUrl { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
}

// ============================================================
//  SETTINGS  VIEW-MODELS
// ============================================================

public class SystemSettingsViewModel
{
    // General Settings
    [Required(ErrorMessage = "Platform name is required")]
    [StringLength(100)]
    [Display(Name = "Platform Name")]
    public string PlatformName { get; set; } = "ByteBill";

    [StringLength(200)]
    [Display(Name = "Tagline")]
    public string? Tagline { get; set; } = "Repair Shop Billing System";

    [Display(Name = "Currency")]
    public string Currency { get; set; } = "PHP";

    [Display(Name = "Timezone")]
    public string Timezone { get; set; } = "Asia/Manila";

    [Display(Name = "Date Format")]
    public string DateFormat { get; set; } = "MMM dd, yyyy";

    // Security Settings
    [Range(6, 50)]
    [Display(Name = "Minimum Password Length")]
    public int MinPasswordLength { get; set; } = 6;

    [Display(Name = "Require Uppercase")]
    public bool RequireUppercase { get; set; } = true;

    [Display(Name = "Require Numbers")]
    public bool RequireNumbers { get; set; } = true;

    [Display(Name = "Require Special Characters")]
    public bool RequireSpecialChars { get; set; } = false;

    [Range(5, 1440)]
    [Display(Name = "Session Timeout (minutes)")]
    public int SessionTimeout { get; set; } = 60;

    [Range(1, 10)]
    [Display(Name = "Max Login Attempts")]
    public int MaxLoginAttempts { get; set; } = 5;

    [Display(Name = "Enable Two-Factor Auth")]
    public bool Enable2FA { get; set; } = false;

    // Email Settings
    [Display(Name = "SMTP Host")]
    public string? SmtpHost { get; set; } = "smtp.gmail.com";

    [Display(Name = "SMTP Port")]
    public int SmtpPort { get; set; } = 587;

    [Display(Name = "SMTP Username")]
    public string? SmtpUsername { get; set; }

    [Display(Name = "SMTP Password")]
    public string? SmtpPassword { get; set; }

    [Display(Name = "Use SSL")]
    public bool SmtpUseSsl { get; set; } = true;

    [Display(Name = "From Email")]
    [EmailAddress]
    public string? FromEmail { get; set; } = "noreply@bytebill.ph";

    [Display(Name = "From Name")]
    public string? FromName { get; set; } = "ByteBill System";

    [Display(Name = "Enable Email Notifications")]
    public bool EnableEmailNotifications { get; set; } = true;
}
