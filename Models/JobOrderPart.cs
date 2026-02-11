namespace ByteBill_BS.Models;

public class JobOrderPart
{
    public long JobOrderPartId { get; set; }
    public long JobOrderId { get; set; }
    public long ItemId { get; set; }
    public int QtyUsed { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; private set; } // Computed by DB: QtyUsed * UnitPrice

    // Navigation properties
    public JobOrder? JobOrder { get; set; }
    public InventoryItem? Item { get; set; }
}
