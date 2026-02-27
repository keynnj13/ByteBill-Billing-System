namespace ByteBill_BS.Models;

public class Notification
{
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public long ShopId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info";          // info, adjustment, approved, rejected
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
    public Shop? Shop { get; set; }
}
