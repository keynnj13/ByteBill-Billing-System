namespace ByteBill_BS.Models;

public class PasswordResetToken
{
    public long PasswordResetTokenId { get; set; }
    public long UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? RequestedIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
