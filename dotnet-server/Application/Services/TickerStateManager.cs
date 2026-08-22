using System.Collections.Concurrent;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Models;

namespace TradingScanner.Application.Services;

/// <summary>Thread-safe, in-memory projection of normalized market events by symbol.</summary>
public sealed class TickerStateManager : ITickerStateManager
{
    private readonly ConcurrentDictionary<string, Entry> _states = new(StringComparer.OrdinalIgnoreCase);

    public void Apply(MarketTrade trade)
    {
        if (!IsValidSymbol(trade.Symbol) || trade.Price <= 0 || trade.Size <= 0) return;
        var entry = GetEntry(trade.Symbol);
        lock (entry.Gate)
        {
            var state = entry.State;
            state.Price = trade.Price;
            state.HighOfDay = state.HighOfDay == 0 ? trade.Price : Math.Max(state.HighOfDay, trade.Price);
            state.LowOfDay = state.LowOfDay == 0 ? trade.Price : Math.Min(state.LowOfDay, trade.Price);
            state.Volume += trade.Size;
            state.ChangePercent = PercentChange(trade.Price, state.PreviousClose);
            state.LastUpdated = Max(state.LastUpdated, trade.Timestamp);
        }
    }

    public void Apply(MarketQuote quote)
    {
        if (!IsValidSymbol(quote.Symbol) || quote.Bid < 0 || quote.Ask < quote.Bid) return;
        var entry = GetEntry(quote.Symbol);
        lock (entry.Gate)
        {
            entry.State.Bid = quote.Bid;
            entry.State.Ask = quote.Ask;
            entry.State.Spread = quote.Ask - quote.Bid;
            entry.State.SpreadPercent = quote.Ask == 0 ? 0 : entry.State.Spread / quote.Ask * 100;
            entry.State.LastUpdated = Max(entry.State.LastUpdated, quote.Timestamp);
        }
    }

    public void Apply(MinuteBar bar)
    {
        if (!IsValidSymbol(bar.Symbol) || bar.Open <= 0 || bar.High <= 0 || bar.Low <= 0 || bar.Close <= 0 || bar.Volume < 0) return;
        var entry = GetEntry(bar.Symbol);
        lock (entry.Gate)
        {
            var state = entry.State;
            if (state.Open == 0) state.Open = bar.Open;
            state.Price = bar.Close;
            state.High = state.High == 0 ? bar.High : Math.Max(state.High, bar.High);
            state.Low = state.Low == 0 ? bar.Low : Math.Min(state.Low, bar.Low);
            state.HighOfDay = state.HighOfDay == 0 ? bar.High : Math.Max(state.HighOfDay, bar.High);
            state.LowOfDay = state.LowOfDay == 0 ? bar.Low : Math.Min(state.LowOfDay, bar.Low);
            state.PreviousOneMinuteVolume = state.OneMinuteVolume;
            state.OneMinuteVolume = bar.Volume;
            state.VolumeAcceleration = state.PreviousOneMinuteVolume == 0 ? 0
                : (decimal)bar.Volume / state.PreviousOneMinuteVolume;
            state.ChangePercent = PercentChange(bar.Close, state.PreviousClose);
            state.LastUpdated = Max(state.LastUpdated, bar.Timestamp);
        }
    }

    public IReadOnlyCollection<TickerState> Snapshot() => _states.Values.Select(Copy).ToArray();

    public bool TryGet(string symbol, out TickerState? state)
    {
        if (_states.TryGetValue(symbol, out var entry)) { state = Copy(entry); return true; }
        state = null;
        return false;
    }

    private Entry GetEntry(string symbol) => _states.GetOrAdd(symbol.Trim().ToUpperInvariant(),
        static value => new Entry(new TickerState { Symbol = value }));
    private static bool IsValidSymbol(string? symbol) => !string.IsNullOrWhiteSpace(symbol);
    private static decimal PercentChange(decimal price, decimal basis) => basis == 0 ? 0 : (price - basis) / basis * 100;
    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;
    private static TickerState Copy(Entry entry) { lock (entry.Gate) return entry.State.Clone(); }

    private sealed record Entry(TickerState State) { public object Gate { get; } = new(); }
}
