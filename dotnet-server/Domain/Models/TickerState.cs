using TradingScanner.Domain.Enums;

namespace TradingScanner.Domain.Models;

public sealed class TickerState
{
    public required string Symbol { get; init; }
    public decimal Price { get; set; }
    public decimal PreviousClose { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal GapPercent { get; set; }
    public long Volume { get; set; }
    public long PremarketVolume { get; set; }
    public decimal RelativeVolume { get; set; }
    public long? FloatShares { get; set; }
    public decimal Vwap { get; set; }
    public decimal PremarketHigh { get; set; }
    public decimal PremarketLow { get; set; }
    public decimal HighOfDay { get; set; }
    public decimal LowOfDay { get; set; }
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Spread { get; set; }
    public decimal SpreadPercent { get; set; }
    public decimal OneMinuteChangePercent { get; set; }
    public decimal ThreeMinuteChangePercent { get; set; }
    public decimal FiveMinuteChangePercent { get; set; }
    public long OneMinuteVolume { get; set; }
    public long PreviousOneMinuteVolume { get; set; }
    public decimal VolumeAcceleration { get; set; }
    public bool HasCatalyst { get; set; }
    public CatalystType CatalystType { get; set; } = CatalystType.None;
    public int CatalystQuality { get; set; }
    public string? CatalystHeadline { get; set; }
    public SetupState CurrentSetup { get; set; } = SetupState.Waiting;
    public MomentumState MomentumState { get; set; } = MomentumState.Dormant;
    public ScoreResult APlusScore { get; set; } = ScoreResult.Empty;
    public DateTimeOffset LastUpdated { get; set; }
}

public sealed record ScoreComponents(decimal Catalyst, decimal RelativeVolume, decimal GapMomentum,
    decimal Float, decimal PremarketVolume, decimal VolumeAcceleration, decimal Vwap,
    decimal Setup, decimal ResistanceRoom, decimal Liquidity);

public sealed record ScoreResult(decimal TotalScore, ScoreGrade Grade, ScoreComponents Components)
{
    public static readonly ScoreResult Empty = new(0, ScoreGrade.Ignore, new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
}

public sealed record MarketTrade(string Symbol, decimal Price, long Size, DateTimeOffset Timestamp);
public sealed record MarketQuote(string Symbol, decimal Bid, decimal Ask, DateTimeOffset Timestamp);
public sealed record MinuteBar(string Symbol, decimal Open, decimal High, decimal Low, decimal Close, long Volume, DateTimeOffset Timestamp);

/// <summary>
/// The versioned envelope published to scanner clients. Keeping transport metadata in
/// the domain contract lets clients reject stale snapshots without relying on arrival order.
/// </summary>
public sealed record ScannerSnapshot(
    long Sequence,
    DateTimeOffset GeneratedAt,
    MarketSession Session,
    decimal MarketHeat,
    IReadOnlyList<TickerState> Tickers);

/// <summary>A transient scanner alert. Persistence concerns remain in AlertHistory.</summary>
public sealed record ScannerAlert(
    Guid Id,
    DateTimeOffset Timestamp,
    string Symbol,
    decimal Price,
    decimal Score,
    ScoreGrade Grade,
    AlertType Type,
    SetupState Setup,
    MomentumState Momentum,
    string Message);
