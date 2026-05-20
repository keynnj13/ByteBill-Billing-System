using ByteBill_BS.Extensions;
using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.Adjustments;
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
        var typeConfigs = await _adjustmentService.GetTypeConfigsAsync(shopId);

        ViewBag.Metrics = metrics;
        ViewBag.PendingRequests = pending;
        ViewBag.AllAdjustments = history.Where(a => a.Status != "Pending").ToList();
        ViewBag.TypeConfigs = typeConfigs;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long id)
    {
        if (!IsAuthorized()) return Forbid();

        if (id <= 0)
            ModelState.AddModelError(nameof(id), "Invalid request.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid request.";
            return RedirectToAction(nameof(Index));
        }

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

        if (id <= 0)
            ModelState.AddModelError(nameof(id), "Invalid request.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid request.";
            return RedirectToAction(nameof(Index));
        }

        var shopId = User.GetShopId();
        var userId = User.GetUserId();
        var result = await _adjustmentService.RejectAsync(id, shopId, userId);

        TempData[result ? "Success" : "Error"] = result
            ? "Adjustment rejected."
            : "Adjustment not found or already processed.";

        return RedirectToAction(nameof(Index));
    }

    // ── Adjustment Type Config CRUD ──────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTypeConfig(AdjustmentTypeConfigCreateViewModel model)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid adjustment type data.";
            return RedirectToAction(nameof(Index));
        }

        var shopId = User.GetShopId();
        await _adjustmentService.CreateTypeConfigAsync(shopId, model.Name, model.Category, model.Percentage);
        TempData["Success"] = $"Adjustment type '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTypeConfig(AdjustmentTypeConfigUpdateViewModel model)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid adjustment type data.";
            return RedirectToAction(nameof(Index));
        }

        var shopId = User.GetShopId();
        var result = await _adjustmentService.UpdateTypeConfigAsync(
            shopId,
            model.ConfigId,
            model.Name,
            model.Category,
            model.Percentage,
            model.IsActive);
        TempData[result ? "Success" : "Error"] = result ? "Type config updated." : "Config not found.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTypeConfig(long configId)
    {
        if (!IsAuthorized()) return Forbid();

        if (configId <= 0)
            ModelState.AddModelError(nameof(configId), "Invalid request.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid request.";
            return RedirectToAction(nameof(Index));
        }

        var shopId = User.GetShopId();
        var result = await _adjustmentService.DeleteTypeConfigAsync(shopId, configId);
        TempData[result ? "Success" : "Error"] = result ? "Type config removed." : "Config not found.";
        return RedirectToAction(nameof(Index));
    }
}
