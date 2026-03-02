namespace ByteBill_BS.Models;

/// <summary>
/// Tracks a shop's active subscription to a plan with billing cycle.
/// </summary>
public class Subscription
{
    public long SubscriptionId { get; set; }
    public long ShopId { get; set; }
    public long PlanId { get; set; }

    /// <summary>Monthly, Yearly, Permanent</summary>
    public string BillingCycle { get; set; } = "Monthly";

    /// <summary>Active, Expired, Cancelled, PastDue</summary>
    public string Status { get; set; } = "Active";

    public decimal Price { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }          // null for Permanent
    public DateTime? NextBillingDate { get; set; }  // null for Permanent
    public DateTime? CancelledAt { get; set; }

    public bool IsDefault { get; set; }             // true for ByteBill Main Shop

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Shop? Shop { get; set; }
    public SubscriptionPlan? Plan { get; set; }
    public ICollection<SubscriptionPayment> Payments { get; set; } = new List<SubscriptionPayment>();
}
