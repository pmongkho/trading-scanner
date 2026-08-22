using Microsoft.Extensions.Options;
using TradingScanner.Application.Configuration;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Enums;
using TradingScanner.Domain.Models;

namespace TradingScanner.Application.Services;

public sealed class MomentumEngine(IOptions<ScannerSettings> options) : IMomentumEngine
{
    public MomentumState Evaluate(TickerState state)
    {
        var thresholds = options.Value.Momentum;
        if (state.Vwap > 0 && (state.Price - state.Vwap) / state.Vwap * 100 >= thresholds.ExtendedFromVwapPercent)
            return MomentumState.Extended;
        if (state.OneMinuteChangePercent < 0 && state.ThreeMinuteChangePercent < 0) return MomentumState.Fading;

        var strength = Math.Max(0, state.OneMinuteChangePercent)
            + Math.Max(0, state.ThreeMinuteChangePercent)
            + Math.Min(3, state.RelativeVolume)
            + Math.Min(2, state.VolumeAcceleration);
        if (strength >= thresholds.StrongScore) return MomentumState.Strong;
        if (strength >= thresholds.AcceleratingScore) return MomentumState.Accelerating;
        if (strength >= thresholds.BuildingScore) return MomentumState.Building;
        return MomentumState.Dormant;
    }
}

public sealed class APlusScoringEngine(IOptions<ScannerSettings> options) : IAPlusScoringEngine
{
    public ScoreResult Score(TickerState state)
    {
        var settings = options.Value;
        var w = settings.ScoreWeights;
        decimal Points(decimal quality, int weight) => Math.Clamp(quality, 0, 1) * weight;
        var components = new ScoreComponents(
            Points(state.HasCatalyst ? Math.Max(.5m, state.CatalystQuality / 100m) : 0, w.Catalyst),
            Points(state.RelativeVolume / Math.Max(1, settings.MinimumRelativeVolume * 2), w.RelativeVolume),
            Points(Math.Max(state.GapPercent / 20, state.ThreeMinuteChangePercent / 5), w.GapMomentum),
            Points(state.FloatShares is > 0 ? 1 - Math.Min(1, state.FloatShares.Value / (decimal)settings.MaximumFloat) : 0, w.Float),
            Points(state.PremarketVolume / (decimal)Math.Max(1, settings.MinimumPremarketVolume * 2), w.PremarketVolume),
            Points(state.VolumeAcceleration / 3, w.VolumeAcceleration),
            Points(state.Vwap > 0 && state.Price >= state.Vwap ? 1 : 0, w.Vwap),
            Points(state.CurrentSetup is not SetupState.Waiting and not SetupState.Failed ? 1 : 0, w.Setup),
            Points(state.HighOfDay > state.Price ? (state.HighOfDay - state.Price) / state.Price * 10 : 0, w.ResistanceRoom),
            Points(state.SpreadPercent == 0 ? 0 : 1 - state.SpreadPercent / 2, w.Liquidity));
        var total = Math.Round(components.Catalyst + components.RelativeVolume + components.GapMomentum
            + components.Float + components.PremarketVolume + components.VolumeAcceleration + components.Vwap
            + components.Setup + components.ResistanceRoom + components.Liquidity, 2);
        var grade = total >= 85 ? ScoreGrade.APlus : total >= 70 ? ScoreGrade.A : total >= 55 ? ScoreGrade.B : ScoreGrade.Ignore;
        return new ScoreResult(total, grade, components);
    }
}

public sealed class FilteredViewService(IOptions<ScannerSettings> options) : IFilteredViewService
{
    public IReadOnlyList<TickerState> Filter(IEnumerable<TickerState> states)
    {
        var s = options.Value;
        return states.Where(x => x.Price >= s.MinimumPrice && x.Price <= s.MaximumPrice
                && x.GapPercent >= s.MinimumGapPercent && x.Volume >= s.MinimumVolume
                && x.RelativeVolume >= s.MinimumRelativeVolume
                && (!x.FloatShares.HasValue || x.FloatShares <= s.MaximumFloat))
            .OrderByDescending(x => x.APlusScore.TotalScore).ThenByDescending(x => x.ChangePercent).ToArray();
    }
}

public sealed class MarketHeatService : IMarketHeatService
{
    public decimal Calculate(IReadOnlyCollection<TickerState> states)
    {
        if (states.Count == 0) return 0;
        var advancing = states.Count(x => x.ChangePercent > 0) / (decimal)states.Count;
        var momentum = states.Average(x => Math.Clamp(x.OneMinuteChangePercent, -5, 5)) / 10 + .5m;
        var rvol = states.Average(x => Math.Min(x.RelativeVolume, 5)) / 5;
        return Math.Round(Math.Clamp((advancing * .4m + momentum * .3m + rvol * .3m) * 100, 0, 100), 1);
    }
}
