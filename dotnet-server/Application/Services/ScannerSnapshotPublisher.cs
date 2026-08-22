using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TradingScanner.Application.Configuration;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Models;
using TradingScanner.Hubs;

namespace TradingScanner.Application.Services;

/// <summary>
/// Coalesces high-frequency market events into a bounded stream of immutable client snapshots.
/// Slow clients are handled by SignalR rather than blocking ingestion locks.
/// </summary>
public sealed class ScannerSnapshotPublisher(
    ITickerStateManager states,
    IFilteredViewService filteredView,
    IMarketHeatService heat,
    IMarketSessionService sessions,
    IHubContext<MarketHub, IMarketClient> hub,
    IOptions<ScannerSettings> options,
    TimeProvider clock,
    ILogger<ScannerSnapshotPublisher> logger) : BackgroundService
{
    private long _sequence;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(options.Value.FrontendUpdateMilliseconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = clock.GetUtcNow();
                var all = states.Snapshot();
                var snapshot = new ScannerSnapshot(
                    Interlocked.Increment(ref _sequence), now, sessions.GetSession(now),
                    heat.Calculate(all), filteredView.Filter(all));
                await hub.Clients.All.ScannerSnapshotReceived(snapshot);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Scanner snapshot publisher stopped unexpectedly");
        }
    }
}
