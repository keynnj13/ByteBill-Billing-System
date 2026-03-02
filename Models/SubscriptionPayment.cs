namespace ByteBill_BS.Models;

/// <summary>
/// Records each payment made for a subscription (via PayMongo).
/// </summary>
public class SubscriptionPayment
{
    public long SubscriptionPaymentId { get; set; }
    public long SubscriptionId { get; set; }
    public long ShopId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PHP";

    /// <summary>Paid, Pending, Failed, Refunded</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>PayMongo payment method: card, gcash, grab_pay, paymaya</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>System-generated reference: SUBPAY-YYYYMMDD-XXXX</summary>
    public string ReferenceNumber { get; set; } = string.Empty;

    /// <summary>PayMongo checkout session / payment intent ID</summary>
    public string? PayMongoPaymentId { get; set; }
    public string? PayMongoCheckoutUrl { get; set; }

    /// <summary>Billing period this payment covers</summary>
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    // Navigation
    public Subscription? Subscription { get; set; }
    public Shop? Shop { get; set; }
}
