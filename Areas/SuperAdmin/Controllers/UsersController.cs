using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class UsersController : Controller
{
    private readonly ISuperAdminService _service;

    public UsersController(ISuperAdminService service)
    {
        _service = service;
    }

    private bool IsAuthorized()
    {
        var roleClaim = User.Claims.FirstOrDefault(c => c.Type == "Role")?.Value;
        return roleClaim == UserRole.SuperAdmin.ToString();
    }

    private long GetUserId() => long.TryParse(User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value, out var id) ? id : 0;
    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> Index(string? search, UserRole? role, string? shop, string? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = await _service.GetUsersAsync(search, role, shop, status, page);
        return View(viewModel);
    }

    // NOTE: User creation is now handled by self-service registration (Admin role created during shop signup).
    // Shop admins manage their own users via the Admin area.
    // SuperAdmin retains read-only oversight with ToggleStatus and ResetPassword for support.

    [HttpGet]
    public async Task<IActionResult> DetailsModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var model = await _service.GetUserDetailsAsync(id);
        if (model == null) return NotFound();
        return PartialView("_DetailsModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var result = await _service.ToggleUserStatusAsync(id, GetUserId(), GetIpAddress());
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = result.Success, message = result.Message });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(long id, string? newPassword)
    {
        if (!IsAuthorized()) return Forbid();
        var password = newPassword ?? "ByteBill@123";
        var result = await _service.ResetUserPasswordAsync(id, password, GetUserId(), GetIpAddress());
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = result.Success, message = result.Message });
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var model = await _service.GetUserForEditAsync(id);
        if (model == null) return NotFound();
        return PartialView("_EditModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(GlobalUserFormViewModel model)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }
            // Reload available shops for the dropdown
            var freshModel = await _service.GetUserForEditAsync(model.Id);
            if (freshModel != null) model.AvailableShops = freshModel.AvailableShops;
            return PartialView("_EditModal", model);
        }

        var result = await _service.UpdateUserAsync(model, GetUserId(), GetIpAddress());
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Json(new { success = result.Success, message = result.Message });
        return RedirectToAction(nameof(Index));
    }
}
