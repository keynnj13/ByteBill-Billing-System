using ByteBill_BS.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.JobOrders;

public class JobOrderListViewModel
{
    public List<JobOrderItemViewModel> JobOrders { get; set; } = new();
    public string? SearchTerm { get; set; }
    public JobOrderStatus? StatusFilter { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class JobOrderItemViewModel
{
    public long Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string JobOrderNumber { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerInitials { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string DeviceInfo { get; set; } = string.Empty;
    public string? DeviceBrand { get; set; }
    public string? DeviceModel { get; set; }
    public string? Brand { get; set; }
    public JobOrderStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public string StatusClass => Status switch
    {
        JobOrderStatus.Pending => "status-pending",
        JobOrderStatus.CheckedIn => "status-info",
        JobOrderStatus.Diagnosis => "status-info",
        JobOrderStatus.InProgress => "status-primary",
        JobOrderStatus.WaitingForParts => "status-warning",
        JobOrderStatus.Completed => "status-success",
        JobOrderStatus.Delivered => "status-delivered",
        JobOrderStatus.Cancelled => "status-cancelled",
        _ => "status-muted"
    };
    public string? Priority { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? AssignedTo { get; set; }
    public string? TechnicianName { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public string? AssignedTechInitials { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public string? ProblemDescription { get; set; }
}

public class JobOrderDetailViewModel
{
    public long Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string JobOrderNumber { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    
    // Customer
    public long CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerInitials { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    
    // Device
    public long DeviceId { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? DeviceBrand { get; set; }
    public string? DeviceModel { get; set; }
    public string? SerialNumber { get; set; }
    public string? DeviceSerial { get; set; }
    public string? DeviceAccessories { get; set; }
    public string DeviceInfo { get; set; } = string.Empty;
    
    // Status
    public JobOrderStatus Status { get; set; }
    public string StatusDisplay => Status.ToString();
    public string StatusClass => Status switch
    {
        JobOrderStatus.Pending => "status-pending",
        JobOrderStatus.CheckedIn => "status-info",
        JobOrderStatus.Diagnosis => "status-info",
        JobOrderStatus.InProgress => "status-primary",
        JobOrderStatus.WaitingForParts => "status-warning",
        JobOrderStatus.Completed => "status-success",
        JobOrderStatus.Delivered => "status-delivered",
        JobOrderStatus.Cancelled => "status-cancelled",
        _ => "status-muted"
    };
    
    // Problem & Diagnosis
    public string ProblemDescription { get; set; } = string.Empty;
    public string ProblemReported { get; set; } = string.Empty;
    public string? IssueDescription { get; set; }
    public string? DiagnosisNotes { get; set; }
    public string? TechnicianNotes { get; set; }
    public string? Priority { get; set; }
    
    // Assignment
    public long? TechnicianId { get; set; }
    public long? AssignedTechnicianId { get; set; }
    public string? AssignedTechnician { get; set; }
    public string? TechnicianName { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public string? AssignedTechInitials { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    
    // Cost
    public decimal EstimatedCost { get; set; }
    public decimal? FinalCost { get; set; }
    public decimal TotalServiceCost { get; set; }
    public decimal TotalPartsCost { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    
    // Dates
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DiagnosedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? EstimatedCompletionDate { get; set; }
    
    // Invoice
    public long? InvoiceId { get; set; }
    public bool HasInvoice => InvoiceId.HasValue;
    
    // Related Items
    public List<JobOrderServiceItemViewModel> Services { get; set; } = new();
    public List<JobOrderPartItemViewModel> Parts { get; set; } = new();
    public List<JobOrderItemLineViewModel> Items { get; set; } = new();
    public List<TimelineEventViewModel> Timeline { get; set; } = new();
    public List<LineItem> LineItems { get; set; } = new();
    
    public class LineItem
    {
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = "Service";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }
}

public class JobOrderServiceItemViewModel
{
    public long Id { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public class JobOrderPartItemViewModel
{
    public long Id { get; set; }
    public string PartName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public class JobOrderItemLineViewModel
{
    public long Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public bool IsService { get; set; }
}

public class TimelineEventViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Icon { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public string? Status { get; set; }
    public string? CompletedBy { get; set; }
}

public class JobOrderTimelineItem
{
    public string Icon { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class JobOrderCreateViewModel
{
    public long Id { get; set; }
    public int CurrentStep { get; set; } = 1;
    
    [Required(ErrorMessage = "Customer is required")]
    [Display(Name = "Customer")]
    public long CustomerId { get; set; }
    
    [Required(ErrorMessage = "Device type is required")]
    [StringLength(50)]
    [Display(Name = "Device Type")]
    public string DeviceType { get; set; } = string.Empty;
    
    [StringLength(50)]
    [Display(Name = "Brand")]
    public string? Brand { get; set; }
    
    [StringLength(50)]
    [Display(Name = "Model")]
    public string? DeviceModel { get; set; }
    
    [StringLength(100)]
    [Display(Name = "Serial Number")]
    public string? SerialNumber { get; set; }
    
    [StringLength(200)]
    [Display(Name = "Accessories")]
    public string? DeviceAccessories { get; set; }
    
    [StringLength(100)]
    [Display(Name = "Device Serial")]
    public string? DeviceSerial { get; set; }
    
    [Required(ErrorMessage = "Problem description is required")]
    [StringLength(2000)]
    [Display(Name = "Problem Description")]
    public string ProblemDescription { get; set; } = string.Empty;
    
    [StringLength(2000)]
    [Display(Name = "Issue Description")]
    public string? IssueDescription { get; set; }
    
    [Display(Name = "Priority")]
    public string? Priority { get; set; }
    
    [Display(Name = "Assigned Technician")]
    public long? AssignedTechnicianId { get; set; }
    
    [Display(Name = "Estimated Completion Date")]
    public DateTime? EstimatedCompletionDate { get; set; }
    
    // Dropdown lists
    public List<CustomerSelectItem> AvailableCustomers { get; set; } = new();
    public List<CustomerSelectItem> Customers { get; set; } = new();
    public List<TechnicianSelectItem> AvailableTechnicians { get; set; } = new();
    public List<TechnicianSelectItem> Technicians { get; set; } = new();
}

public class CustomerSelectItem
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class TechnicianSelectItem
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ActiveJobOrders { get; set; }
}

public class JobOrderFormViewModel
{
    public long Id { get; set; }
    
    [Required(ErrorMessage = "Customer is required")]
    [Display(Name = "Customer")]
    public long CustomerId { get; set; }
    
    [Required(ErrorMessage = "Device is required")]
    [Display(Name = "Device")]
    public long DeviceId { get; set; }
    
    [Required(ErrorMessage = "Device type is required")]
    [StringLength(50)]
    [Display(Name = "Device Type")]
    public string DeviceType { get; set; } = string.Empty;
    
    [StringLength(50)]
    [Display(Name = "Brand")]
    public string? Brand { get; set; }
    
    [StringLength(50)]
    [Display(Name = "Model")]
    public string? DeviceModel { get; set; }
    
    [StringLength(100)]
    [Display(Name = "Serial Number")]
    public string? SerialNumber { get; set; }
    
    [Required(ErrorMessage = "Problem description is required")]
    [StringLength(2000)]
    [Display(Name = "Problem Description")]
    public string ProblemDescription { get; set; } = string.Empty;
    
    [StringLength(2000)]
    [Display(Name = "Diagnosis Notes")]
    public string? DiagnosisNotes { get; set; }
    
    [Display(Name = "Assigned Technician")]
    public long? AssignedTechnicianId { get; set; }
    
    [Display(Name = "Estimated Cost")]
    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }
}
