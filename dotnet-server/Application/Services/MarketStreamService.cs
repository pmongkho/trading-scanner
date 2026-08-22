using TradingScanner.Application.Interfaces;

namespace TradingScanner.Application.Services;

/// <summary>Connects the selected provider to the state projection for the application's lifetime.</summary>
public sealed class MarketStreamService(
    IMarketDataProvider provider,
    ITickerStateManager states,
    ILogger<MarketStreamService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        provider.TradeReceived += trade => { states.Apply(trade); return ValueTask.CompletedTask; };
        provider.QuoteReceived += quote => { states.Apply(quote); return ValueTask.CompletedTask; };
        provider.MinuteBarReceived += bar => { states.Apply(bar); return ValueTask.CompletedTask; };
        try { await provider.RunAsync(stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception) { logger.LogCritical(exception, "Market stream stopped unexpectedly"); }
    }
}
