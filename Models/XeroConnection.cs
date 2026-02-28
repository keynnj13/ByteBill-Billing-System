namespace ByteBill_BS.Models;

public class XeroConnection
{
    public long XeroConnectionId { get; set; }
    public long ShopId { get; set; }
    public string XeroTenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Shop? Shop { get; set; }
}
