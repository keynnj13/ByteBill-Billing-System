using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.DTOs.Payments;

// ── List item ───────────────────────────────────────────────────────────
public class PaymentListItemDto
{
    public long PaymentId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? InvoiceNo { get; set; }    // first allocated invoice (if single)
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string ReceivedByName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReferenceNo { get; set; }
    public string? Notes { get; set; }
}

// ── Detail ──────────────────────────────────────────────────────────────
public class PaymentDetailDto
{
    public long PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string? ReferenceNo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string ReceivedByName { get; set; } = string.Empty;

    public List<PaymentAllocationDto> Allocations { get; set; } = new();
}

public class PaymentAllocationDto
{
    public long PaymentAllocationId { get; set; }
    public long InvoiceId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public decimal AmountApplied { get; set; }
}

// ── Dashboard metrics ───────────────────────────────────────────────────
public class PaymentMetricsDto
{
    public decimal TotalReceived { get; set; }   // sum confirmed
    public decimal TodayReceived { get; set; }   // sum confirmed today
    public int Transactions { get; set; }         // count all
}

// ── Record payment request ──────────────────────────────────────────────
public class RecordPaymentRequest
{
    [Required]
    public long CustomerId { get; set; }

    [Required, Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive.")]
    public decimal Amount { get; set; }

    [Required, MaxLength(30)]
    public string Method { get; set; } = "Cash";

    [MaxLength(60)]
    public string? ReferenceNo { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Allocations to invoices. Sum of AmountApplied must equal Amount.
    /// At least one allocation is required.
    /// </summary>
    [Required, MinLength(1, ErrorMessage = "At least one invoice allocation is required.")]
    public List<PaymentAllocationRequestDto> Allocations { get; set; } = new();
}

public class PaymentAllocationRequestDto
{
    [Required]
    public long InvoiceId { get; set; }

    [Required, Range(0.01, double.MaxValue)]
    public decimal AmountApplied { get; set; }
}

// ── Paged request with status filter ────────────────────────────────────
public class PaymentPagedRequest : DTOs.Common.PagedRequest
{
    public string? StatusFilter { get; set; }
}
