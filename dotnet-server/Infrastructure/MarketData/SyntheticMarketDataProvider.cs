using Microsoft.Extensions.Options;
using TradingScanner.Application.Configuration;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Models;

namespace TradingScanner.Infrastructure.MarketData;

/// <summary>Deterministic local feed for development, demos, and integration tests.</summary>
public sealed class SyntheticMarketDataProvider(IOptions<ScannerSettings> settings) : IMarketDataProvider
{
    private readonly MarketStreamSettings _settings = settings.Value.MarketStream;
    public event Func<MarketTrade, ValueTask>? TradeReceived;
    public event Func<MarketQuote, ValueTask>? QuoteReceived;
    public event Func<MinuteBar, ValueTask>? MinuteBarReceived;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var symbols = _settings.Symbols.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (symbols.Length == 0) throw new InvalidOperationException("At least one synthetic symbol is required.");
        var prices = symbols.Select((symbol, index) => (symbol, price: 5m + index * 2m))
            .ToDictionary(x => x.symbol, x => x.price, StringComparer.OrdinalIgnoreCase);
        var random = new Random(1729);
        var tick = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_settings.SyntheticIntervalMilliseconds));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var symbol in symbols)
            {
                var previous = prices[symbol];
                var price = Math.Max(.01m, previous + (decimal)(random.NextDouble() - .48) * .04m);
                prices[symbol] = decimal.Round(price, 4);
                var size = random.Next(1, 20) * 100L;
                if (TradeReceived is not null) await TradeReceived(new(symbol, prices[symbol], size, now));
                if (QuoteReceived is not null) await QuoteReceived(new(symbol, prices[symbol] - .01m, prices[symbol] + .01m, now));
                if (tick % 4 == 0 && MinuteBarReceived is not null)
                    await MinuteBarReceived(new(symbol, previous, Math.Max(previous, price), Math.Min(previous, price), price, size * 4, now));
            }
            tick++;
        }
    }
}
