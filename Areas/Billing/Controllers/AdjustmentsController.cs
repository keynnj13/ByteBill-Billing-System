using ByteBill_BS.Data;
using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBill_BS.Areas.Billing.Controllers;

[Area("Billing")]
[Authorize]
public class AdjustmentsController : Controller
{
    private readonly IAdjustmentService _adjustmentService;
    private readonly ApplicationDbContext _db;

    public AdjustmentsController(IAdjustmentService adjustmentService, ApplicationDbContext db)
    {
        _adjustmentService = adjustmentService;
        _db = db;
    }

    private bool IsAuthorized()
    {
        var role = User.GetRole();
        return role is "Billing" or "Admin" or "SuperAdmin";
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var metrics = await _adjustmentService.GetUserMetricsAsync(shopId, userId);
        var adjustments = await _adjustmentService.GetByUserAsync(shopId, userId);

        // Get invoices for the create form
        var invoices = await _db.Invoices
            .Where(i => i.ShopId == shopId && i.Status != InvoiceStatus.Void)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new { i.InvoiceId, i.InvoiceNo, CustomerName = i.Customer != null ? i.Customer.FullName : "", i.Balance })
            .ToListAsync();

        ViewBag.Metrics = metrics;
        ViewBag.Adjustments = adjustments;
        ViewBag.Invoices = invoices;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAdjustmentRequest request)
    {
        if (!IsAuthorized()) return Forbid();

        try
        {
            var shopId = User.GetShopId();
            var userId = User.GetUserId();
            await _adjustmentService.CreateAsync(shopId, userId, request);
            TempData["Success"] = "Adjustment request submitted successfully. Awaiting admin approval.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
