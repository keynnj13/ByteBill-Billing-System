namespace ByteBill_BS.Models;

public class Device
{
    public long DeviceId { get; set; }
    public long CustomerId { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? SerialNo { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public Customer? Customer { get; set; }
    public ICollection<JobOrder> JobOrders { get; set; } = new List<JobOrder>();
}
