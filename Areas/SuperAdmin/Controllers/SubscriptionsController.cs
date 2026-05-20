using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class SubscriptionsController : Controller
{
    private readonly ISuperAdminService _service;

    public SubscriptionsController(ISuperAdminService service)
    {
        _service = service;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.SuperAdmin.ToString();
    }

    private static bool IsAjaxRequest(string? requestedWith)
        => string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private long GetUserId() => long.TryParse(User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value, out var id) ? id : 0;
    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? status, string? plan, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = await _service.GetSubscriptionsAsync(search, status, plan, page);
        ViewBag.AvailablePlans = await _service.GetActivePlansAsync();
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var model = await _service.GetSubscriptionDetailsAsync(id);
        if (model == null) return NotFound();
        return PartialView("_DetailsModal", model);
    }

    [HttpGet]
    public async Task<IActionResult> AssignModal()
    {
        if (!IsAuthorized()) return Forbid();
        var shops = await _service.GetShopDropdownAsync();
        var plans = await _service.GetActivePlansAsync();
        var model = new AssignSubscriptionViewModel
        {
            AvailableShops = shops
        };
        ViewBag.Plans = plans;
        return PartialView("_AssignModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(
        AssignSubscriptionViewModel model,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            model.AvailableShops = await _service.GetShopDropdownAsync();
            ViewBag.Plans = await _service.GetActivePlansAsync();
            if (IsAjaxRequest(requestedWith))
                return PartialView("_AssignModal", model);
            return RedirectToAction(nameof(Index));
        }

        var result = await _service.AssignSubscriptionAsync(model.ShopId, model.PlanId, model.BillingCycle, GetUserId(), GetIpAddress());

        if (IsAjaxRequest(requestedWith))
            return Json(new { success = result.Success, message = result.Message, checkoutUrl = result.CheckoutUrl });

        if (result.Success) TempData["Success"] = result.Message;
        else TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(
        long id,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest(requestedWith))
                return Json(new { success = false, message = "Invalid request." });
            return RedirectToAction(nameof(Index));
        }
        var result = await _service.CancelSubscriptionAsync(id, GetUserId(), GetIpAddress());
        if (IsAjaxRequest(requestedWith))
            return Json(new { success = result.Success, message = result.Message });
        return RedirectToAction(nameof(Index));
    }
}
