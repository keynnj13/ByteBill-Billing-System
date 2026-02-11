namespace ByteBill_BS.Models;

public class XeroSyncLog
{
    public long XeroSyncLogId { get; set; }
    public long ShopId { get; set; }
    public long? SyncedByUserId { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public long? InvoiceId { get; set; }
    public long? PaymentId { get; set; }
    public long? AccountingEntryId { get; set; }
    public string? XeroRecordId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Message { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Shop? Shop { get; set; }
    public User? SyncedByUser { get; set; }
    public Invoice? Invoice { get; set; }
    public Payment? Payment { get; set; }
    public AccountingEntry? AccountingEntry { get; set; }
}
