namespace ByteBill_BS.Models;

public class AccountingEntry
{
    public long AccountingEntryId { get; set; }
    public long ShopId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public long? SourceInvoiceId { get; set; }
    public long? SourcePaymentId { get; set; }
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public string AccountCode { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Memo { get; set; }

    // Navigation properties
    public Shop? Shop { get; set; }
    public Invoice? SourceInvoice { get; set; }
    public Payment? SourcePayment { get; set; }
    public ICollection<XeroSyncLog> XeroSyncLogs { get; set; } = new List<XeroSyncLog>();
}
