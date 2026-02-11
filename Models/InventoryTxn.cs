using ByteBill_BS.Models.Enums;

namespace ByteBill_BS.Models;

public class InventoryTxn
{
    public long InventoryTxnId { get; set; }
    public long ItemId { get; set; }
    public InventoryTxnType TxnType { get; set; }
    public int Quantity { get; set; }
    public string? ReferenceType { get; set; }
    public long? ReferenceId { get; set; }
    public string? Remarks { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public InventoryItem? Item { get; set; }
}
