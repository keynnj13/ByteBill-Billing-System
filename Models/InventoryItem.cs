namespace ByteBill_BS.Models;

public class InventoryItem
{
    public long ItemId { get; set; }
    public long ShopId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Shop? Shop { get; set; }
    public ICollection<InventoryTxn> Transactions { get; set; } = new List<InventoryTxn>();
    public ICollection<JobOrderPart> JobOrderParts { get; set; } = new List<JobOrderPart>();

    // Computed
    public bool IsLowStock => QtyOnHand <= ReorderLevel;
}
