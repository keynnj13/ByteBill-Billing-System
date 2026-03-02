using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public async Task<IActionResult> Index(string report = "revenue", string? from = null, string? to = null)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var dateFrom = DateTime.TryParse(from, out var df) ? df : DateTime.UtcNow.AddMonths(-6);
        var dateTo = DateTime.TryParse(to, out var dt) ? dt : DateTime.UtcNow;

        var vm = await _service.GenerateReportAsync(report, dateFrom, dateTo);
        return View(vm);
    }

    public async Task<IActionResult> ExportCsv(string report = "revenue", string? from = null, string? to = null)
    {
        if (!IsAuthorized()) return Forbid();

        var dateFrom = DateTime.TryParse(from, out var df) ? df : DateTime.UtcNow.AddMonths(-6);
        var dateTo = DateTime.TryParse(to, out var dt) ? dt : DateTime.UtcNow;

        var csv = await _service.ExportReportCsvAsync(report, dateFrom, dateTo);
        return File(csv, "text/csv", $"ByteBill_{report}_report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
