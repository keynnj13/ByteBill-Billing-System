using ByteBill_BS.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Payments;

public class PaymentListViewModel
{
    public List<PaymentItemViewModel> Payments { get; set; } = new();
    public string? SearchTerm { get; set; }
    public PaymentMethod? MethodFilter { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    public decimal TotalReceived { get; set; }
    public decimal TodayReceived { get; set; }
}

public class PaymentItemViewModel
{
    public long Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerInitials { get; set; } = string.Empty;
    public string? InvoiceNumber { get; set; }
    public long? InvoiceId { get; set; }
    public PaymentMethod Method { get; set; }
    public string MethodDisplay => Method.ToString();
    public string MethodClass => Method switch
    {
        PaymentMethod.Cash => "status-success",
        PaymentMethod.GCash => "status-primary",
        PaymentMethod.Card => "status-info",
        _ => "status-muted"
    };
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? ReferenceNo { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ReceivedBy { get; set; }
    public string? ReceivedByName { get; set; }
    public PaymentStatus Status { get; set; }
    public bool IsVoid { get; set; }
}

public class PaymentDetailViewModel
{
    public long Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    
    // Customer
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    
    // Invoice
    public long? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    
    // Payment Details
    public PaymentMethod Method { get; set; }
    public string MethodDisplay => Method.ToString();
    public decimal Amount { get; set; }
    public string? ReferenceNo { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime PaidAt { get; set; }
    public DateTime PaymentDate { get; set; }
    
    // Staff
    public string? ReceivedBy { get; set; }
    public string? ReceivedByName { get; set; }
    public string? ProcessedBy { get; set; }
    
    // Status
    public PaymentStatus Status { get; set; }
    public bool IsVoid { get; set; }
    public string? Notes { get; set; }
    
    // Allocations
    public List<PaymentAllocationItem> Allocations { get; set; } = new();
}

public class PaymentAllocationItem
{
    public long InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal AmountApplied { get; set; }
}

public class PaymentCreateViewModel
{
    public long Id { get; set; }
    
    [Required(ErrorMessage = "Invoice is required")]
    [Display(Name = "Invoice")]
    public long InvoiceId { get; set; }
    
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal InvoiceBalance { get; set; }
    
    public List<AvailableInvoiceOption> AvailableInvoices { get; set; } = new();
    
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }
    
    [Required(ErrorMessage = "Payment method is required")]
    [Display(Name = "Payment Method")]
    public PaymentMethod Method { get; set; }
    
    [StringLength(100)]
    [Display(Name = "Reference Number")]
    public string? ReferenceNumber { get; set; }
    
    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }
}

public class PaymentFormViewModel
{
    public long Id { get; set; }
    
    [Required(ErrorMessage = "Customer is required")]
    [Display(Name = "Customer")]
    public long CustomerId { get; set; }
    
    [Display(Name = "Invoice")]
    public long? InvoiceId { get; set; }
    
    [Required(ErrorMessage = "Amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }
    
    [Required(ErrorMessage = "Payment method is required")]
    [Display(Name = "Payment Method")]
    public PaymentMethod Method { get; set; }
    
    [StringLength(100)]
    [Display(Name = "Reference Number")]
    public string? ReferenceNo { get; set; }
    
    [Display(Name = "Payment Date")]
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    
    [StringLength(500)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }
}

public class AvailableInvoiceOption
{
    public long InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}
