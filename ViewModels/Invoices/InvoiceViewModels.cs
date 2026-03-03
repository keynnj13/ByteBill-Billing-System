using ByteBill_BS.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Invoices;

public class InvoiceListViewModel
{
    public List<InvoiceItemViewModel> Invoices { get; set; } = new();
    public string? SearchTerm { get; set; }
    public InvoiceStatus? StatusFilter { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    public decimal TotalOutstanding { get; set; }
    public int OverdueCount { get; set; }
}

public class InvoiceItemViewModel
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerInitials { get; set; } = string.Empty;
    public string? JobNumber { get; set; }
    public InvoiceStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public string StatusClass => Status switch
    {
        InvoiceStatus.Unpaid => "status-danger",
        InvoiceStatus.Partial => "status-partial",
        InvoiceStatus.Paid => "status-success",
        InvoiceStatus.Void => "status-muted",
        _ => "status-muted"
    };
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAdjustments { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.Today && Status != InvoiceStatus.Paid && Status != InvoiceStatus.Void;

    /// <summary>Original invoice amount before deductions (Subtotal + positive adjustments).</summary>
    public decimal GrossTotal => Subtotal + Math.Max(0, TotalAdjustments);

    /// <summary>Total settled amount: actual payments + discounts + credit adjustments.</summary>
    public decimal EffectivePaid => AmountPaid + Math.Max(0, GrossTotal - Total);
}

public class InvoiceDetailViewModel
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    
    // Customer
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerAddress { get; set; }
    
    // Job Order
    public long JobOrderId { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string JobOrderNumber { get; set; } = string.Empty;
    
    // Shop
    public string ShopName { get; set; } = string.Empty;
    public string? ShopAddress { get; set; }
    public string? ShopPhone { get; set; }
    public string? ShopEmail { get; set; }
    
    // Status
    public InvoiceStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public string StatusClass => Status switch
    {
        InvoiceStatus.Unpaid => "status-danger",
        InvoiceStatus.Partial => "status-partial",
        InvoiceStatus.Paid => "status-success",
        InvoiceStatus.Void => "status-muted",
        _ => "status-muted"
    };
    
    // Amounts
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatableSales { get; set; }
    public decimal VatExemptSales { get; set; }
    public decimal ZeroRatedSales { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAdjustments { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }

    /// <summary>Original invoice amount before deductions (Subtotal + positive adjustments).</summary>
    public decimal GrossTotal => Subtotal + Math.Max(0, TotalAdjustments);

    /// <summary>Total settled amount: actual payments + discounts + credit adjustments.</summary>
    public decimal EffectivePaid => AmountPaid + Math.Max(0, GrossTotal - Total);
    
    // Shop Tax Info
    public string? ShopTIN { get; set; }
    public bool IsVatRegistered { get; set; }
    
    // Discounts
    public List<InvoiceDiscountViewModel> Discounts { get; set; } = new();
    
    // Dates
    public DateTime CreatedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    
    public string? Notes { get; set; }
    
    // Line Items
    public List<InvoiceLineItemViewModel> LineItems { get; set; } = new();
    
    // Payments
    public List<PaymentSummaryViewModel> Payments { get; set; } = new();
}

public class InvoiceLineItemViewModel
{
    public long Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public string Type { get; set; } = "Service";
}

public class PaymentSummaryViewModel
{
    public long Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public DateTime PaidAt { get; set; }
    public bool IsVoid { get; set; }
    public string? Reference { get; set; }
    public string? ReceivedBy { get; set; }
}

public class InvoiceCreateViewModel
{
    public long CustomerId { get; set; }
    public long JobOrderId { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    
    public List<CustomerOption> Customers { get; set; } = new();
    public List<ServiceOption> AvailableServices { get; set; } = new();
    public List<AvailableJobOrderOption> AvailableJobOrders { get; set; } = new();
    
    public List<InvoiceLineItemFormViewModel> LineItems { get; set; } = new();
    public List<LineItemInput> Items { get; set; } = new();
    
    [Display(Name = "Tax Rate (%)")]
    [Range(0, 100)]
    public decimal TaxRate { get; set; }
    
    [Display(Name = "Discount Amount")]
    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; }
    
    [Display(Name = "Due Date")]
    public DateTime? DueDate { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    public class CustomerOption
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
    }
    
    public class ServiceOption
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
    }
    
    public class LineItemInput
    {
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public string Type { get; set; } = "Service";
    }
}

public class InvoiceLineItemFormViewModel
{
    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
    
    [Required]
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}

public class AvailableJobOrderOption
{
    public long JobOrderId { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DeviceSummary { get; set; } = string.Empty;
}

public class InvoiceDiscountViewModel
{
    public long InvoiceDiscountId { get; set; }
    public string DiscountType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public decimal Amount { get; set; }
    public bool IsVatExempt { get; set; }
    public string? BeneficiaryIdNo { get; set; }
    public string? BeneficiaryName { get; set; }
    public DateTime AppliedAt { get; set; }
}
