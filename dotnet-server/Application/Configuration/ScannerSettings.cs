namespace TradingScanner.Application.Configuration;

public sealed class ScannerSettings
{
    public const string SectionName = "Scanner";
    public decimal MinimumPrice { get; set; } = 1;
    public decimal MaximumPrice { get; set; } = 20;
    public decimal MinimumGapPercent { get; set; } = 10;
    public long MinimumVolume { get; set; } = 500_000;
    public decimal MinimumRelativeVolume { get; set; } = 2;
    public long MaximumFloat { get; set; } = 20_000_000;
    public long PreferredFloat { get; set; } = 10_000_000;
    public long MinimumPremarketVolume { get; set; } = 1_000_000;
    public int FrontendUpdateMilliseconds { get; set; } = 250;
    public int AlertCooldownSeconds { get; set; } = 300;
    public string MarketDataFeed { get; set; } = "iex";
    public MarketStreamSettings MarketStream { get; set; } = new();
    public ScoreWeights ScoreWeights { get; set; } = new();
    public MomentumThresholds Momentum { get; set; } = new();
    public SetupThresholds Setups { get; set; } = new();
}

public sealed class MarketStreamSettings
{
    public string Provider { get; set; } = "Synthetic";
    public string AlpacaUrl { get; set; } = "wss://stream.data.alpaca.markets/v2/iex";
    public string[] Symbols { get; set; } = ["AAPL", "MSFT"];
    public int SyntheticIntervalMilliseconds { get; set; } = 250;
}

public sealed class ScoreWeights
{
    public int Catalyst { get; set; } = 20;
    public int RelativeVolume { get; set; } = 15;
    public int GapMomentum { get; set; } = 10;
    public int Float { get; set; } = 10;
    public int PremarketVolume { get; set; } = 10;
    public int VolumeAcceleration { get; set; } = 10;
    public int Vwap { get; set; } = 10;
    public int Setup { get; set; } = 5;
    public int ResistanceRoom { get; set; } = 5;
    public int Liquidity { get; set; } = 5;

    public int Total => Catalyst + RelativeVolume + GapMomentum + Float + PremarketVolume
        + VolumeAcceleration + Vwap + Setup + ResistanceRoom + Liquidity;
}

public sealed class MomentumThresholds
{
    public decimal BuildingScore { get; set; } = 3;
    public decimal AcceleratingScore { get; set; } = 5;
    public decimal StrongScore { get; set; } = 7;
    public decimal ExtendedFromVwapPercent { get; set; } = 12;
}

public sealed class SetupThresholds
{
    public decimal MinimumBreakoutRelativeVolume { get; set; } = 2;
    public decimal VwapConfirmationPercent { get; set; } = 0.25m;
    public int ConfirmationSeconds { get; set; } = 15;
}
