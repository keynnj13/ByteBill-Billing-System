using ByteBill_BS.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.Models;

public class Payment
{
    public long PaymentId { get; set; }
    public long ShopId { get; set; }

    [MaxLength(50)]
    public string PaymentNo { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Range(0.01, (double)decimal.MaxValue)]
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [MaxLength(100)]
    public string? ReferenceNo { get; set; }
    public long ReceivedByUserId { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public Shop? Shop { get; set; }
    public Customer? Customer { get; set; }
    public User? ReceivedByUser { get; set; }
    public PayMongoTxn? PayMongoTxn { get; set; }
    public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
    public ICollection<AccountingEntry> AccountingEntries { get; set; } = new List<AccountingEntry>();
    public ICollection<XeroSyncLog> XeroSyncLogs { get; set; } = new List<XeroSyncLog>();
}
