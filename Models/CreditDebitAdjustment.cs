using ByteBill_BS.Models.Enums;

namespace ByteBill_BS.Models;

public class CreditDebitAdjustment
{
    public long AdjustmentId { get; set; }
    public long ShopId { get; set; }
    public long InvoiceId { get; set; }
    public long CreatedByUserId { get; set; }
    public long? ReviewedByUserId { get; set; }
    public AdjustmentType AdjustmentType { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AdjustmentStatus Status { get; set; } = AdjustmentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    // Navigation properties
    public Shop? Shop { get; set; }
    public Invoice? Invoice { get; set; }
    public User? CreatedByUser { get; set; }
    public User? ReviewedByUser { get; set; }
}
