
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Orders.Api.Hubs;

[Authorize(Roles = "Buyer")]
public class OrdersHub : Hub
{
    public static string GetBuyerGroupName(string buyerId)
    {
        return $"buyer_{buyerId}";
    }

    public override async Task OnConnectedAsync()
    {
        var buyerId = Context.User?.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(buyerId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetBuyerGroupName(buyerId));

        await base.OnConnectedAsync();
    }
}
