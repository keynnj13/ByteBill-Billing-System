using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db) => _db = db;

    private bool IsAuthorized() => User.IsInRoles("Billing", "Admin", "SuperAdmin");

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
            : (parts.Length == 1 ? parts[0][..1].ToUpper() : "??");
    }

    private static string TimeAgo(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? period, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var (dateFrom, dateTo, activePeriod) = ResolveDateRange(period, from, to);
        var hasDateFilter = dateFrom != DateTime.MinValue;
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Revenue (confirmed payments – filtered by date range)
        var payBase = _db.Payments
            .Where(p => p.ShopId == shopId && p.Status != PaymentStatus.Refunded);
        if (hasDateFilter) payBase = payBase.Where(p => p.PaymentDate >= dateFrom && p.PaymentDate <= dateTo);

        var periodRevenue = await payBase.SumAsync(p => (decimal?)p.Amount) ?? 0;

        // Paid count
        var paidCount = await payBase.CountAsync();

        // Invoice stats (filtered by InvoiceDate when date range specified)
        var invBase = _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived
                        && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial));
        if (hasDateFilter) invBase = invBase.Where(i => i.InvoiceDate >= dateFrom && i.InvoiceDate <= dateTo);

        var pendingInvoices = await invBase.CountAsync();

        var outstandingBalance = await invBase
            .SumAsync(i => (decimal?)i.Balance) ?? 0;

        var overdueCount = await invBase
            .Where(i => i.DueDate.HasValue && i.DueDate < today)
            .CountAsync();

        // Recent activity from audit logs (billing-related)
        var recentLogs = await _db.AuditLogs
            .Where(a => a.ShopId == shopId &&
                (a.EntityName == "Payment" || a.EntityName == "Invoice"))
            .OrderByDescending(a => a.CreatedAt)
            .Take(6)
            .Select(a => new { a.Action, a.EntityName, a.Details, a.CreatedAt })
            .ToListAsync();

        var recentActivity = recentLogs.Select(a => new RecentActivityItem
        {
            Icon = a.EntityName == "Payment" ? "credit-card" : "file-text",
            IconColor = a.EntityName == "Payment" ? "success" : (a.Action == "Warning" ? "warning" : "primary"),
            Title = a.EntityName == "Payment" ? "Payment recorded" : $"Invoice {a.Action.ToLower()}",
            Description = a.Details?.Length > 80 ? a.Details[..80] + "…" : a.Details ?? "",
            TimeAgo = TimeAgo(a.CreatedAt)
        }).ToList();

        // Recent payments
        var recentPayments = await _db.Payments
            .Include(p => p.Customer)
            .Where(p => p.ShopId == shopId && p.Status != PaymentStatus.Refunded)
            .OrderByDescending(p => p.PaymentDate)
            .Take(5)
            .Select(p => new RecentPaymentItem
            {
                Id = p.PaymentId,
                PaymentNumber = p.PaymentNo ?? $"PAY-{p.PaymentId}",
                CustomerName = p.Customer!.FirstName + " " + p.Customer.LastName,
                Amount = p.Amount,
                Method = p.Method.ToString(),
                PaidAt = p.PaymentDate
            })
            .ToListAsync();

        // Unpaid invoices
        var unpaidInvoices = await _db.Invoices
            .Include(i => i.Customer)
            .Where(i => i.ShopId == shopId && !i.IsArchived
                        && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial))
            .OrderByDescending(i => i.CreatedAt)
            .Take(5)
            .Select(i => new RecentInvoiceItem
            {
                Id = i.InvoiceId,
                InvoiceNumber = i.InvoiceNo,
                CustomerName = i.Customer!.FirstName + " " + i.Customer.LastName,
                Total = i.TotalAmount,
                Balance = i.Balance,
                Status = i.Status.ToString(),
                DueDate = i.DueDate
            })
            .ToListAsync();

        var viewModel = new DashboardViewModel
        {
            UserRole = UserRole.Billing,
            UserName = User.GetFullName(),
            PeriodRevenue = periodRevenue,
            TodayRevenue = periodRevenue,
            PendingInvoices = pendingInvoices,
            PaidToday = paidCount,
            OutstandingBalance = outstandingBalance,
            OverdueInvoices = overdueCount,
            RecentActivity = recentActivity,
            RecentPayments = recentPayments,
            RecentInvoices = unpaidInvoices,
            ActivePeriod = activePeriod,
            FilterFrom = dateFrom == DateTime.MinValue ? null : dateFrom,
            FilterTo = dateTo == DateTime.MaxValue ? null : dateTo
        };

        return View(viewModel);
    }

    private static (DateTime from, DateTime to, string period) ResolveDateRange(string? period, DateTime? from, DateTime? to)
    {
        var today = DateTime.UtcNow.Date;
        return period switch
        {
            "today"   => (today, today.AddDays(1).AddTicks(-1), "today"),
            "week"    => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(1).AddTicks(-1), "week"),
            "month"   => (new DateTime(today.Year, today.Month, 1), today.AddDays(1).AddTicks(-1), "month"),
            "last30"  => (today.AddDays(-30), today.AddDays(1).AddTicks(-1), "last30"),
            "last3mo" => (today.AddMonths(-3), today.AddDays(1).AddTicks(-1), "last3mo"),
            "custom" when from.HasValue && to.HasValue =>
                (from.Value.Date, to.Value.Date.AddDays(1).AddTicks(-1), "custom"),
            _ => (DateTime.MinValue, DateTime.MaxValue, "all")
        };
    }
}
