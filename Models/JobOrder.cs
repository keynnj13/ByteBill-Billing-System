using ByteBill_BS.Models.Enums;

namespace ByteBill_BS.Models;

public class JobOrder
{
    public long JobOrderId { get; set; }
    public long ShopId { get; set; }
    public long CustomerId { get; set; }
    public long DeviceId { get; set; }
    public long CreatedByUserId { get; set; }
    public long? AssignedTechUserId { get; set; }
    public string JobOrderNo { get; set; } = string.Empty;
    public string ProblemReported { get; set; } = string.Empty;
    public string? DiagnosisNotes { get; set; }
    public string Priority { get; set; } = "Normal";
    public DateTime? EstimatedCompletionDate { get; set; }
    public JobOrderStatus Status { get; set; } = JobOrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedDate { get; set; }

    // Navigation properties
    public Shop? Shop { get; set; }
    public Customer? Customer { get; set; }
    public Device? Device { get; set; }
    public User? CreatedByUser { get; set; }
    public User? AssignedTechUser { get; set; }
    public Invoice? Invoice { get; set; }
    public ICollection<JobOrderService> JobOrderServices { get; set; } = new List<JobOrderService>();
    public ICollection<JobOrderPart> JobOrderParts { get; set; } = new List<JobOrderPart>();
    public ICollection<JobOrderStatusHistory> StatusHistory { get; set; } = new List<JobOrderStatusHistory>();
}
