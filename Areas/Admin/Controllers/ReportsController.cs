using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class ReportsController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.Admin.ToString();
    }

    // ─── HUB PAGE ───────────────────────────────────────────
    [HttpGet]
    public IActionResult Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var vm = new ReportIndexViewModel
        {
            Revenue = new() { Title = "Monthly Revenue", Value = "₱87,450", SubText = "vs ₱72,300 last month", Trend = "+20.9%", IsPositive = true },
            Payments = new() { Title = "Payments Collected", Value = "₱68,200", SubText = "142 transactions", Trend = "+12.5%", IsPositive = true },
            Services = new() { Title = "Services Performed", Value = "89", SubText = "across 24 service types", Trend = "+8.2%", IsPositive = true },
            Inventory = new() { Title = "Stock Value", Value = "₱124,500", SubText = "3 items low stock", Trend = "-2.1%", IsPositive = false },
            RecentActivity = new()
            {
                new() { Description = "Revenue report generated", Category = "Revenue", Date = DateTime.Now.AddHours(-2) },
                new() { Description = "Inventory audit completed", Category = "Inventory", Date = DateTime.Now.AddDays(-1) },
                new() { Description = "Monthly payment reconciliation", Category = "Payments", Date = DateTime.Now.AddDays(-3) },
            }
        };
        return View(vm);
    }

    // ─── REVENUE REPORT ─────────────────────────────────────
    [HttpGet]
    public IActionResult Revenue(DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var dateFrom = from ?? DateTime.Today.AddMonths(-6);
        var dateTo = to ?? DateTime.Today;

        var vm = new RevenueReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            TotalRevenue = 524700.00m,
            TotalCollected = 438250.00m,
            TotalOutstanding = 86450.00m,
            AverageInvoice = 2850.00m,
            InvoiceCount = 184,
            MonthlyBreakdown = new()
            {
                new() { Month = "Jan 2025", Invoiced = 72300m, Collected = 65100m, Count = 28 },
                new() { Month = "Feb 2025", Invoiced = 81500m, Collected = 71200m, Count = 31 },
                new() { Month = "Mar 2025", Invoiced = 95200m, Collected = 82400m, Count = 35 },
                new() { Month = "Apr 2025", Invoiced = 88100m, Collected = 78500m, Count = 32 },
                new() { Month = "May 2025", Invoiced = 100150m, Collected = 89750m, Count = 30 },
                new() { Month = "Jun 2025", Invoiced = 87450m, Collected = 51300m, Count = 28 },
            },
            CategoryBreakdown = new()
            {
                new() { Category = "Repair",        Amount = 215400m, Count = 78, Percentage = 41.1m },
                new() { Category = "Installation",  Amount = 128600m, Count = 42, Percentage = 24.5m },
                new() { Category = "Data Recovery",  Amount = 89500m,  Count = 28, Percentage = 17.1m },
                new() { Category = "Maintenance",   Amount = 56200m,  Count = 24, Percentage = 10.7m },
                new() { Category = "Diagnosis",     Amount = 35000m,  Count = 12, Percentage = 6.6m },
            }
        };
        return View(vm);
    }

    // ─── PAYMENT REPORT ─────────────────────────────────────
    [HttpGet]
    public IActionResult Payments(DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var dateFrom = from ?? DateTime.Today.AddDays(-30);
        var dateTo = to ?? DateTime.Today;

        var vm = new PaymentReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            TotalReceived = 68200.00m,
            TransactionCount = 142,
            AveragePayment = 480.28m,
            MethodBreakdown = new()
            {
                new() { Method = "Cash",     Amount = 28500m, Count = 62, Percentage = 43.5m },
                new() { Method = "GCash",    Amount = 22300m, Count = 48, Percentage = 34.1m },
                new() { Method = "Card",     Amount = 17400m, Count = 32, Percentage = 22.4m },
            },
            DailyTrend = Enumerable.Range(0, 14).Select(i => new PaymentByDay
            {
                Date = DateTime.Today.AddDays(-13 + i),
                Total = new Random(i + 42).Next(1500, 8500),
                Count = new Random(i + 42).Next(3, 15)
            }).ToList()
        };
        return View(vm);
    }

    // ─── SERVICE PERFORMANCE REPORT ──────────────────────────
    [HttpGet]
    public IActionResult ServicePerformance(DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var dateFrom = from ?? DateTime.Today.AddMonths(-3);
        var dateTo = to ?? DateTime.Today;

        var vm = new ServicePerformanceReportViewModel
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            TotalJobOrders = 89,
            AverageCompletionDays = 2.3m,
            TotalServiceRevenue = 87450m,
            Services = new()
            {
                new() { ServiceName = "Virus/Malware Removal",  Category = "Repair",       UsageCount = 18, Revenue = 13500m, AveragePrice = 750m },
                new() { ServiceName = "Screen Replacement",     Category = "Repair",       UsageCount = 14, Revenue = 16800m, AveragePrice = 1200m },
                new() { ServiceName = "Data Recovery",          Category = "Data Recovery", UsageCount = 12, Revenue = 18000m, AveragePrice = 1500m },
                new() { ServiceName = "OS Installation",        Category = "Installation", UsageCount = 11, Revenue = 8800m,  AveragePrice = 800m },
                new() { ServiceName = "System Diagnosis",       Category = "Diagnosis",    UsageCount = 10, Revenue = 5000m,  AveragePrice = 500m },
                new() { ServiceName = "Hardware Cleanup",       Category = "Maintenance",  UsageCount = 9,  Revenue = 4050m,  AveragePrice = 450m },
                new() { ServiceName = "RAM Upgrade",            Category = "Installation", UsageCount = 8,  Revenue = 7200m,  AveragePrice = 900m },
                new() { ServiceName = "SSD Installation",       Category = "Installation", UsageCount = 7,  Revenue = 8400m,  AveragePrice = 1200m },
            },
            Categories = new()
            {
                new() { Category = "Repair",        ServiceCount = 6, UsageCount = 38, Revenue = 36800m, Percentage = 42.1m },
                new() { Category = "Installation",  ServiceCount = 5, UsageCount = 26, Revenue = 24400m, Percentage = 27.9m },
                new() { Category = "Data Recovery",  ServiceCount = 2, UsageCount = 12, Revenue = 18000m, Percentage = 20.6m },
                new() { Category = "Maintenance",   ServiceCount = 3, UsageCount = 9,  Revenue = 4050m,  Percentage = 4.6m },
                new() { Category = "Diagnosis",     ServiceCount = 2, UsageCount = 10, Revenue = 5000m,  Percentage = 5.7m },
            }
        };
        return View(vm);
    }

    // ─── INVENTORY REPORT ───────────────────────────────────
    [HttpGet]
    public IActionResult Inventory()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var vm = new InventoryReportViewModel
        {
            TotalItems = 45,
            LowStockItems = 3,
            OutOfStockItems = 1,
            TotalStockValue = 124500m,
            TotalRetailValue = 186750m,
            Items = new()
            {
                new() { SKU = "SCR-LCD-156",  ItemName = "LCD Screen 15.6\"",      Category = "Screens",     QtyOnHand = 8,  ReorderLevel = 5,  UnitCost = 2200m, StockValue = 17600m, IsLowStock = false },
                new() { SKU = "BAT-LP-GEN",   ItemName = "Laptop Battery (Generic)", Category = "Batteries",  QtyOnHand = 3,  ReorderLevel = 5,  UnitCost = 950m,  StockValue = 2850m,  IsLowStock = true },
                new() { SKU = "SSD-256-SAT",   ItemName = "SSD 256GB SATA",          Category = "Storage",     QtyOnHand = 15, ReorderLevel = 10, UnitCost = 1800m, StockValue = 27000m, IsLowStock = false },
                new() { SKU = "RAM-8G-DDR4",   ItemName = "RAM 8GB DDR4",            Category = "Memory",      QtyOnHand = 12, ReorderLevel = 8,  UnitCost = 1200m, StockValue = 14400m, IsLowStock = false },
                new() { SKU = "TP-MX4-001",    ItemName = "Thermal Paste MX-4",      Category = "Accessories", QtyOnHand = 25, ReorderLevel = 10, UnitCost = 350m,  StockValue = 8750m,  IsLowStock = false },
                new() { SKU = "KBD-USB-GEN",   ItemName = "USB Keyboard",            Category = "Peripherals", QtyOnHand = 2,  ReorderLevel = 5,  UnitCost = 450m,  StockValue = 900m,   IsLowStock = true },
                new() { SKU = "CHG-USB-C",     ItemName = "USB-C Charger 65W",       Category = "Chargers",    QtyOnHand = 0,  ReorderLevel = 3,  UnitCost = 800m,  StockValue = 0m,     IsLowStock = true },
                new() { SKU = "FAN-LP-GEN",    ItemName = "Laptop Cooling Fan",      Category = "Parts",       QtyOnHand = 6,  ReorderLevel = 3,  UnitCost = 550m,  StockValue = 3300m,  IsLowStock = false },
            },
            CategoryBreakdown = new()
            {
                new() { Category = "Storage",     ItemCount = 8,  TotalQty = 42,  TotalValue = 45600m, Percentage = 36.6m },
                new() { Category = "Screens",     ItemCount = 6,  TotalQty = 18,  TotalValue = 28200m, Percentage = 22.7m },
                new() { Category = "Memory",      ItemCount = 5,  TotalQty = 28,  TotalValue = 21600m, Percentage = 17.4m },
                new() { Category = "Accessories", ItemCount = 10, TotalQty = 65,  TotalValue = 14500m, Percentage = 11.6m },
                new() { Category = "Batteries",   ItemCount = 4,  TotalQty = 8,   TotalValue = 6800m,  Percentage = 5.5m },
                new() { Category = "Parts",       ItemCount = 12, TotalQty = 35,  TotalValue = 7800m,  Percentage = 6.2m },
            }
        };
        return View(vm);
    }
}
