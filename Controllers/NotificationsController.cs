using ByteBill_BS.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBill_BS.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    [HttpGet("/Notifications")]
    public IActionResult Index() => View();
}
