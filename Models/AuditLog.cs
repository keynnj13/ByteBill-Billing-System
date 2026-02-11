namespace ByteBill_BS.Models;

public class AuditLog
{
    public long AuditLogId { get; set; }
    public long ShopId { get; set; }
    public long UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public long EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Shop? Shop { get; set; }
    public User? User { get; set; }
}
