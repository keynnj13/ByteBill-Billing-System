namespace ByteBill_BS.Models;

public class JobOrderStatusHistory
{
    public long JobOrderStatusHistoryId { get; set; }
    public long JobOrderId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public long ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? Remarks { get; set; }

    // Navigation properties
    public JobOrder? JobOrder { get; set; }
    public User? ChangedByUser { get; set; }
}
