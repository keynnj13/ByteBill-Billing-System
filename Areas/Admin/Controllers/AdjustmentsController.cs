using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
public class AdjustmentsController : Controller
{
    private readonly IAdjustmentService _adjustmentService;

    public AdjustmentsController(IAdjustmentService adjustmentService)
        => _adjustmentService = adjustmentService;

    private bool IsAuthorized()
    {
        var role = User.GetRole();
        return role is "Admin" or "SuperAdmin";
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var shopId = User.GetShopId();
        var metrics = await _adjustmentService.GetMetricsAsync(shopId);
        var pending = await _adjustmentService.GetAllAsync(shopId, AdjustmentStatus.Pending);
        var history = await _adjustmentService.GetAllAsync(shopId);

        ViewBag.Metrics = metrics;
        ViewBag.PendingRequests = pending;
        ViewBag.AllAdjustments = history.Where(a => a.Status != "Pending").ToList();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _adjustmentService.ApproveAsync(id, shopId, userId);

        TempData[result ? "Success" : "Error"] = result
            ? "Adjustment approved and applied to invoice."
            : "Adjustment not found or already processed.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id)
    {
        if (!IsAuthorized()) return Forbid();

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _adjustmentService.RejectAsync(id, shopId, userId);

        TempData[result ? "Success" : "Error"] = result
            ? "Adjustment rejected."
            : "Adjustment not found or already processed.";

        return RedirectToAction(nameof(Index));
    }
}
