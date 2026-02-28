using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.DTOs.JobOrders;

// ── List item ───────────────────────────────────────────────────────────
public class JobOrderListItemDto
{
    public long JobOrderId { get; set; }
    public string JobOrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DeviceSummary { get; set; } = string.Empty;  // "Laptop - Lenovo ThinkPad"
    public string Status { get; set; } = string.Empty;
    public string? TechnicianName { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Detail ──────────────────────────────────────────────────────────────
public class JobOrderDetailDto
{
    public long JobOrderId { get; set; }
    public string JobOrderNo { get; set; } = string.Empty;
    public string ProblemReported { get; set; } = string.Empty;
    public string? DiagnosisNotes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public DateTime? EstimatedCompletionDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Customer
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }

    // Device
    public long DeviceId { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? SerialNo { get; set; }
    public string? DeviceAccessories { get; set; }

    // Assignment
    public string CreatedByName { get; set; } = string.Empty;
    public string? TechnicianName { get; set; }
    public long? AssignedTechUserId { get; set; }

    // Line items
    public List<JobOrderServiceLineDto> Services { get; set; } = new();
    public List<JobOrderPartLineDto> Parts { get; set; } = new();

    // Timeline
    public List<StatusHistoryDto> Timeline { get; set; } = new();

    // Invoice reference (if exists)
    public long? InvoiceId { get; set; }
    public string? InvoiceNo { get; set; }
}

public class JobOrderServiceLineDto
{
    public long JobOrderServiceId { get; set; }
    public long ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class JobOrderPartLineDto
{
    public long JobOrderPartId { get; set; }
    public long ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int QtyUsed { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class StatusHistoryDto
{
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string ChangedByName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Remarks { get; set; }
}

// ── Create request ──────────────────────────────────────────────────────
public class CreateJobOrderRequest
{
    [Required]
    public long CustomerId { get; set; }

    /// <summary>If DeviceId > 0, use existing device; otherwise create new from DeviceInfo.</summary>
    public long? DeviceId { get; set; }

    /// <summary>Required when DeviceId is null/0 (new device).</summary>
    public CreateDeviceDto? NewDevice { get; set; }

    [Required, MaxLength(255)]
    public string ProblemReported { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? DiagnosisNotes { get; set; }

    /// <summary>Optional technician to assign at creation.</summary>
    public long? AssignedTechUserId { get; set; }

    public string Priority { get; set; } = "Normal";
    public DateTime? EstimatedCompletionDate { get; set; }

    /// <summary>Optional service lines to add at creation.</summary>
    public List<AddServiceLineDto>? Services { get; set; }

    /// <summary>Optional part lines to add at creation.</summary>
    public List<AddPartLineDto>? Parts { get; set; }
}

public class CreateDeviceDto
{
    [Required, MaxLength(50)]
    public string DeviceType { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Brand { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Model { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? SerialNo { get; set; }

    [MaxLength(255)]
    public string? Notes { get; set; }
}

public class AddServiceLineDto
{
    [Required]
    public long ServiceId { get; set; }
    [Range(1, int.MaxValue)]
    public int Qty { get; set; } = 1;
    /// <summary>Optional. If omitted or zero, auto-resolves from ServiceCatalog.BasePrice.</summary>
    public decimal? OverridePrice { get; set; }
    /// <summary>Required when OverridePrice differs from catalog price.</summary>
    [MaxLength(255)]
    public string? OverrideReason { get; set; }
}

public class AddPartLineDto
{
    [Required]
    public long ItemId { get; set; }
    [Range(1, int.MaxValue)]
    public int QtyUsed { get; set; } = 1;
    /// <summary>Optional. If omitted or zero, auto-resolves from InventoryItem.UnitPrice (with markup).</summary>
    public decimal? OverridePrice { get; set; }
    /// <summary>Required when OverridePrice differs from catalog price.</summary>
    [MaxLength(255)]
    public string? OverrideReason { get; set; }
}

// ── Assign technician ───────────────────────────────────────────────────
public class AssignTechnicianRequest
{
    [Required]
    public long TechnicianUserId { get; set; }
}

// ── Update status ───────────────────────────────────────────────────────
public class UpdateJobOrderStatusRequest
{
    [Required, MaxLength(30)]
    public string NewStatus { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Remarks { get; set; }
}

// ── Paged request with status filter ────────────────────────────────────
public class JobOrderPagedRequest : DTOs.Common.PagedRequest
{
    public string? StatusFilter { get; set; }
}
