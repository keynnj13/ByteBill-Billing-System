using ByteBill_BS.Models.Enums;

namespace ByteBill_BS.Models;

public class CreditDebitAdjustment
{
    public long AdjustmentId { get; set; }
    public long InvoiceId { get; set; }
    public long CreatedByUserId { get; set; }
    public AdjustmentType AdjustmentType { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Invoice? Invoice { get; set; }
    public User? CreatedByUser { get; set; }
}
