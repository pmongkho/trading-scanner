using TradingScanner.Domain.Enums;

namespace TradingScanner.Persistence.Entities;

public sealed class TickerMetadata
{
    public int Id { get; set; }
    public required string Symbol { get; set; }
    public string? CompanyName { get; set; }
    public long? FloatShares { get; set; }
    public DateOnly? IpoDate { get; set; }
    public bool IsFormerRunner { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class NewsArticle
{
    public long Id { get; set; }
    public required string ProviderId { get; set; }
    public required string Headline { get; set; }
    public string? Summary { get; set; }
    public string? Source { get; set; }
    public string Symbols { get; set; } = string.Empty;
    public CatalystType CatalystType { get; set; }
    public int CatalystQuality { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}

public sealed class ScannerSignal
{
    public long Id { get; set; }
    public required string Symbol { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public decimal Price { get; set; }
    public decimal Score { get; set; }
    public ScoreGrade Grade { get; set; }
    public SetupState Setup { get; set; }
    public MomentumState MomentumState { get; set; }
    public CatalystType Catalyst { get; set; }
    public int CatalystQuality { get; set; }
    public long? FloatShares { get; set; }
    public decimal RelativeVolume { get; set; }
    public decimal GapPercent { get; set; }
    public long Volume { get; set; }
    public long PremarketVolume { get; set; }
    public decimal Vwap { get; set; }
    public decimal DistanceFromVwapPercent { get; set; }
    public decimal SpreadPercent { get; set; }
    public MarketSession MarketSession { get; set; }
    public HistoricalPerformance? Performance { get; set; }
}

public sealed class AlertHistory
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public required string Symbol { get; set; }
    public required string DeduplicationKey { get; set; }
    public decimal Price { get; set; }
    public decimal Score { get; set; }
    public ScoreGrade Grade { get; set; }
    public AlertType AlertType { get; set; }
    public SetupState Setup { get; set; }
    public MomentumState MomentumState { get; set; }
    public required string Message { get; set; }
}

public sealed class HistoricalPerformance
{
    public long Id { get; set; }
    public long ScannerSignalId { get; set; }
    public ScannerSignal ScannerSignal { get; set; } = null!;
    public decimal? Price1Minute { get; set; }
    public decimal? Price5Minutes { get; set; }
    public decimal? Price15Minutes { get; set; }
    public decimal? Price30Minutes { get; set; }
    public decimal? Return1Minute { get; set; }
    public decimal? Return5Minutes { get; set; }
    public decimal? Return15Minutes { get; set; }
    public decimal? Return30Minutes { get; set; }
    public decimal? MaximumFavorableExcursion { get; set; }
    public decimal? MaximumAdverseExcursion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ScannerConfiguration
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string JsonValue { get; set; }
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
