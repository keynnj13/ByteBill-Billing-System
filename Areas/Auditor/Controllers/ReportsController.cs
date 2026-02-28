using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Auditor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ByteBill_BS.Areas.Auditor.Controllers;

[Area("Auditor")]
[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ReportsController(ApplicationDbContext db) => _db = db;

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Auditor.ToString();
    }

    private (DateTime from, DateTime to, string label) ResolveDateRange(string? range, DateTime? from, DateTime? to)
    {
        var today = DateTime.UtcNow.Date;
        return range switch
        {
            "today" => (today, today, "Today"),
            "week" => (today.AddDays(-(int)today.DayOfWeek), today, "This Week"),
            "month" => (new DateTime(today.Year, today.Month, 1), today, "This Month"),
            "last30" => (today.AddDays(-30), today, "Last 30 Days"),
            "last3mo" => (today.AddMonths(-3), today, "Last 3 Months"),
            "custom" when from.HasValue && to.HasValue => (from.Value, to.Value, $"{from.Value:MMM d} – {to.Value:MMM d, yyyy}"),
            _ => (today.AddDays(-30), today, "Last 30 Days")
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  HUB PAGE
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public IActionResult Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var vm = new AuditorReportHubViewModel
        {
            Categories = new()
            {
                new() { Id = "revenue", Name = "Revenue & Invoice Reports", Description = "Monthly revenue, invoice status breakdown, collection rates", Icon = "dollar-sign", Color = "#10b981", Url = Url.Action("RevenueInvoice")! },
                new() { Id = "cashflow", Name = "Payment & Cash Flow Reports", Description = "Payment methods, daily cash flow trends, refund tracking", Icon = "credit-card", Color = "#3b82f6", Url = Url.Action("PaymentCashFlow")! },
                new() { Id = "joborder", Name = "Job Order Operational Audit", Description = "Job completion rates, technician performance, turnaround times", Icon = "tool", Color = "#8b5cf6", Url = Url.Action("JobOrderAudit")! },
                new() { Id = "integrity", Name = "Financial Integrity Reports", Description = "Adjustments, voids, write-offs, and anomaly detection", Icon = "shield", Color = "#f59e0b", Url = Url.Action("FinancialIntegrity")! },
                new() { Id = "xero", Name = "Xero & External Sync Reports", Description = "Sync success rates, PayMongo transactions, integration health", Icon = "refresh-cw", Color = "#06b6d4", Url = Url.Action("XeroSync")! },
                new() { Id = "security", Name = "User & Security Audit", Description = "User activity logs, login history, action breakdown", Icon = "users", Color = "#ef4444", Url = Url.Action("UserSecurity")! }
            }
        };

        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  1. REVENUE & INVOICE
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> RevenueInvoice(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var dateToExcl = dateTo.AddDays(1);

        var invoices = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived && i.CreatedAt >= dateFrom && i.CreatedAt < dateToExcl)
            .ToListAsync();

        var vm = new RevenueInvoiceReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            DateRange = label,
            TotalInvoiced = invoices.Sum(i => i.TotalAmount),
            TotalCollected = invoices.Sum(i => i.AmountPaid),
            TotalOutstanding = invoices.Where(i => i.Status != InvoiceStatus.Void).Sum(i => i.Balance),
            InvoiceCount = invoices.Count,
            AverageInvoice = invoices.Count > 0 ? invoices.Sum(i => i.TotalAmount) / invoices.Count : 0,
            PaidCount = invoices.Count(i => i.Status == InvoiceStatus.Paid),
            UnpaidCount = invoices.Count(i => i.Status == InvoiceStatus.Unpaid),
            PartialCount = invoices.Count(i => i.Status == InvoiceStatus.Partial),
            VoidCount = invoices.Count(i => i.Status == InvoiceStatus.Void),
            MonthlyBreakdown = invoices
                .GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new MonthlyRevenueRow
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Invoiced = g.Sum(x => x.TotalAmount),
                    Collected = g.Sum(x => x.AmountPaid),
                    Outstanding = g.Where(x => x.Status != InvoiceStatus.Void).Sum(x => x.Balance),
                    Count = g.Count()
                }).ToList()
        };

        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  2. PAYMENT & CASH FLOW
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> PaymentCashFlow(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var dateToExcl = dateTo.AddDays(1);

        var payments = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed && p.PaymentDate >= dateFrom && p.PaymentDate < dateToExcl)
            .ToListAsync();

        var refunds = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.AdjustmentType == AdjustmentType.Refund && a.Status == AdjustmentStatus.Approved && a.CreatedAt >= dateFrom && a.CreatedAt < dateToExcl)
            .SumAsync(a => (decimal?)a.Amount) ?? 0;

        var totalReceived = payments.Sum(p => p.Amount);
        var txnCount = payments.Count;
        var methodTotal = totalReceived > 0 ? totalReceived : 1;

        var vm = new PaymentCashFlowReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            DateRange = label,
            TotalReceived = totalReceived,
            TransactionCount = txnCount,
            AveragePayment = txnCount > 0 ? totalReceived / txnCount : 0,
            TotalRefunds = refunds,
            NetCashFlow = totalReceived - refunds,
            MethodBreakdown = payments
                .GroupBy(p => p.Method.ToString())
                .Select(g => new PaymentMethodRow
                {
                    Method = g.Key,
                    Amount = g.Sum(x => x.Amount),
                    Count = g.Count(),
                    Percentage = Math.Round(g.Sum(x => x.Amount) / methodTotal * 100, 1)
                }).OrderByDescending(m => m.Amount).ToList(),
            DailyTrend = payments
                .GroupBy(p => p.PaymentDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DailyCashFlowRow
                {
                    Date = g.Key,
                    Received = g.Sum(x => x.Amount),
                    Refunded = 0,
                    Net = g.Sum(x => x.Amount),
                    Count = g.Count()
                }).ToList()
        };

        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  3. JOB ORDER OPERATIONAL AUDIT
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> JobOrderAudit(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var dateToExcl = dateTo.AddDays(1);

        var jobs = await _db.JobOrders
            .Where(j => j.ShopId == shopId && !j.IsArchived && j.CreatedAt >= dateFrom && j.CreatedAt < dateToExcl)
            .Include(j => j.AssignedTechUser)
            .Include(j => j.JobOrderServices)
            .ToListAsync();

        var totalJobs = jobs.Count;
        var completed = jobs.Where(j => j.Status == JobOrderStatus.Completed).ToList();
        var avgDays = completed.Count > 0 && completed.Any(j => j.UpdatedAt.HasValue)
            ? (decimal)completed.Where(j => j.UpdatedAt.HasValue).Average(j => (j.UpdatedAt!.Value - j.CreatedAt).TotalDays)
            : 0;

        var statusGroups = jobs.GroupBy(j => j.Status.ToString()).ToList();
        var svcRevenue = jobs.SelectMany(j => j.JobOrderServices).Sum(js => js.UnitPrice * js.Qty);

        var vm = new JobOrderAuditReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            DateRange = label,
            TotalJobOrders = totalJobs,
            CompletedCount = completed.Count,
            InProgressCount = jobs.Count(j => j.Status == JobOrderStatus.InProgress),
            CancelledCount = jobs.Count(j => j.Status == JobOrderStatus.Cancelled),
            AverageCompletionDays = Math.Round(avgDays, 1),
            TotalServiceRevenue = svcRevenue,
            StatusBreakdown = statusGroups.Select(g => new JobStatusRow
            {
                Status = g.Key,
                Count = g.Count(),
                Percentage = totalJobs > 0 ? Math.Round((decimal)g.Count() / totalJobs * 100, 1) : 0
            }).OrderByDescending(s => s.Count).ToList(),
            TechnicianPerformance = jobs.Where(j => j.AssignedTechUser != null)
                .GroupBy(j => j.AssignedTechUser!.FirstName + " " + j.AssignedTechUser.LastName)
                .Select(g =>
                {
                    var comp = g.Where(j => j.Status == JobOrderStatus.Completed).ToList();
                    return new TechnicianRow
                    {
                        Name = g.Key,
                        JobsAssigned = g.Count(),
                        JobsCompleted = comp.Count,
                        Revenue = g.SelectMany(j => j.JobOrderServices).Sum(s => s.UnitPrice * s.Qty),
                        AvgDays = comp.Count > 0 && comp.Any(j => j.UpdatedAt.HasValue)
                            ? Math.Round((decimal)comp.Where(j => j.UpdatedAt.HasValue).Average(j => (j.UpdatedAt!.Value - j.CreatedAt).TotalDays), 1)
                            : 0
                    };
                }).OrderByDescending(t => t.JobsCompleted).ToList()
        };

        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  4. FINANCIAL INTEGRITY
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> FinancialIntegrity(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var dateToExcl = dateTo.AddDays(1);

        var adjustments = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.CreatedAt >= dateFrom && a.CreatedAt < dateToExcl)
            .Include(a => a.Invoice).ThenInclude(i => i!.Customer)
            .Include(a => a.CreatedByUser)
            .ToListAsync();

        var voidedInvoices = await _db.Invoices
            .Where(i => i.ShopId == shopId && i.Status == InvoiceStatus.Void && i.CreatedAt >= dateFrom && i.CreatedAt < dateToExcl)
            .Include(i => i.Customer)
            .ToListAsync();

        var voidedPayments = await _db.Payments
            .Where(p => p.ShopId == shopId && (p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.Failed) && p.PaymentDate >= dateFrom && p.PaymentDate < dateToExcl)
            .Include(p => p.Customer)
            .ToListAsync();

        var approved = adjustments.Where(a => a.Status == AdjustmentStatus.Approved).ToList();

        var vm = new FinancialIntegrityReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            DateRange = label,
            TotalDiscounts = 0m, // No Discount type in enum
            TotalRefunds = approved.Where(a => a.AdjustmentType == AdjustmentType.Refund).Sum(a => a.Amount),
            TotalCredits = approved.Where(a => a.AdjustmentType == AdjustmentType.Credit).Sum(a => a.Amount),
            TotalDebits = approved.Where(a => a.AdjustmentType == AdjustmentType.Debit).Sum(a => a.Amount),
            NetAdjustments = approved.Where(a => a.AdjustmentType == AdjustmentType.Debit).Sum(a => a.Amount)
                           - approved.Where(a => a.AdjustmentType != AdjustmentType.Debit).Sum(a => a.Amount),
            VoidedInvoiceCount = voidedInvoices.Count,
            VoidedInvoiceAmount = voidedInvoices.Sum(i => i.TotalAmount),
            VoidedPaymentCount = voidedPayments.Count,
            VoidedPaymentAmount = voidedPayments.Sum(p => p.Amount),
            Adjustments = adjustments.Select(a => new AdjustmentRow
            {
                Type = a.AdjustmentType.ToString(),
                InvoiceNo = a.Invoice?.InvoiceNo ?? "—",
                Customer = a.Invoice?.Customer != null ? a.Invoice.Customer.FirstName + " " + a.Invoice.Customer.LastName : "—",
                Amount = a.Amount,
                Reason = a.Reason,
                Status = a.Status.ToString(),
                CreatedBy = a.CreatedByUser != null ? a.CreatedByUser.FirstName + " " + a.CreatedByUser.LastName : "—",
                CreatedAt = a.CreatedAt
            }).ToList(),
            VoidedInvoices = voidedInvoices.Select(i => new VoidedItemRow
            {
                ReferenceNo = i.InvoiceNo,
                Customer = i.Customer != null ? i.Customer.FirstName + " " + i.Customer.LastName : "—",
                Amount = i.TotalAmount,
                Date = i.CreatedAt
            }).ToList(),
            VoidedPayments = voidedPayments.Select(p => new VoidedItemRow
            {
                ReferenceNo = p.PaymentNo,
                Customer = p.Customer != null ? p.Customer.FirstName + " " + p.Customer.LastName : "—",
                Amount = p.Amount,
                Date = p.PaymentDate
            }).ToList()
        };

        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  5. XERO & EXTERNAL SYNC
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> XeroSync(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var dateToExcl = dateTo.AddDays(1);

        var syncs = await _db.XeroSyncLogs
            .Where(x => x.ShopId == shopId && x.SyncedAt >= dateFrom && x.SyncedAt < dateToExcl)
            .Include(x => x.SyncedByUser)
            .OrderByDescending(x => x.SyncedAt)
            .ToListAsync();

        var payMongoTxns = await _db.PayMongoTxns
            .Where(t => t.ShopId == shopId && t.CreatedAt >= dateFrom && t.CreatedAt < dateToExcl)
            .ToListAsync();

        var totalSyncs = syncs.Count;
        var successCount = syncs.Count(s => s.Status == "Success");

        var vm = new XeroSyncReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            DateRange = label,
            TotalSyncs = totalSyncs,
            SuccessCount = successCount,
            FailedCount = syncs.Count(s => s.Status == "Failed"),
            SuccessRate = totalSyncs > 0 ? Math.Round((decimal)successCount / totalSyncs * 100, 1) : 0,
            LastSyncAt = syncs.FirstOrDefault()?.SyncedAt,
            PayMongoTxnCount = payMongoTxns.Count,
            PayMongoTotalAmount = payMongoTxns.Sum(t => t.Amount),
            RecentSyncs = syncs.Take(50).Select(s => new SyncLogRow
            {
                Id = s.XeroSyncLogId,
                SyncType = s.SyncType,
                Status = s.Status,
                EntityRef = s.XeroRecordId,
                Message = s.Message,
                SyncedBy = s.SyncedByUser != null ? s.SyncedByUser.FirstName + " " + s.SyncedByUser.LastName : "System",
                SyncedAt = s.SyncedAt
            }).ToList(),
            PayMongoSummary = payMongoTxns
                .GroupBy(t => t.PayMongoStatus)
                .Select(g => new PayMongoSummaryRow
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Amount)
                }).OrderByDescending(r => r.Amount).ToList()
        };

        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  6. USER & SECURITY AUDIT
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> UserSecurity(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var dateToExcl = dateTo.AddDays(1);

        var logs = await _db.AuditLogs
            .Where(a => a.ShopId == shopId && a.CreatedAt >= dateFrom && a.CreatedAt < dateToExcl)
            .Include(a => a.User).ThenInclude(u => u!.UserRoles).ThenInclude(ur => ur.Role)
            .ToListAsync();

        var totalActions = logs.Count;
        var totalSafe = totalActions > 0 ? totalActions : 1;

        var vm = new UserSecurityReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            DateRange = label,
            TotalActions = totalActions,
            LoginCount = logs.Count(l => l.Action == "Login"),
            CreateCount = logs.Count(l => l.Action == "Create"),
            UpdateCount = logs.Count(l => l.Action == "Update"),
            DeleteCount = logs.Count(l => l.Action == "Delete"),
            UserActivity = logs.Where(l => l.User != null)
                .GroupBy(l => new { Name = l.User!.FirstName + " " + l.User.LastName, Role = l.User.UserRoles.FirstOrDefault()?.Role?.RoleName ?? "—" })
                .Select(g => new UserActivityRow
                {
                    UserName = g.Key.Name,
                    Role = g.Key.Role,
                    ActionCount = g.Count(),
                    LastAction = g.Max(x => x.CreatedAt),
                    LastLogin = g.Where(x => x.Action == "Login").Max(x => (DateTime?)x.CreatedAt)
                }).OrderByDescending(u => u.ActionCount).ToList(),
            ActionBreakdown = logs.GroupBy(l => l.Action)
                .Select(g => new ActionBreakdownRow
                {
                    Action = g.Key,
                    Count = g.Count(),
                    Percentage = Math.Round((decimal)g.Count() / totalSafe * 100, 1)
                }).OrderByDescending(a => a.Count).ToList(),
            EntityBreakdown = logs.GroupBy(l => l.EntityName)
                .Select(g => new EntityBreakdownRow
                {
                    Entity = g.Key,
                    Count = g.Count(),
                    Percentage = Math.Round((decimal)g.Count() / totalSafe * 100, 1)
                }).OrderByDescending(e => e.Count).ToList()
        };

        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  PDF EXPORTS
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> ExportRevenueInvoicePdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var result = await RevenueInvoice(range, from, to);
        var vm = (result as ViewResult)?.Model as RevenueInvoiceReportViewModel;
        if (vm == null) return NotFound();

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Revenue & Invoice Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total Invoiced: ₱{vm.TotalInvoiced:N2}").Bold();
                        row.RelativeItem().Text($"Collected: ₱{vm.TotalCollected:N2}").Bold();
                        row.RelativeItem().Text($"Outstanding: ₱{vm.TotalOutstanding:N2}").Bold();
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Invoices: {vm.InvoiceCount}");
                        row.RelativeItem().Text($"Avg Invoice: ₱{vm.AverageInvoice:N2}");
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Paid: {vm.PaidCount}  |  Unpaid: {vm.UnpaidCount}  |  Partial: {vm.PartialCount}  |  Void: {vm.VoidCount}");
                    });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Monthly Breakdown").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Month").Bold(); h.Cell().Text("Invoiced").Bold(); h.Cell().Text("Collected").Bold(); h.Cell().Text("Outstanding").Bold(); h.Cell().Text("Count").Bold(); });
                        foreach (var m in vm.MonthlyBreakdown)
                        {
                            table.Cell().Text(m.Month); table.Cell().Text($"₱{m.Invoiced:N2}"); table.Cell().Text($"₱{m.Collected:N2}"); table.Cell().Text($"₱{m.Outstanding:N2}"); table.Cell().Text(m.Count.ToString());
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"Revenue_Invoice_Report_{vm.DateFrom:yyyyMMdd}_{vm.DateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPaymentCashFlowPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var result = await PaymentCashFlow(range, from, to);
        var vm = (result as ViewResult)?.Model as PaymentCashFlowReportViewModel;
        if (vm == null) return NotFound();

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Payment & Cash Flow Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"Total Received: ₱{vm.TotalReceived:N2}").Bold(); row.RelativeItem().Text($"Refunds: ₱{vm.TotalRefunds:N2}").Bold(); row.RelativeItem().Text($"Net Cash Flow: ₱{vm.NetCashFlow:N2}").Bold(); });
                    col.Item().Row(row => { row.RelativeItem().Text($"Transactions: {vm.TransactionCount}"); row.RelativeItem().Text($"Average: ₱{vm.AveragePayment:N2}"); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Payment Methods").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Method").Bold(); h.Cell().Text("Amount").Bold(); h.Cell().Text("Count").Bold(); h.Cell().Text("%").Bold(); });
                        foreach (var m in vm.MethodBreakdown) { table.Cell().Text(m.Method); table.Cell().Text($"₱{m.Amount:N2}"); table.Cell().Text(m.Count.ToString()); table.Cell().Text($"{m.Percentage}%"); }
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().Text("Daily Trend").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Date").Bold(); h.Cell().Text("Received").Bold(); h.Cell().Text("Count").Bold(); });
                        foreach (var d in vm.DailyTrend) { table.Cell().Text(d.Date.ToString("MMM dd")); table.Cell().Text($"₱{d.Received:N2}"); table.Cell().Text(d.Count.ToString()); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"Payment_CashFlow_{vm.DateFrom:yyyyMMdd}_{vm.DateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportJobOrderAuditPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var result = await JobOrderAudit(range, from, to);
        var vm = (result as ViewResult)?.Model as JobOrderAuditReportViewModel;
        if (vm == null) return NotFound();

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Job Order Operational Audit").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"Total Jobs: {vm.TotalJobOrders}").Bold(); row.RelativeItem().Text($"Completed: {vm.CompletedCount}").Bold(); row.RelativeItem().Text($"Avg Days: {vm.AverageCompletionDays}").Bold(); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Technician Performance").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Technician").Bold(); h.Cell().Text("Assigned").Bold(); h.Cell().Text("Completed").Bold(); h.Cell().Text("Revenue").Bold(); h.Cell().Text("Avg Days").Bold(); });
                        foreach (var t in vm.TechnicianPerformance) { table.Cell().Text(t.Name); table.Cell().Text(t.JobsAssigned.ToString()); table.Cell().Text(t.JobsCompleted.ToString()); table.Cell().Text($"₱{t.Revenue:N2}"); table.Cell().Text(t.AvgDays.ToString()); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"JobOrder_Audit_{vm.DateFrom:yyyyMMdd}_{vm.DateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportFinancialIntegrityPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var result = await FinancialIntegrity(range, from, to);
        var vm = (result as ViewResult)?.Model as FinancialIntegrityReportViewModel;
        if (vm == null) return NotFound();

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Financial Integrity Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"Discounts: ₱{vm.TotalDiscounts:N2}").Bold(); row.RelativeItem().Text($"Refunds: ₱{vm.TotalRefunds:N2}").Bold(); row.RelativeItem().Text($"Net Adj: ₱{vm.NetAdjustments:N2}").Bold(); });
                    col.Item().Row(row => { row.RelativeItem().Text($"Voided Invoices: {vm.VoidedInvoiceCount} (₱{vm.VoidedInvoiceAmount:N2})"); row.RelativeItem().Text($"Voided Payments: {vm.VoidedPaymentCount} (₱{vm.VoidedPaymentAmount:N2})"); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Adjustments").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(2); c.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Type").Bold(); h.Cell().Text("Invoice").Bold(); h.Cell().Text("Customer").Bold(); h.Cell().Text("Amount").Bold(); h.Cell().Text("Reason").Bold(); h.Cell().Text("Status").Bold(); });
                        foreach (var a in vm.Adjustments) { table.Cell().Text(a.Type); table.Cell().Text(a.InvoiceNo); table.Cell().Text(a.Customer); table.Cell().Text($"₱{a.Amount:N2}"); table.Cell().Text(a.Reason); table.Cell().Text(a.Status); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"Financial_Integrity_{vm.DateFrom:yyyyMMdd}_{vm.DateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportXeroSyncPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var result = await XeroSync(range, from, to);
        var vm = (result as ViewResult)?.Model as XeroSyncReportViewModel;
        if (vm == null) return NotFound();

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Xero & External Sync Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"Total Syncs: {vm.TotalSyncs}").Bold(); row.RelativeItem().Text($"Success: {vm.SuccessCount}").Bold(); row.RelativeItem().Text($"Failed: {vm.FailedCount}").Bold(); row.RelativeItem().Text($"Rate: {vm.SuccessRate}%").Bold(); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Sync Logs").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(1.5f); c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(2); c.RelativeColumn(1.5f); });
                        table.Header(h => { h.Cell().Text("Type").Bold(); h.Cell().Text("Status").Bold(); h.Cell().Text("Reference").Bold(); h.Cell().Text("Message").Bold(); h.Cell().Text("Date").Bold(); });
                        foreach (var s in vm.RecentSyncs.Take(30)) { table.Cell().Text(s.SyncType); table.Cell().Text(s.Status); table.Cell().Text(s.EntityRef ?? "—"); table.Cell().Text(s.Message ?? "—"); table.Cell().Text(s.SyncedAt.ToString("MMM dd HH:mm")); }
                    });

                    if (vm.PayMongoSummary.Any())
                    {
                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().Text("PayMongo Summary").Bold().FontSize(12);
                        col.Item().PaddingTop(5).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(2); });
                            table.Header(h => { h.Cell().Text("Status").Bold(); h.Cell().Text("Count").Bold(); h.Cell().Text("Amount").Bold(); });
                            foreach (var p in vm.PayMongoSummary) { table.Cell().Text(p.Status); table.Cell().Text(p.Count.ToString()); table.Cell().Text($"₱{p.Amount:N2}"); }
                        });
                    }
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"Xero_Sync_{vm.DateFrom:yyyyMMdd}_{vm.DateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportUserSecurityPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var result = await UserSecurity(range, from, to);
        var vm = (result as ViewResult)?.Model as UserSecurityReportViewModel;
        if (vm == null) return NotFound();

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — User & Security Audit Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"Total Actions: {vm.TotalActions}").Bold(); row.RelativeItem().Text($"Logins: {vm.LoginCount}").Bold(); row.RelativeItem().Text($"Creates: {vm.CreateCount}").Bold(); row.RelativeItem().Text($"Deletes: {vm.DeleteCount}").Bold(); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("User Activity").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(1); c.RelativeColumn(2); });
                        table.Header(h => { h.Cell().Text("User").Bold(); h.Cell().Text("Role").Bold(); h.Cell().Text("Actions").Bold(); h.Cell().Text("Last Active").Bold(); });
                        foreach (var u in vm.UserActivity) { table.Cell().Text(u.UserName); table.Cell().Text(u.Role); table.Cell().Text(u.ActionCount.ToString()); table.Cell().Text(u.LastAction?.ToString("MMM dd HH:mm") ?? "—"); }
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().Text("Action Breakdown").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Action").Bold(); h.Cell().Text("Count").Bold(); h.Cell().Text("%").Bold(); });
                        foreach (var a in vm.ActionBreakdown) { table.Cell().Text(a.Action); table.Cell().Text(a.Count.ToString()); table.Cell().Text($"{a.Percentage}%"); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"User_Security_{vm.DateFrom:yyyyMMdd}_{vm.DateTo:yyyyMMdd}.pdf");
    }
}
