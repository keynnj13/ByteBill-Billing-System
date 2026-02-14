using ByteBill_BS.Models.Enums;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class SystemLogsController : Controller
{
    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.SuperAdmin.ToString();
    }

    [HttpGet]
    public IActionResult Index(string? search, string? type, string? shop, DateTime? dateFrom, DateTime? dateTo, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var allLogs = GetDemoLogs();

        if (!string.IsNullOrWhiteSpace(search))
            allLogs = allLogs
                .Where(l => l.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || l.UserName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        if (!string.IsNullOrEmpty(type))
            allLogs = allLogs.Where(l => l.Type == type).ToList();
        if (!string.IsNullOrEmpty(shop))
            allLogs = allLogs.Where(l => l.ShopName == shop).ToList();
        if (dateFrom.HasValue)
            allLogs = allLogs.Where(l => l.Timestamp >= dateFrom.Value).ToList();
        if (dateTo.HasValue)
            allLogs = allLogs.Where(l => l.Timestamp <= dateTo.Value.AddDays(1)).ToList();

        var viewModel = new SystemLogListViewModel
        {
            SearchTerm = search,
            TypeFilter = type,
            ShopFilter = shop,
            DateFrom = dateFrom,
            DateTo = dateTo,
            CurrentPage = page,
            TotalCount = allLogs.Count,
            TodayCount = allLogs.Count(l => l.Timestamp.Date == DateTime.Today),
            ErrorCount = allLogs.Count(l => l.Type == "Error" || l.Type == "Critical"),
            WarningCount = allLogs.Count(l => l.Type == "Warning"),
            InfoCount = allLogs.Count(l => l.Type == "Info"),
            Logs = allLogs.OrderByDescending(l => l.Timestamp).Skip((page - 1) * 20).Take(20).ToList(),
            AvailableShops = new() { "TechFix Pro", "QuickRepairs", "ComputerMD", "OldTech Solutions", "GadgetCare PH" }
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_DetailsModal", GetDetailModel(id));
    }

    [HttpGet]
    public IActionResult ExportCsv(string? type, string? shop, DateTime? dateFrom, DateTime? dateTo)
    {
        if (!IsAuthorized()) return Forbid();

        var logs = GetDemoLogs();
        if (!string.IsNullOrEmpty(type)) logs = logs.Where(l => l.Type == type).ToList();
        if (!string.IsNullOrEmpty(shop)) logs = logs.Where(l => l.ShopName == shop).ToList();

        var csv = "Timestamp,Type,Message,User,Shop,IP Address,Source\n";
        foreach (var log in logs.OrderByDescending(l => l.Timestamp))
        {
            csv += $"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{log.Type}\",\"{log.Message}\",\"{log.UserName}\",\"{log.ShopName}\",\"{log.IpAddress}\",\"{log.Source}\"\n";
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"system-logs-{DateTime.Now:yyyyMMdd}.csv");
    }

    [HttpGet]
    public IActionResult ExportPdf(string? type, string? shop, DateTime? dateFrom, DateTime? dateTo)
    {
        if (!IsAuthorized()) return Forbid();
        // In production, generate a real PDF. For now, return a placeholder.
        return Content("PDF export would be generated here with a library like QuestPDF or iText.", "text/plain");
    }

    // ── Demo Data ──────────────────────────────────────────────
    private static List<SystemLogItemViewModel> GetDemoLogs() => new()
    {
        new() { Id = 1,  Type = "Info",     Message = "User login successful",              UserName = "John Anderson",  UserEmail = "john@techfixpro.com",    ShopName = "TechFix Pro",       IpAddress = "192.168.1.100", Source = "AuthController",     Timestamp = DateTime.Now.AddMinutes(-5) },
        new() { Id = 2,  Type = "Warning",  Message = "Failed login attempt (3rd try)",     UserName = "unknown",        UserEmail = "unknown@test.com",        ShopName = null,                IpAddress = "10.0.0.50",     Source = "AuthController",     Timestamp = DateTime.Now.AddMinutes(-15) },
        new() { Id = 3,  Type = "Info",     Message = "Invoice INV-2025-0145 created",      UserName = "Emily Brown",    UserEmail = "emily@techfixpro.com",   ShopName = "TechFix Pro",       IpAddress = "192.168.1.101", Source = "InvoicesController", Timestamp = DateTime.Now.AddMinutes(-30) },
        new() { Id = 4,  Type = "Error",    Message = "Payment gateway timeout — PayMongo", UserName = "System",         UserEmail = null,                      ShopName = null,                IpAddress = "N/A",           Source = "PaymentService",     Timestamp = DateTime.Now.AddHours(-1) },
        new() { Id = 5,  Type = "Info",     Message = "Shop settings updated",              UserName = "Mike Chen",      UserEmail = "mike@computermd.com",    ShopName = "ComputerMD",        IpAddress = "192.168.1.105", Source = "SettingsController", Timestamp = DateTime.Now.AddHours(-2) },
        new() { Id = 6,  Type = "Info",     Message = "New customer registered: Carol White", UserName = "Sarah Miller", UserEmail = "sarah@quickrepairs.com", ShopName = "QuickRepairs",      IpAddress = "192.168.1.110", Source = "CustomersController",Timestamp = DateTime.Now.AddHours(-3) },
        new() { Id = 7,  Type = "Warning",  Message = "Inventory low: Screen Protector (5 left)", UserName = "System",   UserEmail = null,                      ShopName = "TechFix Pro",       IpAddress = "N/A",           Source = "InventoryService",   Timestamp = DateTime.Now.AddHours(-4) },
        new() { Id = 8,  Type = "Critical", Message = "Database connection pool exhausted", UserName = "System",         UserEmail = null,                      ShopName = null,                IpAddress = "N/A",           Source = "DbContext",          Timestamp = DateTime.Now.AddHours(-5) },
        new() { Id = 9,  Type = "Info",     Message = "Job order JO-2025-0089 completed",   UserName = "David Lee",      UserEmail = "david@techfixpro.com",   ShopName = "TechFix Pro",       IpAddress = "192.168.1.102", Source = "JobOrdersController",Timestamp = DateTime.Now.AddHours(-6) },
        new() { Id = 10, Type = "Info",     Message = "Payment ₱1,200.00 recorded",         UserName = "Emily Brown",    UserEmail = "emily@techfixpro.com",   ShopName = "TechFix Pro",       IpAddress = "192.168.1.101", Source = "PaymentsController", Timestamp = DateTime.Now.AddHours(-7) },
        new() { Id = 11, Type = "Error",    Message = "Xero sync failed: Invalid token",    UserName = "System",         UserEmail = null,                      ShopName = "ComputerMD",        IpAddress = "N/A",           Source = "XeroSyncService",    Timestamp = DateTime.Now.AddHours(-8) },
        new() { Id = 12, Type = "Info",     Message = "New shop registered: GadgetCare PH", UserName = "Super Admin",    UserEmail = "admin@bytebill.ph",      ShopName = "GadgetCare PH",     IpAddress = "192.168.1.1",   Source = "ShopsController",    Timestamp = DateTime.Now.AddDays(-1) },
    };

    private SystemLogDetailViewModel GetDetailModel(long id) => new()
    {
        Id = id, Type = "Error", Message = "Payment gateway timeout — PayMongo",
        UserName = "System", UserEmail = null, ShopName = null,
        IpAddress = "N/A", Source = "PaymentService",
        StackTrace = "System.Net.Http.HttpRequestException: A connection attempt failed\n   at System.Net.Http.HttpConnectionPool.ConnectAsync()\n   at ByteBill_BS.Services.PaymentService.ProcessPaymentAsync()\n   at ByteBill_BS.Controllers.PaymentsController.Create()",
        RequestUrl = "/Admin/Payments/Create",
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
        Timestamp = DateTime.Now.AddHours(-1)
    };
}
