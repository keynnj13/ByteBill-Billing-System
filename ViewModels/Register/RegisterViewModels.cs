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
    private const string NamePattern = @"^[A-Za-z]+(?:[ '-][A-Za-z]+)*$";
    private const string PhMobilePattern = @"^(09\d{9}|\+639\d{9})$";

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

    [StringLength(13)]
    [RegularExpression(PhMobilePattern, ErrorMessage = "Phone must be in 09XXXXXXXXX or +639XXXXXXXXX format")]
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
    [RegularExpression(NamePattern, ErrorMessage = "First name may contain letters, spaces, hyphen, and apostrophe only")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(50)]
    [RegularExpression(NamePattern, ErrorMessage = "Middle name may contain letters, spaces, hyphen, and apostrophe only")]
    [Display(Name = "Middle Name")]
    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50)]
    [RegularExpression(NamePattern, ErrorMessage = "Last name may contain letters, spaces, hyphen, and apostrophe only")]
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
