namespace ByteBill_BS.Models;

public class ServiceCatalog
{
    public long ServiceId { get; set; }
    public long ShopId { get; set; }
    public long ServiceCategoryId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Shop? Shop { get; set; }
    public ServiceCategory? ServiceCategory { get; set; }
    public ICollection<JobOrderService> JobOrderServices { get; set; } = new List<JobOrderService>();
}
