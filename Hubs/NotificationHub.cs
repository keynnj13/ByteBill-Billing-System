using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ByteBill_BS.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Group by ShopId so we can broadcast to all users in a shop if needed
        var shopId = Context.User?.FindFirst("ShopId")?.Value;
        if (!string.IsNullOrEmpty(shopId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"shop-{shopId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var shopId = Context.User?.FindFirst("ShopId")?.Value;
        if (!string.IsNullOrEmpty(shopId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"shop-{shopId}");

        await base.OnDisconnectedAsync(exception);
    }
}
