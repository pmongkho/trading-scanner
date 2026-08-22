using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingScanner._Data;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Models;
using Microsoft.Extensions.Options;
using TradingScanner.Application.Configuration;
using TradingScanner.Persistence.Entities;

namespace TradingScanner._Controllers;

[ApiController]
[Route("api/scanner")]
public sealed class ScannerController(
    ITickerStateManager states,
    IFilteredViewService filtered,
    IMarketHeatService heat,
    IMarketSessionService sessions,
    AppDbContext db,
    TimeProvider clock,
    IOptions<ScannerSettings> options) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardHydration>> Dashboard(CancellationToken token)
    {
        var now = clock.GetUtcNow();
        var all = states.Snapshot();
        var snapshot = new ScannerSnapshot(0, now, sessions.GetSession(now), heat.Calculate(all), filtered.Filter(all));
        var articles = await db.NewsArticles.AsNoTracking().OrderByDescending(x => x.PublishedAt)
            .Take(50).ToListAsync(token);
        var news = articles.Select(x => new NewsItem(x.Id, x.Headline, x.Source,
            x.Symbols.Split(',', StringSplitOptions.RemoveEmptyEntries), x.CatalystType,
            x.CatalystQuality, x.PublishedAt)).ToList();
        return new DashboardHydration(snapshot, news);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<SignalOutcome>>> History(
        [FromQuery] string? symbol, [FromQuery] int days = 30, [FromQuery] int limit = 200,
        CancellationToken token = default)
    {
        days = Math.Clamp(days, 1, options.Value.Outcomes.AnalyticsMaximumDays);
        limit = Math.Clamp(limit, 1, 1000);
        var from = clock.GetUtcNow().AddDays(-days);
        var query = db.ScannerSignals.AsNoTracking().Include(x => x.Performance).Where(x => x.Timestamp >= from);
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var normalized = symbol.Trim().ToUpperInvariant();
            query = query.Where(x => x.Symbol == normalized);
        }
        var signals = await query.OrderByDescending(x => x.Timestamp).Take(limit).ToListAsync(token);
        return signals.Select(x => new SignalOutcome(x.Id, x.Symbol, x.Timestamp, x.Price, x.Score, x.Grade,
            x.Setup, x.Performance?.Return1Minute, x.Performance?.Return5Minutes,
            x.Performance?.Return15Minutes, x.Performance?.Return30Minutes,
            x.Performance?.MaximumFavorableExcursion, x.Performance?.MaximumAdverseExcursion)).ToList();
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<HistoricalAnalytics>> Analytics([FromQuery] int days = 30,
        CancellationToken token = default)
    {
        days = Math.Clamp(days, 1, options.Value.Outcomes.AnalyticsMaximumDays);
        var to = clock.GetUtcNow();
        var from = to.AddDays(-days);
        var signals = await db.ScannerSignals.AsNoTracking().Include(x => x.Performance)
            .Where(x => x.Timestamp >= from && x.Timestamp <= to).ToListAsync(token);
        return new HistoricalAnalytics(from, to, signals.Count, signals.Count(x => x.Performance?.Return15Minutes != null),
            Buckets(signals.GroupBy(x => x.Grade), x => x.Key.ToString()),
            Buckets(signals.GroupBy(x => x.Setup), x => x.Key.ToString()));
    }

    [HttpGet("analytics/tuning-recommendation")]
    public async Task<ActionResult<ModelTuningRecommendation>> TuningRecommendation([FromQuery] int days = 30,
        CancellationToken token = default)
    {
        days = Math.Clamp(days, 1, options.Value.Outcomes.AnalyticsMaximumDays);
        var from = clock.GetUtcNow().AddDays(-days);
        var winners = await db.ScannerSignals.AsNoTracking().Include(x => x.Performance)
            .Where(x => x.Timestamp >= from && x.Performance != null && x.Performance.Return15Minutes > 0)
            .OrderBy(x => x.Score).ToListAsync(token);
        if (winners.Count < 10)
            return new ModelTuningRecommendation(winners.Count, null, null,
                "At least 10 positive 15-minute outcomes are required before recommending model thresholds.");
        return new ModelTuningRecommendation(winners.Count, Median(winners.Select(x => x.Score)),
            Median(winners.Select(x => x.RelativeVolume)),
            "Medians of signals with positive measured 15-minute returns; review before changing configuration.");
    }

    private static IReadOnlyList<OutcomeBucket> Buckets<TKey>(
        IEnumerable<IGrouping<TKey, ScannerSignal>> groups, Func<IGrouping<TKey, ScannerSignal>, string> name) =>
        groups.Select(group =>
        {
            var measured = group.Where(x => x.Performance?.Return15Minutes != null).ToList();
            return new OutcomeBucket(name(group), group.Count(), Average(measured.Select(x => x.Performance!.Return15Minutes)),
                measured.Count == 0 ? null : Math.Round(measured.Count(x => x.Performance!.Return15Minutes > 0) * 100m / measured.Count, 2),
                Average(measured.Select(x => x.Performance!.MaximumFavorableExcursion)),
                Average(measured.Select(x => x.Performance!.MaximumAdverseExcursion)));
        }).OrderByDescending(x => x.Signals).ToList();

    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var measured = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return measured.Count == 0 ? null : Math.Round(measured.Average(), 4);
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2 : ordered[middle];
    }
}
