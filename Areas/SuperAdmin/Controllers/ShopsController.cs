using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class ShopsController : Controller
{
    private readonly ISuperAdminService _service;

    public ShopsController(ISuperAdminService service)
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

        var viewModel = await _service.GetShopsAsync(search, status, plan, page);
        return View(viewModel);
    }

    // NOTE: Shop creation is now handled by self-service registration.
    // SuperAdmin retains read-only oversight with ToggleStatus for moderation.

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var model = await _service.GetShopDetailsAsync(id);
        if (model == null) return NotFound();
        return PartialView("_DetailsModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(
        long id,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();
        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest(requestedWith))
                return Json(new { success = false, message = "Invalid request." });
            return BadRequest();
        }
        var result = await _service.ToggleShopStatusAsync(id, GetUserId(), GetIpAddress());
        if (IsAjaxRequest(requestedWith))
            return Json(new { success = result.Success, message = result.Message });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var model = await _service.GetShopForEditAsync(id);
        if (model == null) return NotFound();
        return PartialView("_EditModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        ShopFormViewModel model,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest(requestedWith))
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }
            return PartialView("_EditModal", model);
        }

        var result = await _service.UpdateShopAsync(model, GetUserId(), GetIpAddress());
        if (IsAjaxRequest(requestedWith))
            return Json(new { success = result.Success, message = result.Message });
        return RedirectToAction(nameof(Index));
    }

}
