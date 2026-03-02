namespace ByteBill_BS.Models;

/// <summary>
/// Tracks SuperAdmin-level actions (shop created, user suspended, plan changed, etc.).
/// </summary>
public class SuperAdminAuditLog
{
    public long AuditId { get; set; }
    public long UserId { get; set; }

    /// <summary>ShopCreated, ShopSuspended, UserCreated, PlanChanged, SettingsUpdated, etc.</summary>
    public string Action { get; set; } = string.Empty;

    public string? EntityType { get; set; }     // Shop, User, Subscription, Setting
    public long? EntityId { get; set; }
    public string? Details { get; set; }         // JSON or descriptive text
    public string? IpAddress { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
}
