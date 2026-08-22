using Microsoft.AspNetCore.SignalR;

namespace TradingScanner.Hubs;

public sealed class MarketHub(ILogger<MarketHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Dashboard connected to market hub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation(exception, "Dashboard disconnected from market hub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
