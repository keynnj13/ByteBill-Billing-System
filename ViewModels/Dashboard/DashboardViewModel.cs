using ByteBill_BS.Models.Enums;

namespace ByteBill_BS.ViewModels.Dashboard;

public class DashboardViewModel
{
    // User context
    public UserRole UserRole { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    
    // Job Order Stats
    public int TotalJobOrders { get; set; }
    public int PendingJobOrdersCount { get; set; }
    public int ActiveJobOrders { get; set; }
    public int InProgressJobOrders { get; set; }
    public int CompletedToday { get; set; }
    public int CompletedJobOrders { get; set; }
    public int PendingJobOrders_Count { get; set; }
    
    // Revenue Stats
    public decimal TodayRevenue { get; set; }
    public decimal WeekRevenue { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal TotalRevenue { get; set; }
    
    // Invoice Stats
    public int PendingInvoices { get; set; }
    public int PaidToday { get; set; }
    public int TotalInvoices { get; set; }
    public int UnpaidInvoices { get; set; }
    public int OverdueInvoices { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal OutstandingBalance { get; set; }
    
    // Customer Stats
    public int TotalCustomers { get; set; }
    public int NewCustomersThisMonth { get; set; }
    
    // Inventory
    public int LowStockItems { get; set; }
    public int LowStockCount { get; set; }
    
    // Recent Activity
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
    
    // Job Orders
    public List<JobOrderSummary> PendingJobOrders { get; set; } = new();
    public List<JobOrderSummary> RecentJobOrders { get; set; } = new();
    
    // Recent data
    public List<RecentPaymentItem> RecentPayments { get; set; } = new();
    public List<RecentInvoiceItem> RecentInvoices { get; set; } = new();
    public List<LowStockItem> LowStockAlerts { get; set; } = new();
    
    // Chart Data
    public List<ChartDataPoint> RevenueChart { get; set; } = new();
    public List<ChartDataPoint> JobOrderChart { get; set; } = new();
}

public class RecentActivityItem
{
    public string Icon { get; set; } = string.Empty;
    public string IconColor { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = string.Empty;
    public string? BadgeText { get; set; }
    public string? BadgeClass { get; set; }
}

public class JobOrderSummary
{
    public long Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public JobOrderStatus Status { get; set; }
    public string StatusBadgeClass { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? TechnicianName { get; set; }
}

public class RecentPaymentItem
{
    public long Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }
}

public class RecentInvoiceItem
{
    public long Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusClass { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class LowStockItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int QtyOnHand { get; set; }
    public int ReorderLevel { get; set; }
}

public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
