namespace ByteBill_BS.Models;

/// <summary>
/// Stores platform-wide configuration (branding, tax defaults, SMTP, PayMongo keys, etc.).
/// Key-value store approach for flexibility.
/// </summary>
public class PlatformSetting
{
    public long SettingId { get; set; }

    /// <summary>Dot-separated key: General.PlatformName, Security.MinPasswordLength, etc.</summary>
    public string SettingKey { get; set; } = string.Empty;

    public string SettingValue { get; set; } = string.Empty;

    /// <summary>General, Security, Email, PayMongo, Tax</summary>
    public string Category { get; set; } = "General";

    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
