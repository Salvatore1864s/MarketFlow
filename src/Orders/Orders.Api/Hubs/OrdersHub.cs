
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Orders.Api.Hubs;

[Authorize(Roles = "Buyer")]
public class OrdersHub : Hub
{
    private readonly ILogger<OrdersHub> _logger;

    public OrdersHub(ILogger<OrdersHub> logger)
    {
        _logger = logger;
    }

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
            _logger.LogWarning(
                "SignalR connection rejected: Buyer ID not found.");

            Context.Abort();
            return;
        }

        var groupName = GetBuyerGroupName(buyerId);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            groupName);

        _logger.LogInformation(
            "Buyer {BuyerId} connected to SignalR. Group: {GroupName}",
            buyerId,
            groupName);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "SignalR connection disconnected. ConnectionId: {ConnectionId}",
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
