using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.DTOs.Invoices;

// ── List item ───────────────────────────────────────────────────────────
public class InvoiceListItemDto
{
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string JobOrderNo { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAdjustments { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
}

// ── Detail ──────────────────────────────────────────────────────────────
public class InvoiceDetailDto
{
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;

    // Totals
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAdjustments { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    // BIR Tax Breakdown
    public decimal VatableSales { get; set; }
    public decimal VatExemptSales { get; set; }
    public decimal ZeroRatedSales { get; set; }
    public decimal VatAmount { get; set; }

    // Discounts
    public List<InvoiceDiscountDto> Discounts { get; set; } = new();

    // Customer
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    // Job Order
    public long JobOrderId { get; set; }
    public string JobOrderNo { get; set; } = string.Empty;

    // Lines
    public List<InvoiceLineDto> Lines { get; set; } = new();

    // Adjustments
    public List<AdjustmentDto> Adjustments { get; set; } = new();

    // Payment allocations
    public List<InvoicePaymentDto> Payments { get; set; } = new();
}

public class InvoiceLineDto
{
    public long InvoiceLineId { get; set; }
    public string LineType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class AdjustmentDto
{
    public long AdjustmentId { get; set; }
    public string AdjustmentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class InvoicePaymentDto
{
    public long PaymentId { get; set; }
    public string PaymentNo { get; set; } = string.Empty;
    public decimal AmountApplied { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? ReferenceNo { get; set; }
    public string? ReceivedBy { get; set; }
    public bool IsVoid { get; set; }
}

public class InvoiceDiscountDto
{
    public long InvoiceDiscountId { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public decimal Amount { get; set; }
    public bool IsVatExempt { get; set; }
    public string? BeneficiaryIdNo { get; set; }
    public string? BeneficiaryName { get; set; }
    public string? AppliedByName { get; set; }
    public DateTime AppliedAt { get; set; }
}

// ── Dashboard metrics ───────────────────────────────────────────────────
public class InvoiceMetricsDto
{
    public int TotalInvoices { get; set; }
    public decimal Outstanding { get; set; }  // sum Balance where Balance > 0 and not Void
    public int Overdue { get; set; }           // count where DueDate < today and Balance > 0
}

// ── Create invoice from job order ───────────────────────────────────────
public class CreateInvoiceRequest
{
    [Required]
    public long JobOrderId { get; set; }

    /// <summary>Optional due date. Defaults to 30 days from creation.</summary>
    public DateTime? DueDate { get; set; }
}

// ── Create adjustment ───────────────────────────────────────────────────
public class CreateAdjustmentRequest
{
    [Required, RegularExpression("^(CREDIT|DEBIT)$", ErrorMessage = "AdjustmentType must be CREDIT or DEBIT.")]
    public string AdjustmentType { get; set; } = string.Empty;

    [Required, Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive.")]
    public decimal Amount { get; set; }

    [Required, MaxLength(150)]
    public string Reason { get; set; } = string.Empty;
}

// ── Paged request with status filter ────────────────────────────────────
public class InvoicePagedRequest : DTOs.Common.PagedRequest
{
    public string? StatusFilter { get; set; }
}
