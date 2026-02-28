namespace ByteBill_BS.Models;

public class JobOrderService
{
    public long JobOrderServiceId { get; set; }
    public long JobOrderId { get; set; }
    public long ServiceId { get; set; }
    public int Qty { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal CatalogPrice { get; set; }
    public bool IsPriceOverride { get; set; }
    public string? OverrideReason { get; set; }
    public decimal LineTotal { get; private set; } // Computed by DB: Qty * UnitPrice

    // Navigation properties
    public JobOrder? JobOrder { get; set; }
    public ServiceCatalog? Service { get; set; }
}
