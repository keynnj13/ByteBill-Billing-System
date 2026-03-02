using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class PaymentsController : Controller
{
    private readonly ISuperAdminService _service;

    public PaymentsController(ISuperAdminService service)
    {
        _service = service;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.SuperAdmin.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status, string? method, DateTime? from, DateTime? to, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = await _service.GetPaymentsAsync(search, status, method, from, to, page);
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var model = await _service.GetPaymentDetailsAsync(id);
        if (model == null) return NotFound();
        return PartialView("_DetailsModal", model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? status, string? method, DateTime? from, DateTime? to)
    {
        if (!IsAuthorized()) return Forbid();

        var data = await _service.GetPaymentsAsync(null, status, method, from, to, 1, 10000);
        var csv = "Reference,Shop,Plan,Amount,Status,Method,Period Start,Period End,Paid At\n";
        foreach (var p in data.Payments)
        {
            csv += $"\"{p.ReferenceNumber}\",\"{p.ShopName}\",\"{p.PlanName}\",\"{p.Amount:N2}\",\"{p.Status}\",\"{p.PaymentMethod}\",\"{p.PeriodStart:yyyy-MM-dd}\",\"{p.PeriodEnd:yyyy-MM-dd}\",\"{p.PaidAt?.ToString("yyyy-MM-dd HH:mm") ?? "—"}\"\n";
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"subscription-payments-{DateTime.Now:yyyyMMdd}.csv");
    }
}
