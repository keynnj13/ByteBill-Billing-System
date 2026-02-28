using ByteBill_BS.Models.Enums;

namespace ByteBill_BS.ViewModels.Auditor;

// ═══════════════════════════════════════════════════════════════
//  AUDITOR  REPORT  HUB
// ═══════════════════════════════════════════════════════════════

public class AuditorReportHubViewModel
{
    public List<ReportCategoryCard> Categories { get; set; } = new();
}

public class ReportCategoryCard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════
//  1.  REVENUE  &  INVOICE  REPORT
// ═══════════════════════════════════════════════════════════════

public class RevenueInvoiceReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;

    // Summary
    public decimal TotalInvoiced { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int InvoiceCount { get; set; }
    public decimal AverageInvoice { get; set; }

    // Status breakdown
    public int PaidCount { get; set; }
    public int UnpaidCount { get; set; }
    public int PartialCount { get; set; }
    public int VoidCount { get; set; }

    // Monthly
    public List<MonthlyRevenueRow> MonthlyBreakdown { get; set; } = new();
}

public class MonthlyRevenueRow
{
    public string Month { get; set; } = string.Empty;
    public decimal Invoiced { get; set; }
    public decimal Collected { get; set; }
    public decimal Outstanding { get; set; }
    public int Count { get; set; }
}

// ═══════════════════════════════════════════════════════════════
//  2.  PAYMENT  &  CASH  FLOW  REPORT
// ═══════════════════════════════════════════════════════════════

public class PaymentCashFlowReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;

    public decimal TotalReceived { get; set; }
    public int TransactionCount { get; set; }
    public decimal AveragePayment { get; set; }
    public decimal TotalRefunds { get; set; }
    public decimal NetCashFlow { get; set; }

    public List<PaymentMethodRow> MethodBreakdown { get; set; } = new();
    public List<DailyCashFlowRow> DailyTrend { get; set; } = new();
}

public class PaymentMethodRow
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class DailyCashFlowRow
{
    public DateTime Date { get; set; }
    public decimal Received { get; set; }
    public decimal Refunded { get; set; }
    public decimal Net { get; set; }
    public int Count { get; set; }
}

// ═══════════════════════════════════════════════════════════════
//  3.  JOB  ORDER  OPERATIONAL  AUDIT  REPORT
// ═══════════════════════════════════════════════════════════════

public class JobOrderAuditReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;

    public int TotalJobOrders { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public int CancelledCount { get; set; }
    public decimal AverageCompletionDays { get; set; }
    public decimal TotalServiceRevenue { get; set; }

    public List<JobStatusRow> StatusBreakdown { get; set; } = new();
    public List<TechnicianRow> TechnicianPerformance { get; set; } = new();
}

public class JobStatusRow
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class TechnicianRow
{
    public string Name { get; set; } = string.Empty;
    public int JobsAssigned { get; set; }
    public int JobsCompleted { get; set; }
    public decimal Revenue { get; set; }
    public decimal AvgDays { get; set; }
}

// ═══════════════════════════════════════════════════════════════
//  4.  FINANCIAL  INTEGRITY  REPORT
// ═══════════════════════════════════════════════════════════════

public class FinancialIntegrityReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;

    public decimal TotalDiscounts { get; set; }
    public decimal TotalRefunds { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal NetAdjustments { get; set; }
    public int VoidedInvoiceCount { get; set; }
    public decimal VoidedInvoiceAmount { get; set; }
    public int VoidedPaymentCount { get; set; }
    public decimal VoidedPaymentAmount { get; set; }

    public List<AdjustmentRow> Adjustments { get; set; } = new();
    public List<VoidedItemRow> VoidedInvoices { get; set; } = new();
    public List<VoidedItemRow> VoidedPayments { get; set; } = new();
}

public class AdjustmentRow
{
    public string Type { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class VoidedItemRow
{
    public string ReferenceNo { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
}

// ═══════════════════════════════════════════════════════════════
//  5.  XERO  &  EXTERNAL  SYNC  REPORT
// ═══════════════════════════════════════════════════════════════

public class XeroSyncReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;

    public int TotalSyncs { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public decimal SuccessRate { get; set; }
    public DateTime? LastSyncAt { get; set; }

    public int PayMongoTxnCount { get; set; }
    public decimal PayMongoTotalAmount { get; set; }

    public List<SyncLogRow> RecentSyncs { get; set; } = new();
    public List<PayMongoSummaryRow> PayMongoSummary { get; set; } = new();
}

public class SyncLogRow
{
    public long Id { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? EntityRef { get; set; }
    public string? Message { get; set; }
    public string SyncedBy { get; set; } = string.Empty;
    public DateTime SyncedAt { get; set; }
}

public class PayMongoSummaryRow
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

// ═══════════════════════════════════════════════════════════════
//  6.  USER  &  SECURITY  AUDIT  REPORT
// ═══════════════════════════════════════════════════════════════

public class UserSecurityReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string DateRange { get; set; } = string.Empty;

    public int TotalActions { get; set; }
    public int LoginCount { get; set; }
    public int CreateCount { get; set; }
    public int UpdateCount { get; set; }
    public int DeleteCount { get; set; }

    public List<UserActivityRow> UserActivity { get; set; } = new();
    public List<ActionBreakdownRow> ActionBreakdown { get; set; } = new();
    public List<EntityBreakdownRow> EntityBreakdown { get; set; } = new();
}

public class UserActivityRow
{
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int ActionCount { get; set; }
    public DateTime? LastAction { get; set; }
    public DateTime? LastLogin { get; set; }
}

public class ActionBreakdownRow
{
    public string Action { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class EntityBreakdownRow
{
    public string Entity { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}
