using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class ReportsController : Controller
{
    private readonly ISuperAdminService _service;

    public ReportsController(ISuperAdminService service)
    {
        _service = service;
    }

    private bool IsAuthorized()
    {
        var role = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return role == "SuperAdmin";
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
            "last6mo" => (today.AddMonths(-6), today, "Last 6 Months"),
            "year" => (new DateTime(today.Year, 1, 1), today, "This Year"),
            "custom" when from.HasValue && to.HasValue => (from.Value, to.Value, $"{from.Value:MMM d} – {to.Value:MMM d, yyyy}"),
            _ => (today.AddMonths(-3), today, "Last 3 Months")
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  HUB PAGE
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public IActionResult Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var vm = new SAReportHubViewModel
        {
            Categories = new()
            {
                new() { Id = "revenue", Name = "Revenue Report", Description = "Subscription payments, monthly trends, revenue per shop", Icon = "peso-sign", Color = "#10b981", Url = Url.Action("Revenue")! },
                new() { Id = "shops", Name = "Shops Activity", Description = "New shop registrations, growth trends, active vs inactive", Icon = "store", Color = "#6366f1", Url = Url.Action("Shops")! },
                new() { Id = "users", Name = "User Activity", Description = "User registration trends, role breakdown, active users", Icon = "users", Color = "#3b82f6", Url = Url.Action("Users")! },
                new() { Id = "subscriptions", Name = "Subscription Overview", Description = "Plan distribution, MRR tracking, billing cycles", Icon = "credit-card", Color = "#8b5cf6", Url = Url.Action("Subscriptions")! },
                new() { Id = "payments", Name = "Payment History", Description = "Payment methods, status breakdown, collection rates", Icon = "wallet", Color = "#f59e0b", Url = Url.Action("Payments")! },
                new() { Id = "growth", Name = "Growth Analytics", Description = "Platform growth trends, cumulative metrics, lifetime stats", Icon = "trending-up", Color = "#ef4444", Url = Url.Action("Growth")! }
            }
        };

        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  1. REVENUE REPORT
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Revenue(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateRevenueReportAsync(dateFrom, dateTo, label);
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  2. SHOPS ACTIVITY
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Shops(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateShopsReportAsync(dateFrom, dateTo, label);
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  3. USER ACTIVITY
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Users(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateUsersReportAsync(dateFrom, dateTo, label);
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  4. SUBSCRIPTION OVERVIEW
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Subscriptions(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateSubscriptionReportAsync(dateFrom, dateTo, label);
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  5. PAYMENT HISTORY
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Payments(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GeneratePaymentReportAsync(dateFrom, dateTo, label);
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  6. GROWTH ANALYTICS
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> Growth(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateGrowthReportAsync(dateFrom, dateTo, label);
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  LEGACY CSV EXPORT (kept for backward compat)
    // ═══════════════════════════════════════════════════════════
    public async Task<IActionResult> ExportCsv(string report = "revenue", string? from = null, string? to = null)
    {
        if (!IsAuthorized()) return Forbid();

        var dateFrom = DateTime.TryParse(from, out var df) ? df : DateTime.UtcNow.AddMonths(-6);
        var dateTo = DateTime.TryParse(to, out var dt) ? dt : DateTime.UtcNow;

        var csv = await _service.ExportReportCsvAsync(report, dateFrom, dateTo);
        return File(csv, "text/csv", $"ByteBill_{report}_report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ═══════════════════════════════════════════════════════════
    //  PDF EXPORTS — per report type
    // ═══════════════════════════════════════════════════════════
    [HttpGet]
    public async Task<IActionResult> ExportRevenuePdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateRevenueReportAsync(dateFrom, dateTo, label);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Revenue Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total Revenue: ₱{vm.TotalRevenue:N2}").Bold();
                        row.RelativeItem().Text($"Transactions: {vm.TransactionCount}").Bold();
                        row.RelativeItem().Text($"Paying Shops: {vm.PayingShops}").Bold();
                    });
                    col.Item().Row(row => { row.RelativeItem().Text($"Avg / Shop: ₱{vm.AvgPerShop:N2}"); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Monthly Breakdown").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Month").Bold(); h.Cell().Text("Amount").Bold(); h.Cell().Text("Count").Bold(); });
                        foreach (var m in vm.MonthlyBreakdown) { table.Cell().Text(m.Month); table.Cell().Text($"₱{m.Amount:N2}"); table.Cell().Text(m.Count.ToString()); }
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().Text("Transactions").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); });
                        table.Header(h => { h.Cell().Text("Reference").Bold(); h.Cell().Text("Shop").Bold(); h.Cell().Text("Plan").Bold(); h.Cell().Text("Amount").Bold(); h.Cell().Text("Paid Date").Bold(); });
                        foreach (var p in vm.Payments) { table.Cell().Text(p.Reference); table.Cell().Text(p.Shop); table.Cell().Text(p.Plan); table.Cell().Text($"₱{p.Amount:N2}"); table.Cell().Text(p.PaidDate); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"Revenue_Report_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportShopsPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateShopsReportAsync(dateFrom, dateTo, label);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Shops Activity Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"New Shops: {vm.NewShops}").Bold(); row.RelativeItem().Text($"Total: {vm.TotalShops}").Bold(); row.RelativeItem().Text($"Active: {vm.ActiveShops}").Bold(); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Shops Created in Period").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(2); });
                        table.Header(h => { h.Cell().Text("Shop").Bold(); h.Cell().Text("Status").Bold(); h.Cell().Text("Users").Bold(); h.Cell().Text("Created").Bold(); });
                        foreach (var s in vm.Shops) { table.Cell().Text(s.Name); table.Cell().Text(s.Status); table.Cell().Text(s.Users.ToString()); table.Cell().Text(s.Created); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"Shops_Activity_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportUsersPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateUsersReportAsync(dateFrom, dateTo, label);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — User Activity Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"New Users: {vm.NewUsers}").Bold(); row.RelativeItem().Text($"Total: {vm.TotalUsers}").Bold(); row.RelativeItem().Text($"Active: {vm.ActiveUsers}").Bold(); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Users").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1.5f); });
                        table.Header(h => { h.Cell().Text("Name").Bold(); h.Cell().Text("Role").Bold(); h.Cell().Text("Shop").Bold(); h.Cell().Text("Status").Bold(); h.Cell().Text("Created").Bold(); });
                        foreach (var u in vm.Users) { table.Cell().Text(u.Name); table.Cell().Text(u.Role); table.Cell().Text(u.Shop); table.Cell().Text(u.Status); table.Cell().Text(u.Created); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"User_Activity_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportSubscriptionsPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateSubscriptionReportAsync(dateFrom, dateTo, label);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Subscription Overview Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"New: {vm.NewSubscriptions}").Bold(); row.RelativeItem().Text($"Active: {vm.ActiveSubscriptions}").Bold(); row.RelativeItem().Text($"MRR: ₱{vm.MRR:N2}").Bold(); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Subscriptions").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(1); c.RelativeColumn(1.5f); });
                        table.Header(h => { h.Cell().Text("Shop").Bold(); h.Cell().Text("Plan").Bold(); h.Cell().Text("Cycle").Bold(); h.Cell().Text("Price").Bold(); h.Cell().Text("Status").Bold(); h.Cell().Text("Start").Bold(); });
                        foreach (var s in vm.Subscriptions) { table.Cell().Text(s.Shop); table.Cell().Text(s.Plan); table.Cell().Text(s.Cycle); table.Cell().Text($"₱{s.Price:N2}"); table.Cell().Text(s.Status); table.Cell().Text(s.StartDate); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"Subscription_Overview_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPaymentsPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GeneratePaymentReportAsync(dateFrom, dateTo, label);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Payment History Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"Collected: ₱{vm.TotalCollected:N2}").Bold(); row.RelativeItem().Text($"Pending: ₱{vm.TotalPending:N2}").Bold(); row.RelativeItem().Text($"Failed: {vm.FailedCount}").Bold(); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    if (vm.MethodBreakdown.Any())
                    {
                        col.Item().Text("Payment Methods").Bold().FontSize(12);
                        col.Item().PaddingTop(5).Table(table =>
                        {
                            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); });
                            table.Header(h => { h.Cell().Text("Method").Bold(); h.Cell().Text("Amount").Bold(); h.Cell().Text("Count").Bold(); h.Cell().Text("%").Bold(); });
                            foreach (var m in vm.MethodBreakdown) { table.Cell().Text(m.Method); table.Cell().Text($"₱{m.Amount:N2}"); table.Cell().Text(m.Count.ToString()); table.Cell().Text($"{m.Percentage}%"); }
                        });
                        col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    }

                    col.Item().Text("Transactions").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1.5f); c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); });
                        table.Header(h => { h.Cell().Text("Reference").Bold(); h.Cell().Text("Shop").Bold(); h.Cell().Text("Amount").Bold(); h.Cell().Text("Status").Bold(); h.Cell().Text("Method").Bold(); h.Cell().Text("Date").Bold(); });
                        foreach (var p in vm.Payments) { table.Cell().Text(p.Reference); table.Cell().Text(p.Shop); table.Cell().Text($"₱{p.Amount:N2}"); table.Cell().Text(p.Status); table.Cell().Text(p.Method); table.Cell().Text(p.Date); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"Payment_History_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportGrowthPdf(string? range, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var (dateFrom, dateTo, label) = ResolveDateRange(range, from, to);
        var vm = await _service.GenerateGrowthReportAsync(dateFrom, dateTo, label);

        var pdf = Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(30); page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Growth Analytics Report").Bold().FontSize(18).FontColor(Colors.Blue.Medium);
                    col.Item().Text(vm.DateRange).FontSize(11).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });
                page.Content().Column(col =>
                {
                    col.Item().Row(row => { row.RelativeItem().Text($"Shops: {vm.TotalShops}").Bold(); row.RelativeItem().Text($"Users: {vm.TotalUsers}").Bold(); row.RelativeItem().Text($"Subs: {vm.ActiveSubscriptions}").Bold(); row.RelativeItem().Text($"Revenue: ₱{vm.LifetimeRevenue:N2}").Bold(); });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text("Monthly Growth").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); });
                        table.Header(h => { h.Cell().Text("Month").Bold(); h.Cell().Text("Cumulative Shops").Bold(); h.Cell().Text("Change").Bold(); });
                        foreach (var g in vm.MonthlyGrowth) { table.Cell().Text(g.Month); table.Cell().Text(g.CumulativeShops.ToString()); table.Cell().Text(g.Change); }
                    });
                });
                page.Footer().AlignCenter().Text(t => { t.Span("Generated ").FontSize(8).FontColor(Colors.Grey.Medium); t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium); });
            });
        });
        return File(pdf.GeneratePdf(), "application/pdf", $"Growth_Analytics_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf");
    }

    // Legacy PDF export for backward compat
    public async Task<IActionResult> ExportPdf(string report = "revenue", string? from = null, string? to = null)
    {
        if (!IsAuthorized()) return Forbid();
        var dateFrom = DateTime.TryParse(from, out var df) ? df : DateTime.UtcNow.AddMonths(-6);
        var dateTo = DateTime.TryParse(to, out var dt) ? dt : DateTime.UtcNow;

        return report switch
        {
            "revenue" => await ExportRevenuePdf(null, dateFrom, dateTo),
            "shops" => await ExportShopsPdf(null, dateFrom, dateTo),
            "users" => await ExportUsersPdf(null, dateFrom, dateTo),
            "subscriptions" => await ExportSubscriptionsPdf(null, dateFrom, dateTo),
            "payments" => await ExportPaymentsPdf(null, dateFrom, dateTo),
            "growth" => await ExportGrowthPdf(null, dateFrom, dateTo),
            _ => await ExportRevenuePdf(null, dateFrom, dateTo)
        };
    }
}
