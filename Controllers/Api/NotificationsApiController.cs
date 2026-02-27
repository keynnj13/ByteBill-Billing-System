using ByteBill_BS.Extensions;
using ByteBill_BS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Controllers.Api;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsApiController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsApiController(INotificationService notificationService)
        => _notificationService = notificationService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = User.GetUserId();
        if (userId == 0) return Unauthorized();

        var notifications = await _notificationService.GetByUserAsync(userId);
        var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

        return Ok(new { notifications, unreadCount });
    }

    [HttpPost("read/{id}")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var userId = User.GetUserId();
        if (userId == 0) return Unauthorized();

        await _notificationService.MarkAsReadAsync(id, userId);
        return Ok();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = User.GetUserId();
        if (userId == 0) return Unauthorized();

        await _notificationService.MarkAllReadAsync(userId);
        return Ok();
    }
}
