using ByteBill_BS.Models.Enums;

namespace ByteBill_BS.Models;

public class InvoiceDiscount
{
    public long InvoiceDiscountId { get; set; }
    public long InvoiceId { get; set; }
    public DiscountType DiscountType { get; set; }

    /// <summary>Label shown on receipt, e.g. "Senior Citizen (20%)", "Holiday Promo".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Discount percentage (0–100). For fixed amounts, set to 0 and use Amount directly.</summary>
    public decimal Percentage { get; set; }

    /// <summary>Computed discount amount in PHP.</summary>
    public decimal Amount { get; set; }

    /// <summary>SC/PWD: true → discounted amount becomes VAT-exempt.</summary>
    public bool IsVatExempt { get; set; }

    /// <summary>Optional: SC/PWD ID number for BIR compliance.</summary>
    public string? BeneficiaryIdNo { get; set; }

    /// <summary>Optional: Name of the SC/PWD beneficiary.</summary>
    public string? BeneficiaryName { get; set; }

    public long AppliedByUserId { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Invoice? Invoice { get; set; }
    public User? AppliedByUser { get; set; }
}
