using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ReportsController(ApplicationDbContext db) => _db = db;

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    // ─── HUB PAGE ───────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();

        var thisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var lastMonthStart = thisMonth.AddMonths(-1);
        var lastMonthEnd = thisMonth.AddDays(-1);

        // Gross revenue this month (Subtotal = pre-discount/pre-adjustment amount)
        var thisMonthInvoices = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived && i.InvoiceDate >= thisMonth)
            .ToListAsync();
        var thisMonthGross = thisMonthInvoices.Sum(i => i.Subtotal);
        var thisMonthRevenue = thisMonthInvoices.Sum(i => i.TotalAmount);

        var lastMonthRevenue = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived && i.InvoiceDate >= lastMonthStart && i.InvoiceDate <= lastMonthEnd)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;
        var revTrend = lastMonthRevenue > 0 ? ((thisMonthRevenue - lastMonthRevenue) / lastMonthRevenue * 100) : 0;

        // Payments this month
        var thisMonthPayments = await _db.Payments
            .Where(p => p.ShopId == shopId && p.PaymentDate >= thisMonth && p.Status == PaymentStatus.Confirmed)
            .ToListAsync();
        var payTotal = thisMonthPayments.Sum(p => p.Amount);
        var payCount = thisMonthPayments.Count;
        var lastMonthPayCount = await _db.Payments
            .Where(p => p.ShopId == shopId && p.PaymentDate >= lastMonthStart && p.PaymentDate <= lastMonthEnd && p.Status == PaymentStatus.Confirmed)
            .CountAsync();
        var payTrend = lastMonthPayCount > 0 ? ((double)(payCount - lastMonthPayCount) / lastMonthPayCount * 100) : 0;

        // Services this month (distinct job order services)
        var thisMonthSvcCount = await _db.JobOrderServices
            .Where(js => js.JobOrder!.ShopId == shopId && js.JobOrder.CreatedAt >= thisMonth)
            .CountAsync();
        var svcTypeCount = await _db.ServiceCatalogs.Where(s => s.ShopId == shopId && s.IsActive).CountAsync();

        // Inventory
        var inventoryItems = await _db.InventoryItems.Where(i => i.ShopId == shopId && i.IsActive).ToListAsync();
        var stockValue = inventoryItems.Sum(i => i.UnitCost * i.QtyOnHand);
        var lowStockCount = inventoryItems.Count(i => i.IsLowStock);

        // Recent audit activity related to reports
        var recentActivity = await _db.Set<ByteBill_BS.Models.AuditLog>()
            .Where(a => a.ShopId == shopId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new RecentActivityItem
            {
                Description = a.Action + " " + a.EntityName,
                Category = a.EntityName,
                Date = a.CreatedAt
            })
            .ToListAsync();

        var vm = new ReportIndexViewModel
        {
            Revenue = new()
            {
                Title = "Monthly Revenue",
                Value = "₱" + thisMonthRevenue.ToString("N0"),
                SubText = "vs ₱" + lastMonthRevenue.ToString("N0") + " last month",
                Trend = (revTrend >= 0 ? "+" : "") + revTrend.ToString("N1") + "%",
                IsPositive = revTrend >= 0
            },
            Payments = new()
            {
                Title = "Payments This Month",
                Value = "₱" + payTotal.ToString("N0"),
                SubText = payCount + " transactions",
                Trend = (payTrend >= 0 ? "+" : "") + payTrend.ToString("N1") + "%",
                IsPositive = payTrend >= 0
            },
            Services = new()
            {
                Title = "Services Performed",
                Value = thisMonthSvcCount.ToString(),
                SubText = "across " + svcTypeCount + " service types",
                Trend = "",
                IsPositive = true
            },
            Inventory = new()
            {
                Title = "Stock Value",
                Value = "₱" + stockValue.ToString("N0"),
                SubText = lowStockCount + " items low stock",
                Trend = lowStockCount > 0 ? "⚠" : "✓",
                IsPositive = lowStockCount == 0
            },
            RecentActivity = recentActivity
        };

        // Financial Summary (passed via ViewBag)
        var totalOutstanding = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived && i.Status != InvoiceStatus.Void)
            .SumAsync(i => (decimal?)i.Balance) ?? 0;

        // Adjustments this month
        var adjustments = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.Status == AdjustmentStatus.Approved && a.CreatedAt >= thisMonth)
            .ToListAsync();

        var totalCredits = adjustments
            .Where(a => a.AdjustmentType == AdjustmentType.Credit || a.AdjustmentType == AdjustmentType.Refund)
            .Sum(a => a.Amount);
        var totalDebits = adjustments
            .Where(a => a.AdjustmentType == AdjustmentType.Debit)
            .Sum(a => a.Amount);
        var netAdjustments = totalDebits - totalCredits;

        // Discounts this month (from invoices this month)
        var totalDiscounts = thisMonthInvoices.Sum(i => i.DiscountAmount);

        // VAT collected this month
        var totalVat = thisMonthInvoices.Sum(i => i.VatAmount);

        // Refunds this month
        var totalRefunds = adjustments
            .Where(a => a.AdjustmentType == AdjustmentType.Refund)
            .Sum(a => a.Amount);

        ViewBag.FinancialSummary = new
        {
            GrossRevenue = thisMonthGross,
            TotalRevenue = thisMonthRevenue,
            TotalCollected = payTotal,
            TotalOutstanding = totalOutstanding,
            TotalCredits = totalCredits,
            TotalDebits = totalDebits,
            NetAdjustments = netAdjustments,
            TotalDiscounts = totalDiscounts,
            TotalVat = totalVat,
            TotalRefunds = totalRefunds,
            // Net Revenue = Gross - Discounts + Net Adjustments (no double-counting)
            NetRevenue = thisMonthGross - totalDiscounts + netAdjustments
        };

        return View(vm);
    }

    // ─── REVENUE REPORT ─────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Revenue(DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();

        var dateFrom = from ?? DateTime.Today.AddMonths(-6);
        var dateTo = (to ?? DateTime.Today).AddDays(1); // inclusive end

        var invoices = await _db.Invoices
            .Where(i => i.ShopId == shopId && !i.IsArchived && i.InvoiceDate >= dateFrom && i.InvoiceDate < dateTo)
            .Include(i => i.JobOrder).ThenInclude(j => j!.JobOrderServices).ThenInclude(js => js.Service).ThenInclude(s => s!.ServiceCategory)
            .ToListAsync();

        var totalRevenue = invoices.Sum(i => i.TotalAmount);
        var totalCollected = invoices.Sum(i => i.AmountPaid);
        var totalOutstanding = invoices.Sum(i => i.Balance);
        var totalDiscounts = invoices.Sum(i => i.DiscountAmount);
        var totalVat = invoices.Sum(i => i.VatAmount);
        var invoiceCount = invoices.Count;
        var avgInvoice = invoiceCount > 0 ? totalRevenue / invoiceCount : 0;

        // Adjustments in the date range
        var rangeAdjustments = await _db.CreditDebitAdjustments
            .Where(a => a.ShopId == shopId && a.Status == AdjustmentStatus.Approved
                && a.CreatedAt >= dateFrom && a.CreatedAt < dateTo)
            .ToListAsync();
        var totalAdjustments = rangeAdjustments
            .Where(a => a.AdjustmentType == AdjustmentType.Debit).Sum(a => a.Amount)
            - rangeAdjustments
            .Where(a => a.AdjustmentType == AdjustmentType.Credit || a.AdjustmentType == AdjustmentType.Refund).Sum(a => a.Amount);
        var netRevenue = totalRevenue - totalDiscounts + totalAdjustments;

        // Monthly breakdown
        var monthly = invoices
            .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new RevenueByMonth
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                Invoiced = g.Sum(x => x.TotalAmount),
                Collected = g.Sum(x => x.AmountPaid),
                Count = g.Count()
            }).ToList();

        // Category breakdown via job order services
        var categoryRows = invoices
            .Where(i => i.JobOrder?.JobOrderServices != null)
            .SelectMany(i => i.JobOrder!.JobOrderServices)
            .Where(js => js.Service?.ServiceCategory != null)
            .GroupBy(js => js.Service!.ServiceCategory!.CategoryName)
            .Select(g => new { Cat = g.Key, Amount = g.Sum(x => x.UnitPrice * x.Qty), Count = g.Count() })
            .OrderByDescending(g => g.Amount)
            .ToList();
        var catTotal = categoryRows.Sum(c => c.Amount);
        var categoryBreakdown = categoryRows.Select(c => new RevenueByCategory
        {
            Category = c.Cat,
            Amount = c.Amount,
            Count = c.Count,
            Percentage = catTotal > 0 ? Math.Round(c.Amount / catTotal * 100, 1) : 0
        }).ToList();

        var vm = new RevenueReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = to ?? DateTime.Today,
            TotalRevenue = totalRevenue,
            TotalCollected = totalCollected,
            TotalOutstanding = totalOutstanding,
            AverageInvoice = avgInvoice,
            InvoiceCount = invoiceCount,
            TotalDiscounts = totalDiscounts,
            TotalVat = totalVat,
            TotalAdjustments = totalAdjustments,
            NetRevenue = netRevenue,
            MonthlyBreakdown = monthly,
            CategoryBreakdown = categoryBreakdown
        };
        return View(vm);
    }

    // ─── PAYMENT REPORT ─────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Payments(DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();

        var dateFrom = from ?? DateTime.Today.AddDays(-30);
        var dateTo = (to ?? DateTime.Today).AddDays(1);

        var payments = await _db.Payments
            .Where(p => p.ShopId == shopId && p.Status == PaymentStatus.Confirmed
                        && p.PaymentDate >= dateFrom && p.PaymentDate < dateTo)
            .ToListAsync();

        var totalReceived = payments.Sum(p => p.Amount);
        var txnCount = payments.Count;
        var avgPayment = txnCount > 0 ? totalReceived / txnCount : 0;

        // Method breakdown
        var methodRows = payments
            .GroupBy(p => p.Method.ToString())
            .Select(g => new { Method = g.Key, Amount = g.Sum(x => x.Amount), Count = g.Count() })
            .OrderByDescending(g => g.Amount)
            .ToList();
        var methodTotal = methodRows.Sum(m => m.Amount);
        var methodBreakdown = methodRows.Select(m => new PaymentByMethod
        {
            Method = m.Method,
            Amount = m.Amount,
            Count = m.Count,
            Percentage = methodTotal > 0 ? Math.Round(m.Amount / methodTotal * 100, 1) : 0
        }).ToList();

        // Daily trend
        var dailyTrend = payments
            .GroupBy(p => p.PaymentDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new PaymentByDay
            {
                Date = g.Key,
                Total = g.Sum(x => x.Amount),
                Count = g.Count()
            }).ToList();

        var vm = new PaymentReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = to ?? DateTime.Today,
            TotalReceived = totalReceived,
            TransactionCount = txnCount,
            AveragePayment = avgPayment,
            MethodBreakdown = methodBreakdown,
            DailyTrend = dailyTrend
        };
        return View(vm);
    }

    // ─── SERVICE PERFORMANCE REPORT ──────────────────────────
    [HttpGet]
    public async Task<IActionResult> ServicePerformance(DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();

        var dateFrom = from ?? DateTime.Today.AddMonths(-3);
        var dateTo = (to ?? DateTime.Today).AddDays(1);

        // All job orders in range
        var jobOrders = await _db.JobOrders
            .Where(j => j.ShopId == shopId && !j.IsArchived && j.CreatedAt >= dateFrom && j.CreatedAt < dateTo)
            .Include(j => j.JobOrderServices).ThenInclude(js => js.Service).ThenInclude(s => s!.ServiceCategory)
            .ToListAsync();

        var totalJobs = jobOrders.Count;

        // Average completion days (for completed jobs)
        var completedJobs = jobOrders.Where(j => j.Status == JobOrderStatus.Completed).ToList();
        var avgDays = completedJobs.Count > 0
            ? (decimal)completedJobs.Where(j => j.UpdatedAt.HasValue).Average(j => (j.UpdatedAt!.Value - j.CreatedAt).TotalDays)
            : 0;

        // Service-level breakdown
        var allServices = jobOrders.SelectMany(j => j.JobOrderServices).Where(js => js.Service != null).ToList();
        var totalServiceRevenue = allServices.Sum(js => js.UnitPrice * js.Qty);

        var serviceItems = allServices
            .GroupBy(js => new { js.Service!.ServiceName, Cat = js.Service.ServiceCategory?.CategoryName ?? "Uncategorized" })
            .Select(g => new ServicePerformanceItem
            {
                ServiceName = g.Key.ServiceName,
                Category = g.Key.Cat,
                UsageCount = g.Sum(x => x.Qty),
                Revenue = g.Sum(x => x.UnitPrice * x.Qty),
                AveragePrice = g.Count() > 0 ? g.Sum(x => x.UnitPrice * x.Qty) / g.Sum(x => x.Qty) : 0
            })
            .OrderByDescending(s => s.Revenue)
            .ToList();

        // Category breakdown
        var catItems = allServices
            .Where(js => js.Service?.ServiceCategory != null)
            .GroupBy(js => js.Service!.ServiceCategory!.CategoryName)
            .Select(g =>
            {
                var rev = g.Sum(x => x.UnitPrice * x.Qty);
                var distinctSvc = g.Select(x => x.ServiceId).Distinct().Count();
                return new CategoryPerformanceItem
                {
                    Category = g.Key,
                    ServiceCount = distinctSvc,
                    UsageCount = g.Sum(x => x.Qty),
                    Revenue = rev,
                    Percentage = totalServiceRevenue > 0 ? Math.Round(rev / totalServiceRevenue * 100, 1) : 0
                };
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();

        var vm = new ServicePerformanceReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = to ?? DateTime.Today,
            TotalJobOrders = totalJobs,
            AverageCompletionDays = Math.Round(avgDays, 1),
            TotalServiceRevenue = totalServiceRevenue,
            Services = serviceItems,
            Categories = catItems
        };
        return View(vm);
    }

    // ─── INVENTORY REPORT ───────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Inventory()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });
        var shopId = User.GetShopId();

        var items = await _db.InventoryItems
            .Where(i => i.ShopId == shopId && i.IsActive)
            .Include(i => i.InventoryCategory)
            .OrderBy(i => i.ItemName)
            .ToListAsync();

        var totalItems = items.Count;
        var lowStock = items.Count(i => i.IsLowStock && i.QtyOnHand > 0);
        var outOfStock = items.Count(i => i.QtyOnHand <= 0);
        var stockValue = items.Sum(i => i.UnitCost * i.QtyOnHand);
        var retailValue = items.Sum(i => i.UnitPrice * i.QtyOnHand);

        var itemList = items.Select(i => new InventoryStockItem
        {
            SKU = i.SKU,
            ItemName = i.ItemName,
            Category = i.InventoryCategory?.CategoryName ?? "Uncategorized",
            QtyOnHand = i.QtyOnHand,
            ReorderLevel = i.ReorderLevel,
            UnitCost = i.UnitCost,
            StockValue = i.UnitCost * i.QtyOnHand,
            IsLowStock = i.IsLowStock
        }).ToList();

        var catBreakdown = items
            .GroupBy(i => i.InventoryCategory?.CategoryName ?? "Uncategorized")
            .Select(g =>
            {
                var val = g.Sum(x => x.UnitCost * x.QtyOnHand);
                return new InventoryCategoryBreakdown
                {
                    Category = g.Key,
                    ItemCount = g.Count(),
                    TotalQty = g.Sum(x => x.QtyOnHand),
                    TotalValue = val,
                    Percentage = stockValue > 0 ? Math.Round(val / stockValue * 100, 1) : 0
                };
            })
            .OrderByDescending(c => c.TotalValue)
            .ToList();

        var vm = new InventoryReportViewModel
        {
            TotalItems = totalItems,
            LowStockItems = lowStock,
            OutOfStockItems = outOfStock,
            TotalStockValue = stockValue,
            TotalRetailValue = retailValue,
            Items = itemList,
            CategoryBreakdown = catBreakdown
        };
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════
    //  PDF EXPORT  ENDPOINTS
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> ExportRevenuePdf(DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        // Reuse the Revenue action logic
        var result = await Revenue(from, to);
        var vm = (result as ViewResult)?.Model as RevenueReportViewModel;
        if (vm == null) return NotFound();

        var pdf = QuestPDF.Fluent.Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Revenue Report").Bold().FontSize(18).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                    col.Item().Text($"{vm.DateFrom:MMM dd, yyyy} — {vm.DateTo:MMM dd, yyyy}").FontSize(11).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });

                page.Content().Column(col =>
                {
                    // KPI summary
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total Invoiced: ₱{vm.TotalRevenue:N2}").Bold();
                        row.RelativeItem().Text($"Collected: ₱{vm.TotalCollected:N2}").Bold();
                        row.RelativeItem().Text($"Outstanding: ₱{vm.TotalOutstanding:N2}").Bold();
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Invoice Count: {vm.InvoiceCount}");
                        row.RelativeItem().Text($"Average Invoice: ₱{vm.AverageInvoice:N2}");
                    });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    // Monthly breakdown table
                    col.Item().Text("Monthly Breakdown").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Month").Bold();
                            h.Cell().Text("Invoiced").Bold();
                            h.Cell().Text("Collected").Bold();
                            h.Cell().Text("Count").Bold();
                        });
                        foreach (var m in vm.MonthlyBreakdown)
                        {
                            table.Cell().Text(m.Month);
                            table.Cell().Text($"₱{m.Invoiced:N2}");
                            table.Cell().Text($"₱{m.Collected:N2}");
                            table.Cell().Text(m.Count.ToString());
                        }
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    // Category breakdown table
                    col.Item().Text("Revenue by Category").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Category").Bold();
                            h.Cell().Text("Amount").Bold();
                            h.Cell().Text("Count").Bold();
                            h.Cell().Text("%").Bold();
                        });
                        foreach (var cat in vm.CategoryBreakdown)
                        {
                            table.Cell().Text(cat.Category);
                            table.Cell().Text($"₱{cat.Amount:N2}");
                            table.Cell().Text(cat.Count.ToString());
                            table.Cell().Text($"{cat.Percentage}%");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated ").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                });
            });
        });

        var bytes = pdf.GeneratePdf();
        return File(bytes, "application/pdf", $"Revenue_Report_{vm.DateFrom:yyyyMMdd}_{vm.DateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportPaymentsPdf(DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var result = await Payments(from, to);
        var vm = (result as ViewResult)?.Model as PaymentReportViewModel;
        if (vm == null) return NotFound();

        var pdf = QuestPDF.Fluent.Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Payment Report").Bold().FontSize(18).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                    col.Item().Text($"{vm.DateFrom:MMM dd, yyyy} — {vm.DateTo:MMM dd, yyyy}").FontSize(11).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total Received: ₱{vm.TotalReceived:N2}").Bold();
                        row.RelativeItem().Text($"Transactions: {vm.TransactionCount}").Bold();
                        row.RelativeItem().Text($"Average: ₱{vm.AveragePayment:N2}").Bold();
                    });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    col.Item().Text("Payment Methods").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Method").Bold();
                            h.Cell().Text("Amount").Bold();
                            h.Cell().Text("Count").Bold();
                            h.Cell().Text("%").Bold();
                        });
                        foreach (var m in vm.MethodBreakdown)
                        {
                            table.Cell().Text(m.Method);
                            table.Cell().Text($"₱{m.Amount:N2}");
                            table.Cell().Text(m.Count.ToString());
                            table.Cell().Text($"{m.Percentage}%");
                        }
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    col.Item().Text("Daily Trend").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Date").Bold();
                            h.Cell().Text("Total").Bold();
                            h.Cell().Text("Count").Bold();
                        });
                        foreach (var d in vm.DailyTrend)
                        {
                            table.Cell().Text(d.Date.ToString("MMM dd, yyyy"));
                            table.Cell().Text($"₱{d.Total:N2}");
                            table.Cell().Text(d.Count.ToString());
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated ").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                });
            });
        });

        var bytes = pdf.GeneratePdf();
        return File(bytes, "application/pdf", $"Payments_Report_{vm.DateFrom:yyyyMMdd}_{vm.DateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportServicePdf(DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();
        var result = await ServicePerformance(from, to);
        var vm = (result as ViewResult)?.Model as ServicePerformanceReportViewModel;
        if (vm == null) return NotFound();

        var pdf = QuestPDF.Fluent.Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Service Performance Report").Bold().FontSize(18).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                    col.Item().Text($"{vm.DateFrom:MMM dd, yyyy} — {vm.DateTo:MMM dd, yyyy}").FontSize(11).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total Job Orders: {vm.TotalJobOrders}").Bold();
                        row.RelativeItem().Text($"Avg Completion: {vm.AverageCompletionDays} days").Bold();
                        row.RelativeItem().Text($"Service Revenue: ₱{vm.TotalServiceRevenue:N2}").Bold();
                    });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    col.Item().Text("Top Services").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(2);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Service").Bold();
                            h.Cell().Text("Category").Bold();
                            h.Cell().Text("Used").Bold();
                            h.Cell().Text("Revenue").Bold();
                            h.Cell().Text("Avg Price").Bold();
                        });
                        foreach (var s in vm.Services)
                        {
                            table.Cell().Text(s.ServiceName);
                            table.Cell().Text(s.Category);
                            table.Cell().Text(s.UsageCount.ToString());
                            table.Cell().Text($"₱{s.Revenue:N2}");
                            table.Cell().Text($"₱{s.AveragePrice:N2}");
                        }
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    col.Item().Text("Category Breakdown").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Category").Bold();
                            h.Cell().Text("Services").Bold();
                            h.Cell().Text("Used").Bold();
                            h.Cell().Text("Revenue").Bold();
                            h.Cell().Text("%").Bold();
                        });
                        foreach (var c in vm.Categories)
                        {
                            table.Cell().Text(c.Category);
                            table.Cell().Text(c.ServiceCount.ToString());
                            table.Cell().Text(c.UsageCount.ToString());
                            table.Cell().Text($"₱{c.Revenue:N2}");
                            table.Cell().Text($"{c.Percentage}%");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated ").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                });
            });
        });

        var bytes = pdf.GeneratePdf();
        return File(bytes, "application/pdf", $"Service_Performance_{vm.DateFrom:yyyyMMdd}_{vm.DateTo:yyyyMMdd}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> ExportInventoryPdf()
    {
        if (!IsAuthorized()) return Forbid();
        var result = await Inventory();
        var vm = (result as ViewResult)?.Model as InventoryReportViewModel;
        if (vm == null) return NotFound();

        var pdf = QuestPDF.Fluent.Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text("ByteBill — Inventory Report").Bold().FontSize(18).FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                    col.Item().Text($"Generated {DateTime.Now:MMM dd, yyyy}").FontSize(11).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    col.Item().PaddingBottom(10);
                });

                page.Content().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Total Items: {vm.TotalItems}").Bold();
                        row.RelativeItem().Text($"Low Stock: {vm.LowStockItems}").Bold().FontColor(vm.LowStockItems > 0 ? QuestPDF.Helpers.Colors.Orange.Medium : QuestPDF.Helpers.Colors.Green.Medium);
                        row.RelativeItem().Text($"Out of Stock: {vm.OutOfStockItems}").Bold().FontColor(vm.OutOfStockItems > 0 ? QuestPDF.Helpers.Colors.Red.Medium : QuestPDF.Helpers.Colors.Green.Medium);
                        row.RelativeItem().Text($"Stock Value: ₱{vm.TotalStockValue:N2}").Bold();
                        row.RelativeItem().Text($"Retail Value: ₱{vm.TotalRetailValue:N2}").Bold();
                    });
                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    col.Item().Text("Inventory Items").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.5f); c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(2);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("SKU").Bold();
                            h.Cell().Text("Item").Bold();
                            h.Cell().Text("Category").Bold();
                            h.Cell().Text("Qty").Bold();
                            h.Cell().Text("Reorder").Bold();
                            h.Cell().Text("Unit Cost").Bold();
                            h.Cell().Text("Stock Value").Bold();
                        });
                        foreach (var item in vm.Items)
                        {
                            table.Cell().Text(item.SKU);
                            table.Cell().Text(item.ItemName);
                            table.Cell().Text(item.Category);
                            table.Cell().Text(item.QtyOnHand.ToString()).FontColor(item.IsLowStock ? QuestPDF.Helpers.Colors.Red.Medium : QuestPDF.Helpers.Colors.Black);
                            table.Cell().Text(item.ReorderLevel.ToString());
                            table.Cell().Text($"₱{item.UnitCost:N2}");
                            table.Cell().Text($"₱{item.StockValue:N2}");
                        }
                    });

                    col.Item().PaddingVertical(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                    col.Item().Text("Category Breakdown").Bold().FontSize(12);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Text("Category").Bold();
                            h.Cell().Text("Items").Bold();
                            h.Cell().Text("Total Qty").Bold();
                            h.Cell().Text("Total Value").Bold();
                            h.Cell().Text("%").Bold();
                        });
                        foreach (var c in vm.CategoryBreakdown)
                        {
                            table.Cell().Text(c.Category);
                            table.Cell().Text(c.ItemCount.ToString());
                            table.Cell().Text(c.TotalQty.ToString());
                            table.Cell().Text($"₱{c.TotalValue:N2}");
                            table.Cell().Text($"{c.Percentage}%");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Generated ").FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    t.Span(DateTime.Now.ToString("MMM dd, yyyy HH:mm")).FontSize(8).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                });
            });
        });

        var bytes = pdf.GeneratePdf();
        return File(bytes, "application/pdf", $"Inventory_Report_{DateTime.Now:yyyyMMdd}.pdf");
    }
}
