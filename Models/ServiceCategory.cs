namespace ByteBill_BS.Models;

public class ServiceCategory
{
    public long ServiceCategoryId { get; set; }
    public long ShopId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsArchived { get; set; }

    // Navigation properties
    public Shop? Shop { get; set; }
    public ICollection<ServiceCatalog> Services { get; set; } = new List<ServiceCatalog>();
}
