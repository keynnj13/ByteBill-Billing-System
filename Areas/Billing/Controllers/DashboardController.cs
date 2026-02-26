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
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Revenue & payment stats
        var todayRevenue = await _db.Payments
            .Where(p => p.ShopId == shopId && p.PaymentDate >= today && p.PaymentDate < tomorrow
                        && p.Status != PaymentStatus.Refunded)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var paidTodayCount = await _db.Payments
            .Where(p => p.ShopId == shopId && p.PaymentDate >= today && p.PaymentDate < tomorrow
                        && p.Status != PaymentStatus.Refunded)
            .CountAsync();

        // Invoice stats
        var pendingInvoices = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived
                        && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial))
            .CountAsync();

        var outstandingBalance = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived
                        && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial))
            .SumAsync(i => (decimal?)i.Balance) ?? 0;

        var overdueCount = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived
                        && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Partial)
                        && i.DueDate.HasValue && i.DueDate < today)
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
            TodayRevenue = todayRevenue,
            PendingInvoices = pendingInvoices,
            PaidToday = paidTodayCount,
            OutstandingBalance = outstandingBalance,
            OverdueInvoices = overdueCount,
            RecentActivity = recentActivity,
            RecentPayments = recentPayments,
            RecentInvoices = unpaidInvoices
        };

        return View(viewModel);
    }
}
