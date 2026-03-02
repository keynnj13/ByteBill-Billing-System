namespace ByteBill_BS.Models;

/// <summary>
/// Broadcast messages from SuperAdmin to all shops.
/// </summary>
public class Announcement
{
    public long AnnouncementId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>Info, Warning, Critical, Maintenance</summary>
    public string Type { get; set; } = "Info";

    /// <summary>Draft, Published, Archived</summary>
    public string Status { get; set; } = "Draft";

    public DateTime? PublishedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public User? CreatedBy { get; set; }
}
