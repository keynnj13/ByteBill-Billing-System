namespace ByteBill_BS.Models;

public class InvoiceLine
{
    public long InvoiceLineId { get; set; }
    public long InvoiceId { get; set; }
    public string LineType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Qty { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal CatalogPrice { get; set; }
    public bool IsPriceOverride { get; set; }
    public string? OverrideReason { get; set; }
    public decimal LineTotal { get; private set; } // Computed by DB: Qty * UnitPrice

    // Navigation properties
    public Invoice? Invoice { get; set; }
}
