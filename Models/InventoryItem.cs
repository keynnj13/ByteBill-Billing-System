using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.Models;

public class InventoryItem
{
    public long ItemId { get; set; }
    public long ShopId { get; set; }
    public long? InventoryCategoryId { get; set; }

    [Required, MaxLength(50)]
    public string SKU { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Unit { get; set; } = string.Empty;

    [Range(0, (double)decimal.MaxValue)]
    public decimal UnitCost { get; set; }

    [Range(0, (double)decimal.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, int.MaxValue)]
    public int QtyOnHand { get; set; }

    [Range(0, int.MaxValue)]
    public int ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Shop? Shop { get; set; }
    public InventoryCategory? InventoryCategory { get; set; }
    public ICollection<InventoryTxn> Transactions { get; set; } = new List<InventoryTxn>();
    public ICollection<JobOrderPart> JobOrderParts { get; set; } = new List<JobOrderPart>();

    // Computed
    public bool IsLowStock => QtyOnHand <= ReorderLevel;
}
