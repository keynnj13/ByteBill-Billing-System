namespace ByteBill_BS.Models;

public class InventoryCategory
{
    public long InventoryCategoryId { get; set; }
    public long ShopId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsArchived { get; set; }

    // Navigation properties
    public Shop? Shop { get; set; }
    public ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}
