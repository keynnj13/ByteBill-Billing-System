using ByteBill_BS.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace ByteBill_BS.ViewModels.Admin;

// ============================================================
//  USER  VIEW‑MODELS
// ============================================================

public class UserListViewModel
{
    public List<UserItemViewModel> Users { get; set; } = new();
    public string? SearchTerm { get; set; }
    public UserRole? RoleFilter { get; set; }
    public bool? ActiveOnly { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Stats
    public int ActiveCount { get; set; }
    public int AdminCount { get; set; }
    public int BillingCount { get; set; }
    public int TechnicianCount { get; set; }
}

public class UserItemViewModel
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string RoleClass => Role switch
    {
        UserRole.SuperAdmin  => "status-purple",
        UserRole.Admin       => "status-primary",
        UserRole.Billing     => "status-success",
        UserRole.Technician  => "status-info",
        UserRole.Auditor     => "status-warning",
        _                    => "status-muted"
    };
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserFormViewModel
{
    public long Id { get; set; }

    [Required(ErrorMessage = "First name is required")]
    [StringLength(50)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Middle Name")]
    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(50)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be 3-50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Username can only contain letters, numbers, dots, hyphens, and underscores")]
    [Display(Name = "Username")]
    public string? UserName { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [StringLength(100)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Role is required")]
    [Display(Name = "Role")]
    public UserRole Role { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    // For create only – will be hidden on edit
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Compare("Password", ErrorMessage = "Passwords do not match")]
    [Display(Name = "Confirm Password")]
    public string? ConfirmPassword { get; set; }

    // Helper
    public string FullName => $"{FirstName} {MiddleName} {LastName}".Replace("  ", " ").Trim();
}

public class UserDetailViewModel
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleClass => Role switch
    {
        UserRole.SuperAdmin  => "status-purple",
        UserRole.Admin       => "status-primary",
        UserRole.Billing     => "status-success",
        UserRole.Technician  => "status-info",
        UserRole.Auditor     => "status-warning",
        _                    => "status-muted"
    };
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Activity summary – role-specific
    public int JobOrdersHandled { get; set; }      // Technician
    public int PartsUsed { get; set; }              // Technician
    public int InvoicesCreated { get; set; }        // Billing
    public int PaymentsProcessed { get; set; }      // Billing
    public int LogsReviewed { get; set; }           // Auditor
    public int ReportsGenerated { get; set; }       // Auditor
    public int UsersManagedCount { get; set; }      // Admin
    public int TotalActivityCount { get; set; }     // Admin
    public List<UserActivityItem> RecentActivity { get; set; } = new();
}

public class UserActivityItem
{
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

// ============================================================
//  AUDIT LOG  VIEW‑MODELS
// ============================================================

public class AuditLogListViewModel
{
    public List<AuditLogItemViewModel> Logs { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? EntityFilter { get; set; }
    public string? ActionFilter { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public List<string> EntityNames { get; set; } = new();
    public List<string> ActionTypes { get; set; } = new();

    // Stats
    public int TodayCount { get; set; }
    public int ThisWeekCount { get; set; }

    // SuperAdmin cross-shop support (optional)
    public long? ShopFilter { get; set; }
    public List<(long Id, string Name)> ShopNames { get; set; } = new();
}

public class AuditLogItemViewModel
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActionClass => Action.ToLower() switch
    {
        "create" or "insert" => "status-success",
        "update" or "edit"   => "status-info",
        "delete" or "remove" => "status-danger",
        "login"              => "status-primary",
        "logout"             => "status-muted",
        _                    => "status-warning"
    };
    public string EntityName { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string? Details { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserInitials { get; set; } = string.Empty;
    public string? ShopName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogDetailViewModel
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string? Details { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}

// ============================================================
//  REPORT  VIEW‑MODELS
// ============================================================

public class ReportIndexViewModel
{
    public ReportSummaryCard Revenue { get; set; } = new();
    public ReportSummaryCard Payments { get; set; } = new();
    public ReportSummaryCard Services { get; set; } = new();
    public ReportSummaryCard Inventory { get; set; } = new();
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
}

public class ReportSummaryCard
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string SubText { get; set; } = string.Empty;
    public string Trend { get; set; } = string.Empty;  // "+12%" or "-5%"
    public bool IsPositive { get; set; } = true;
}

public class RecentActivityItem
{
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

// -- Revenue Report --
public class RevenueReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal AverageInvoice { get; set; }
    public int InvoiceCount { get; set; }
    public List<RevenueByMonth> MonthlyBreakdown { get; set; } = new();
    public List<RevenueByCategory> CategoryBreakdown { get; set; } = new();
}

public class RevenueByMonth
{
    public string Month { get; set; } = string.Empty;
    public decimal Invoiced { get; set; }
    public decimal Collected { get; set; }
    public int Count { get; set; }
}

public class RevenueByCategory
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

// -- Payment Report --
public class PaymentReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public decimal TotalReceived { get; set; }
    public int TransactionCount { get; set; }
    public decimal AveragePayment { get; set; }
    public List<PaymentByMethod> MethodBreakdown { get; set; } = new();
    public List<PaymentByDay> DailyTrend { get; set; } = new();
}

public class PaymentByMethod
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class PaymentByDay
{
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
}

// -- Service Performance Report --
public class ServicePerformanceReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int TotalJobOrders { get; set; }
    public decimal AverageCompletionDays { get; set; }
    public decimal TotalServiceRevenue { get; set; }
    public List<ServicePerformanceItem> Services { get; set; } = new();
    public List<CategoryPerformanceItem> Categories { get; set; } = new();
}

public class ServicePerformanceItem
{
    public string ServiceName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal AveragePrice { get; set; }
}

public class CategoryPerformanceItem
{
    public string Category { get; set; } = string.Empty;
    public int ServiceCount { get; set; }
    public int UsageCount { get; set; }
    public decimal Revenue { get; set; }
    public decimal Percentage { get; set; }
}

// -- Inventory Report --
public class InventoryReportViewModel
{
    public int TotalItems { get; set; }
    public int LowStockItems { get; set; }
    public int OutOfStockItems { get; set; }
    public decimal TotalStockValue { get; set; }
    public decimal TotalRetailValue { get; set; }
    public List<InventoryStockItem> Items { get; set; } = new();
    public List<InventoryCategoryBreakdown> CategoryBreakdown { get; set; } = new();
}

public class InventoryStockItem
{
    public string SKU { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public decimal UnitCost { get; set; }
    public decimal StockValue { get; set; }
    public bool IsLowStock { get; set; }
}

public class InventoryCategoryBreakdown
{
    public string Category { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public int TotalQty { get; set; }
    public decimal TotalValue { get; set; }
    public decimal Percentage { get; set; }
}

// ============================================================
//  INTEGRATION  VIEW‑MODELS
// ============================================================

public class IntegrationIndexViewModel
{
    // Xero
    public bool XeroConnected { get; set; }
    public DateTime? XeroLastSyncAt { get; set; }
    public int XeroSyncCount { get; set; }
    public int XeroFailedCount { get; set; }
    public List<XeroSyncLogItem> RecentXeroSyncs { get; set; } = new();

    // PayMongo
    public bool PayMongoEnabled { get; set; }
    public int PayMongoTransactions { get; set; }
    public decimal PayMongoTotalAmount { get; set; }
    public List<PayMongoTxnItem> RecentPayMongoTxns { get; set; } = new();

    // PayMongo Management UI
    public string? PayMongoWebhookUrl { get; set; }
    public bool PayMongoHasKeys { get; set; }
    public string? PayMongoKeyLastFour { get; set; }
}

public class XeroSyncLogItem
{
    public long Id { get; set; }
    public string SyncType { get; set; } = string.Empty;  // Invoice, Payment, AccountingEntry
    public string Status { get; set; } = string.Empty;     // Success, Failed, Pending
    public string StatusClass => Status.ToLower() switch
    {
        "success"  => "status-success",
        "failed"   => "status-danger",
        "pending"  => "status-warning",
        _          => "status-muted"
    };
    public string? EntityReference { get; set; }
    public string? XeroRecordId { get; set; }
    public string? Message { get; set; }
    public string SyncedByName { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
}

public class PayMongoTxnItem
{
    public long Id { get; set; }
    public string PayMongoId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;         // Payment, Checkout, Refund
    public string Status { get; set; } = string.Empty;       // Paid, Pending, Failed, Refunded
    public string StatusClass => Status.ToLower() switch
    {
        "paid"     => "status-success",
        "pending"  => "status-warning",
        "failed"   => "status-danger",
        "refunded" => "status-info",
        _          => "status-muted"
    };
    public decimal Amount { get; set; }
    public string? CustomerName { get; set; }
    public string? InvoiceNo { get; set; }
    public DateTime CreatedAt { get; set; }
}
