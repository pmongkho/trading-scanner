using TradingScanner.Domain.Enums;
using TradingScanner.Domain.Models;

namespace TradingScanner.Application.Interfaces;

public interface IMarketDataProvider
{
    event Func<MarketTrade, ValueTask>? TradeReceived;
    event Func<MarketQuote, ValueTask>? QuoteReceived;
    event Func<MinuteBar, ValueTask>? MinuteBarReceived;
    Task RunAsync(CancellationToken cancellationToken);
}

public interface ITickerStateManager
{
    IReadOnlyCollection<TickerState> Snapshot();
    bool TryGet(string symbol, out TickerState? state);
    void Apply(MarketTrade trade);
    void Apply(MarketQuote quote);
    void Apply(MinuteBar bar);
}

public interface IIndicatorEngine { void Update(TickerState state, MinuteBar bar); }
public interface IAPlusScoringEngine { ScoreResult Score(TickerState state); }
public interface IMomentumEngine { MomentumState Evaluate(TickerState state); }
public interface ISetupDetectionEngine { SetupState Evaluate(TickerState state, DateTimeOffset now); }
public interface IMarketSessionService { MarketSession GetSession(DateTimeOffset instant); }

/// <summary>
/// Methods the server can invoke on a connected SignalR workstation. Method names are
/// intentionally part of the application contract so publishers do not use magic strings.
/// </summary>
public interface IMarketClient
{
    Task ScannerSnapshotReceived(ScannerSnapshot snapshot);
    Task ScannerAlertReceived(ScannerAlert alert);
}
