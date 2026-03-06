using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.Models;

public class InvoiceLine
{
    public long InvoiceLineId { get; set; }
    public long InvoiceId { get; set; }

    [Required, MaxLength(50)]
    public string LineType { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Qty { get; set; } = 1;

    [Range(0, (double)decimal.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(0, (double)decimal.MaxValue)]
    public decimal CatalogPrice { get; set; }
    public bool IsPriceOverride { get; set; }

    [MaxLength(500)]
    public string? OverrideReason { get; set; }
    public decimal LineTotal { get; private set; } // Computed by DB: Qty * UnitPrice

    // Navigation properties
    public Invoice? Invoice { get; set; }
}
