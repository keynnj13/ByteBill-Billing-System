using ByteBill_BS.Models.Enums;
using ByteBill_BS.Services;
using ByteBill_BS.ViewModels.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize]
public class AnnouncementsController : Controller
{
    private readonly ISuperAdminService _service;

    public AnnouncementsController(ISuperAdminService service)
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

    [HttpGet]
    public async Task<IActionResult> Index(string? search, string? type, string? status, int page = 1)
    {
        if (!IsAuthorized()) return RedirectToAction("AccessDenied", "Auth", new { area = "" });

        var viewModel = await _service.GetAnnouncementsAsync(search, type, status, page);
        return View(viewModel);
    }

    [HttpGet]
    public IActionResult CreateModal()
    {
        if (!IsAuthorized()) return Forbid();
        return PartialView("_FormModal", new AnnouncementFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AnnouncementFormViewModel model,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest(requestedWith))
                return PartialView("_FormModal", model);
            return RedirectToAction(nameof(Index));
        }

        var result = await _service.CreateAnnouncementAsync(model, GetUserId());

        if (IsAjaxRequest(requestedWith))
            return Json(new { success = result.Success, message = result.Message });

        if (result.Success) TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditModal(long id)
    {
        if (!IsAuthorized()) return Forbid();
        var model = await _service.GetAnnouncementForEditAsync(id);
        if (model == null) return NotFound();
        return PartialView("_FormModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        AnnouncementFormViewModel model,
        [FromHeader(Name = "X-Requested-With")] string? requestedWith)
    {
        if (!IsAuthorized()) return Forbid();

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest(requestedWith))
                return PartialView("_FormModal", model);
            return RedirectToAction(nameof(Index));
        }

        var result = await _service.UpdateAnnouncementAsync(model, GetUserId());

        if (IsAjaxRequest(requestedWith))
            return Json(new { success = result.Success, message = result.Message });

        if (result.Success) TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(
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
        var result = await _service.PublishAnnouncementAsync(id, GetUserId());
        if (IsAjaxRequest(requestedWith))
            return Json(new { success = result.Success, message = result.Message });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
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
        var result = await _service.DeleteAnnouncementAsync(id, GetUserId());
        if (IsAjaxRequest(requestedWith))
            return Json(new { success = result.Success, message = result.Message });
        return RedirectToAction(nameof(Index));
    }
}
