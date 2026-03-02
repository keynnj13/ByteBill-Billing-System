namespace ByteBill_BS.Models;

/// <summary>
/// Defines available subscription tiers (Basic, Professional, Enterprise).
/// </summary>
public class SubscriptionPlan
{
    public long PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;        // Basic, Professional, Enterprise
    public string? Description { get; set; }

    // ── Pricing ──────────────────────────────────────────────────
    public decimal MonthlyPrice { get; set; }
    public decimal YearlyPrice { get; set; }        // 20% discount baked in
    public decimal PermanentPrice { get; set; }     // 36× monthly

    // ── Tier Limits ──────────────────────────────────────────────
    public int MaxUsers { get; set; }               // 0 = unlimited
    public int MaxCustomers { get; set; }           // 0 = unlimited
    public int MaxJobOrdersPerMonth { get; set; }   // 0 = unlimited
    public bool HasXeroIntegration { get; set; }
    public bool HasPrioritySupport { get; set; }
    public bool HasAdvancedReports { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
