using Microsoft.Extensions.Options;
using TradingScanner.Application.Configuration;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Enums;
using TradingScanner.Domain.Models;

namespace TradingScanner.Application.Services;

public sealed class SetupDetectionEngine(IEnumerable<ISetupDetector> detectors) : ISetupDetectionEngine
{
    private readonly ISetupDetector[] _detectors = detectors.ToArray();

    public SetupState Evaluate(TickerState state, DateTimeOffset now)
    {
        foreach (var detector in _detectors)
        {
            var setup = detector.Detect(state, now);
            if (setup != SetupState.Waiting) return setup;
        }
        return SetupState.Waiting;
    }
}

public sealed class OrbSetupDetector(
    IMarketSessionService sessions,
    IOptions<ScannerSettings> options) : ISetupDetector
{
    public SetupState Detect(TickerState state, DateTimeOffset now) =>
        sessions.GetSession(now) == MarketSession.Regular
        && SetupRules.CrossedAbove(state, state.OpeningRangeHigh, options.Value.Setups)
            ? SetupState.FifteenMinuteOrb : SetupState.Waiting;
}

public sealed class VwapSetupDetector(IOptions<ScannerSettings> options) : ISetupDetector
{
    public SetupState Detect(TickerState state, DateTimeOffset now)
    {
        if (state.Vwap <= 0 || state.PreviousPrice <= 0) return SetupState.Waiting;
        var tolerance = options.Value.Setups.VwapConfirmationPercent / 100;
        if (state.PreviousPrice < state.PreviousVwap && state.Price >= state.Vwap * (1 + tolerance))
            return SetupState.VwapReclaim;
        if (state.PreviousPrice >= state.PreviousVwap && state.Price >= state.Vwap
            && state.Price <= state.Vwap * (1 + tolerance) && state.OneMinuteChangePercent >= 0)
            return SetupState.VwapHold;
        return SetupState.Waiting;
    }
}

public sealed class PullbackSetupDetector : ISetupDetector
{
    public SetupState Detect(TickerState state, DateTimeOffset now) =>
        state.Vwap > 0 && state.Price >= state.Vwap && state.OneMinuteChangePercent < 0
        && state.ThreeMinuteChangePercent > 0 && state.PreviousOneMinuteVolume > 0
        && state.OneMinuteVolume < state.PreviousOneMinuteVolume
            ? SetupState.FirstPullback : SetupState.Waiting;
}

public sealed class PremarketHighSetupDetector(IOptions<ScannerSettings> options) : ISetupDetector
{
    public SetupState Detect(TickerState state, DateTimeOffset now) =>
        SetupRules.CrossedAbove(state, state.PremarketHigh, options.Value.Setups)
            ? SetupState.PremarketHighBreak : SetupState.Waiting;
}

public sealed class HighOfDaySetupDetector(IOptions<ScannerSettings> options) : ISetupDetector
{
    public SetupState Detect(TickerState state, DateTimeOffset now) =>
        SetupRules.CrossedAbove(state, state.PreviousHighOfDay, options.Value.Setups)
            ? SetupState.HighOfDayBreak : SetupState.Waiting;
}

internal static class SetupRules
{
    public static bool CrossedAbove(TickerState state, decimal level, SetupThresholds thresholds) =>
        level > 0 && state.PreviousPrice > 0 && state.PreviousPrice <= level && state.Price > level
        && state.RelativeVolume >= thresholds.MinimumBreakoutRelativeVolume;
}
