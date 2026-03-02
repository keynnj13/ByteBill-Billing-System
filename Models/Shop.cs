namespace ByteBill_BS.Models;

public class Shop
{
    public long ShopId { get; set; }
    public string ShopCode { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "Active";
    public decimal DefaultPartMarkupPct { get; set; } = 0;

    // ── BIR Tax Settings ─────────────────────────────────────────────
    public string? TIN { get; set; }
    public bool IsVatRegistered { get; set; } = true;
    /// <summary>12% for VAT-registered, 3% for Non-VAT (Percentage Tax).</summary>
    public decimal TaxRate { get; set; } = 12m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<ServiceCategory> ServiceCategories { get; set; } = new List<ServiceCategory>();
    public ICollection<ServiceCatalog> ServiceCatalogs { get; set; } = new List<ServiceCatalog>();
    public ICollection<InventoryCategory> InventoryCategories { get; set; } = new List<InventoryCategory>();
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public ICollection<JobOrder> JobOrders { get; set; } = new List<JobOrder>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<AccountingEntry> AccountingEntries { get; set; } = new List<AccountingEntry>();
    public ICollection<XeroSyncLog> XeroSyncLogs { get; set; } = new List<XeroSyncLog>();
    public ICollection<XeroConnection> XeroConnections { get; set; } = new List<XeroConnection>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<SubscriptionPayment> SubscriptionPayments { get; set; } = new List<SubscriptionPayment>();

    /// <summary>True for ByteBill Main Shop — cannot be deleted or suspended.</summary>
    public bool IsDefault { get; set; }
}
