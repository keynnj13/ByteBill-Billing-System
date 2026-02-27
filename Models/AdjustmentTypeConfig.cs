namespace ByteBill_BS.Models;

public class AdjustmentTypeConfig
{
    public long AdjustmentTypeConfigId { get; set; }
    public long ShopId { get; set; }
    public string Name { get; set; } = string.Empty;           // e.g. "Senior Citizen Discount"
    public string Category { get; set; } = "Credit";           // Credit, Debit, Refund
    public decimal Percentage { get; set; }                     // e.g. 20.00 for 20%
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Shop? Shop { get; set; }
}
