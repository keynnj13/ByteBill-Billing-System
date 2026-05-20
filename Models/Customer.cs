namespace ByteBill_BS.Models;

public class Customer
{
    public long CustomerId { get; set; }
    public long ShopId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? EmailHash { get; set; }
    public string? Phone { get; set; }
    public string? PhoneHash { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Shop? Shop { get; set; }
    public ICollection<Device> Devices { get; set; } = new List<Device>();
    public ICollection<JobOrder> JobOrders { get; set; } = new List<JobOrder>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    // Computed
    public string FullName => $"{FirstName} {LastName}";
    public string Initials => $"{(FirstName.Length > 0 ? FirstName[0] : ' ')}{(LastName.Length > 0 ? LastName[0] : ' ')}".Trim().ToUpper();
}
