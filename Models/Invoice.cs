using ByteBill_BS.Models.Enums;

namespace ByteBill_BS.Models;

public class Invoice
{
    public long InvoiceId { get; set; }
    public long ShopId { get; set; }
    public long JobOrderId { get; set; }
    public long CustomerId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public decimal Subtotal { get; set; }
    public decimal TotalAdjustments { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedDate { get; set; }

    // Navigation properties
    public Shop? Shop { get; set; }
    public JobOrder? JobOrder { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();
    public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
    public ICollection<CreditDebitAdjustment> Adjustments { get; set; } = new List<CreditDebitAdjustment>();
    public ICollection<AccountingEntry> AccountingEntries { get; set; } = new List<AccountingEntry>();
    public ICollection<XeroSyncLog> XeroSyncLogs { get; set; } = new List<XeroSyncLog>();
}
