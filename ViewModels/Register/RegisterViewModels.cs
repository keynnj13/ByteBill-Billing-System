using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Register;

/// <summary>
/// Shown after user selects a plan from the landing page.
/// Displays plan summary and initiates PayMongo checkout.
/// </summary>
public class CheckoutViewModel
{
    public long PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string? PlanDescription { get; set; }
    public string BillingCycle { get; set; } = "Monthly";   // Monthly, Yearly, Permanent
    public decimal Price { get; set; }
    public string PriceLabel { get; set; } = string.Empty;  // e.g., "₱999/month"

    // Plan limits (display only)
    public int MaxUsers { get; set; }
    public int MaxCustomers { get; set; }
    public int MaxJobOrdersPerMonth { get; set; }
    public bool HasXeroIntegration { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool HasAdvancedReports { get; set; }
}

/// <summary>
/// Registration form shown after successful PayMongo payment.
/// Creates shop + admin user + subscription.
/// </summary>
public class CreateAccountViewModel
{
    /// <summary>PayMongo checkout session ID (from redirect URL).</summary>
    public string? SessionId { get; set; }

    /// <summary>Plan ID (passed through PayMongo metadata).</summary>
    public long PlanId { get; set; }

    /// <summary>Billing cycle (passed through PayMongo metadata).</summary>
    public string BillingCycle { get; set; } = "Monthly";

    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // ── Shop Details ─────────────────────────────────────────────
    [Required(ErrorMessage = "Shop name is required")]
    [StringLength(150, MinimumLength = 2)]
    [Display(Name = "Shop Name")]
    public string ShopName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Shop email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email")]
    [StringLength(100)]
    [Display(Name = "Shop Email")]
    public string ShopEmail { get; set; } = string.Empty;

    [StringLength(11)]
    [RegularExpression(@"^09\d{9}$", ErrorMessage = "Phone must be 11 digits starting with 09")]
    [Display(Name = "Phone Number")]
    public string? ShopPhone { get; set; }

    [StringLength(255)]
    [Display(Name = "Address")]
    public string? ShopAddress { get; set; }

    [StringLength(15)]
    [RegularExpression(@"^\d{3}-\d{3}-\d{3}-\d{3}$", ErrorMessage = "TIN must be in XXX-XXX-XXX-XXX format (12 digits)")]
    [Display(Name = "TIN (Tax Identification Number)")]
    public string? TIN { get; set; }

    [Display(Name = "VAT Registered")]
    public bool IsVatRegistered { get; set; } = true;

    // ── Owner / Admin Account ────────────────────────────────────
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

    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Username can only contain letters, numbers, dots, hyphens, and underscores")]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email")]
    [StringLength(150)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 12, ErrorMessage = "Password must be at least 12 characters")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{12,}$",
        ErrorMessage = "Password must be at least 12 characters and include uppercase, lowercase, number, and special character")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password")]
    [Compare("Password", ErrorMessage = "Passwords do not match")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Result returned by RegistrationService after account creation.
/// </summary>
public class RegistrationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long ShopId { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;

    public static RegistrationResult Fail(string error)
        => new() { Success = false, ErrorMessage = error };
}
