using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
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
    public string PlanName { get; set; } = "No Plan";
    public string BillingCycle { get; set; } = "—";
    public bool IsDefault { get; set; }
    public string StatusClass => Status switch
    {
        "Active"    => "status-success",
        "Suspended" => "status-danger",
        "Pending"   => "status-warning",
        _           => "status-muted"
    };
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Combined shop + admin user creation form.
/// </summary>
public class ShopCreateViewModel
{
    // Shop fields
    [Required(ErrorMessage = "Shop name is required")]
    [StringLength(100)]
    [Display(Name = "Shop Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "Shop Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(11)]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Phone must be 11 digits starting with 09")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [StringLength(300)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    // Admin user fields
    [Required(ErrorMessage = "Admin first name is required")]
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string AdminFirstName { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Middle Name")]
    public string? AdminMiddleName { get; set; }

    [Required(ErrorMessage = "Admin last name is required")]
    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string AdminLastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Admin email is required")]
    [EmailAddress]
    [StringLength(100)]
    [Display(Name = "Admin Email")]
    public string AdminEmail { get; set; } = string.Empty;

    [StringLength(11)]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Phone must be 11 digits starting with 09")]
    [Display(Name = "Admin Phone")]
    public string? AdminPhone { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6)]
    [Display(Name = "Password")]
    public string AdminPassword { get; set; } = string.Empty;

    [Compare("AdminPassword", ErrorMessage = "Passwords do not match")]
    [Display(Name = "Confirm Password")]
    public string AdminConfirmPassword { get; set; } = string.Empty;
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

    [StringLength(11)]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Phone must be 11 digits starting with 09")]
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
    public bool IsDefault { get; set; }
    public string PlanName { get; set; } = "No Plan";
    public string BillingCycle { get; set; } = "—";

    // Stats — no user-specific data (confidential)
    public decimal TotalRevenue { get; set; }
    public DateTime? LastActiveAt { get; set; }
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

    [StringLength(11)]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Phone must be 11 digits starting with 09")]
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
//  SUBSCRIPTION  VIEW-MODELS
// ============================================================

public class SubscriptionListViewModel
{
    public List<SubscriptionItemViewModel> Subscriptions { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }
    public string? PlanFilter { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public int ActiveCount { get; set; }
    public int ExpiredCount { get; set; }
    public decimal TotalMRR { get; set; }
}

public class SubscriptionItemViewModel
{
    public long Id { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string ShopInitials { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = "Active";
    public string StatusClass => Status switch
    {
        "Active"    => "status-success",
        "Expired"   => "status-danger",
        "Cancelled" => "status-muted",
        "PastDue"   => "status-warning",
        _           => "status-muted"
    };
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public bool IsDefault { get; set; }
}

public class SubscriptionDetailViewModel
{
    public long Id { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string ShopInitials { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string PlanDescription { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = "Active";
    public string StatusClass => Status switch
    {
        "Active"    => "status-success",
        "Expired"   => "status-danger",
        "Cancelled" => "status-muted",
        "PastDue"   => "status-warning",
        _           => "status-muted"
    };
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public bool IsDefault { get; set; }
    public int MaxUsers { get; set; }
    public int MaxCustomers { get; set; }
    public int MaxJobOrdersPerMonth { get; set; }
    public int CurrentUsers { get; set; }
    public decimal TotalPaid { get; set; }
    public List<SubscriptionPaymentSummary> PaymentHistory { get; set; } = new();
}

public class SubscriptionPaymentSummary
{
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public string? PaymentMethod { get; set; }
}

public class AssignSubscriptionViewModel
{
    [Required]
    public long ShopId { get; set; }

    [Required]
    public long PlanId { get; set; }

    [Required]
    public string BillingCycle { get; set; } = "Monthly";

    public List<ShopDropdownItem> AvailableShops { get; set; } = new();
}

// ============================================================
//  SUBSCRIPTION PAYMENT  VIEW-MODELS
// ============================================================

public class SubscriptionPaymentListViewModel
{
    public List<SubscriptionPaymentItemViewModel> Payments { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? StatusFilter { get; set; }
    public string? MethodFilter { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public decimal TotalPaid { get; set; }
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
}

public class SubscriptionPaymentItemViewModel
{
    public long Id { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending";
    public string StatusClass => Status switch
    {
        "Paid"     => "status-success",
        "Pending"  => "status-warning",
        "Failed"   => "status-danger",
        "Refunded" => "status-info",
        _          => "status-muted"
    };
    public string PaymentMethod { get; set; } = "—";
    public string ReferenceNumber { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubscriptionPaymentDetailViewModel
{
    public long Id { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PHP";
    public string Status { get; set; } = string.Empty;
    public string StatusClass => Status switch
    {
        "Paid"     => "status-success",
        "Pending"  => "status-warning",
        "Failed"   => "status-danger",
        "Refunded" => "status-info",
        _          => "status-muted"
    };
    public string PaymentMethod { get; set; } = "—";
    public string ReferenceNumber { get; set; } = string.Empty;
    public string? PayMongoPaymentId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
}

// ============================================================
//  ANNOUNCEMENT  VIEW-MODELS
// ============================================================

public class AnnouncementListViewModel
{
    public List<AnnouncementItemViewModel> Announcements { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? TypeFilter { get; set; }
    public string? StatusFilter { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public int PublishedCount { get; set; }
    public int DraftCount { get; set; }
    public int ScheduledCount { get; set; }
}

public class AnnouncementItemViewModel
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";
    public string TypeClass => Type switch
    {
        "Info"        => "status-info",
        "Warning"     => "status-warning",
        "Critical"    => "status-danger",
        "Maintenance" => "status-purple",
        _             => "status-muted"
    };
    public string Status { get; set; } = "Draft";
    public string StatusClass => Status switch
    {
        "Published" => "status-success",
        "Draft"     => "status-muted",
        "Archived"  => "status-warning",
        _           => "status-muted"
    };
    public string Content { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AnnouncementFormViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "Title is required")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = string.Empty;

    public string Type { get; set; } = "Info";
    public DateTime? ExpiresAt { get; set; }
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
    public string? Tagline { get; set; } = "A Web-Based Billing System";

    [Display(Name = "Currency")]
    public string Currency { get; set; } = "PHP";

    [Display(Name = "Timezone")]
    public string Timezone { get; set; } = "Asia/Manila";

    [Display(Name = "Date Format")]
    public string DateFormat { get; set; } = "MMM dd, yyyy";

    // Tax Settings
    [Display(Name = "Default VAT Rate (%)")]
    public decimal DefaultVatRate { get; set; } = 12m;

    [Display(Name = "Default VAT Registered")]
    public bool DefaultIsVatRegistered { get; set; } = true;

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

    // PayMongo Settings
    [Display(Name = "Test Mode")]
    public bool PayMongoTestMode { get; set; } = true;

    // Subscription Settings
    [Display(Name = "Trial Period (days)")]
    public int TrialDays { get; set; } = 14;
}

// ═══════════════════════════════════════════════════════════════
//  REPORTS — HUB
// ═══════════════════════════════════════════════════════════════

public class SAReportHubViewModel
{
    public List<SAReportCategoryCard> Categories { get; set; } = new();
}

public class SAReportCategoryCard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════
//  1. REVENUE REPORT
// ═══════════════════════════════════════════════════════════════

public class SARevenueReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TransactionCount { get; set; }
    public int PayingShops { get; set; }
    public decimal AvgPerShop { get; set; }
    public string Interpretation { get; set; } = string.Empty;
    public List<SAMonthlyRevenueRow> MonthlyBreakdown { get; set; } = new();
    public List<SARevenuePaymentRow> Payments { get; set; } = new();
}

public class SAMonthlyRevenueRow
{
    public string Month { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class SARevenuePaymentRow
{
    public string Reference { get; set; } = string.Empty;
    public string Shop { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaidDate { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════
//  2. SHOPS ACTIVITY REPORT
// ═══════════════════════════════════════════════════════════════

public class SAShopsReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public int NewShops { get; set; }
    public int TotalShops { get; set; }
    public int ActiveShops { get; set; }
    public int InactiveShops { get; set; }
    public string Interpretation { get; set; } = string.Empty;
    public List<SAShopMonthlyRow> MonthlyBreakdown { get; set; } = new();
    public List<SAShopRow> Shops { get; set; } = new();
}

public class SAShopMonthlyRow
{
    public string Month { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class SAShopRow
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Users { get; set; }
    public string Created { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════
//  3. USER ACTIVITY REPORT
// ═══════════════════════════════════════════════════════════════

public class SAUsersReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public int NewUsers { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int RoleCount { get; set; }
    public string Interpretation { get; set; } = string.Empty;
    public List<SARoleBreakdownRow> RoleBreakdown { get; set; } = new();
    public List<SAUserRow> Users { get; set; } = new();
}

public class SARoleBreakdownRow
{
    public string Role { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class SAUserRow
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Shop { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════
//  4. SUBSCRIPTION OVERVIEW REPORT
// ═══════════════════════════════════════════════════════════════

public class SASubscriptionReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public int NewSubscriptions { get; set; }
    public int ActiveSubscriptions { get; set; }
    public decimal MRR { get; set; }
    public decimal AvgPrice { get; set; }
    public string Interpretation { get; set; } = string.Empty;
    public List<SAPlanBreakdownRow> PlanBreakdown { get; set; } = new();
    public List<SASubscriptionRow> Subscriptions { get; set; } = new();
}

public class SAPlanBreakdownRow
{
    public string Plan { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class SASubscriptionRow
{
    public string Shop { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public string Cycle { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════
//  5. PAYMENT HISTORY REPORT
// ═══════════════════════════════════════════════════════════════

public class SAPaymentReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public decimal TotalCollected { get; set; }
    public decimal TotalPending { get; set; }
    public int FailedCount { get; set; }
    public int TransactionCount { get; set; }
    public string Interpretation { get; set; } = string.Empty;
    public List<SAPaymentMethodRow> MethodBreakdown { get; set; } = new();
    public List<SAPaymentMonthlyRow> MonthlyBreakdown { get; set; } = new();
    public List<SAPaymentRow> Payments { get; set; } = new();
}

public class SAPaymentMethodRow
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class SAPaymentMonthlyRow
{
    public string Month { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class SAPaymentRow
{
    public string Reference { get; set; } = string.Empty;
    public string Shop { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════
//  6. GROWTH ANALYTICS REPORT
// ═══════════════════════════════════════════════════════════════

public class SAGrowthReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public int TotalShops { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveSubscriptions { get; set; }
    public decimal LifetimeRevenue { get; set; }
    public string Interpretation { get; set; } = string.Empty;
    public List<SAGrowthMonthRow> MonthlyGrowth { get; set; } = new();
}

public class SAGrowthMonthRow
{
    public string Month { get; set; } = string.Empty;
    public int CumulativeShops { get; set; }
    public string Change { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════
//  LEGACY — kept for CSV/PDF export compatibility
// ═══════════════════════════════════════════════════════════════

public class ReportsIndexViewModel
{
    public string SelectedReport { get; set; } = "revenue";
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }

    public decimal TotalRevenue { get; set; }
    public decimal AveragePerShop { get; set; }
    public int TotalTransactions { get; set; }
    public List<ReportTableRow> TableRows { get; set; } = new();
    public List<ChartDataPoint> ChartData { get; set; } = new();

    public List<ReportSummaryCard> SummaryCards { get; set; } = new();
}

public class ReportSummaryCard
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Icon { get; set; } = "bar-chart";
    public string Color { get; set; } = "#6366f1";
}

public class ReportTableRow
{
    public string[] Cells { get; set; } = Array.Empty<string>();
}

public class ReportExportRequest
{
    public string Report { get; set; } = "revenue";
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
