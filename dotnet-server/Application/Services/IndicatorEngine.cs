using System.Collections.Concurrent;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Models;

namespace TradingScanner.Application.Services;

/// <summary>Maintains bounded per-symbol bar history and session VWAP without rescanning a data store.</summary>
public sealed class IndicatorEngine(IRelativeVolumeService relativeVolume) : IIndicatorEngine
{
    private const int Capacity = 30;
    private static readonly TimeZoneInfo MarketTimeZone = ResolveMarketTimeZone();
    private readonly ConcurrentDictionary<string, Accumulator> _symbols = new(StringComparer.OrdinalIgnoreCase);

    public void Update(TickerState state, MinuteBar bar)
    {
        var accumulator = _symbols.GetOrAdd(state.Symbol, _ => new Accumulator());
        lock (accumulator)
        {
            var marketDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(bar.Timestamp, MarketTimeZone).Date);
            if (accumulator.MarketDate != marketDate)
            {
                accumulator.Bars.Clear();
                accumulator.PriceVolume = 0;
                accumulator.Volume = 0;
                accumulator.MarketDate = marketDate;
            }

            // A provider may correct its latest bar. Replace it instead of inflating the indicators.
            if (accumulator.Bars.Count > 0 && accumulator.Bars[^1].Timestamp == bar.Timestamp)
            {
                var replaced = accumulator.Bars[^1];
                accumulator.PriceVolume -= TypicalPrice(replaced) * replaced.Volume;
                accumulator.Volume -= replaced.Volume;
                accumulator.Bars[^1] = bar;
            }
            else
            {
                accumulator.Bars.Add(bar);
                if (accumulator.Bars.Count > Capacity) accumulator.Bars.RemoveAt(0);
            }

            accumulator.PriceVolume += TypicalPrice(bar) * bar.Volume;
            accumulator.Volume += bar.Volume;
            state.Vwap = accumulator.Volume == 0 ? 0 : accumulator.PriceVolume / accumulator.Volume;
            state.OneMinuteChangePercent = Change(bar.Close, CloseAt(accumulator.Bars, 1));
            state.ThreeMinuteChangePercent = Change(bar.Close, CloseAt(accumulator.Bars, 3));
            state.FiveMinuteChangePercent = Change(bar.Close, CloseAt(accumulator.Bars, 5));
            state.RelativeVolume = relativeVolume.Calculate(state.Symbol, bar.Volume, accumulator.Bars);
        }
    }

    private static decimal TypicalPrice(MinuteBar bar) => (bar.High + bar.Low + bar.Close) / 3;
    private static decimal CloseAt(IReadOnlyList<MinuteBar> bars, int periods) =>
        bars.Count > periods ? bars[^(periods + 1)].Close : 0;
    private static decimal Change(decimal current, decimal previous) =>
        previous == 0 ? 0 : (current - previous) / previous * 100;
    private static TimeZoneInfo ResolveMarketTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    }

    private sealed class Accumulator
    {
        public List<MinuteBar> Bars { get; } = [];
        public decimal PriceVolume { get; set; }
        public long Volume { get; set; }
        public DateOnly MarketDate { get; set; }
    }
}

/// <summary>Default RVOL policy; replace this registration with a historical profile when available.</summary>
public sealed class RollingRelativeVolumeService : IRelativeVolumeService
{
    public decimal Calculate(string symbol, long currentVolume, IReadOnlyList<MinuteBar> bars)
    {
        if (bars.Count < 2) return 0;
        var comparison = bars.Take(bars.Count - 1).Where(x => x.Volume > 0).Select(x => x.Volume).ToArray();
        if (comparison.Length == 0) return 0;
        return currentVolume / (decimal)comparison.Average();
    }
}
