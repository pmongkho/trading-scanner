using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingScanner._Data;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Models;

namespace TradingScanner._Controllers;

[ApiController]
[Route("api/scanner")]
public sealed class ScannerController(
    ITickerStateManager states,
    IFilteredViewService filtered,
    IMarketHeatService heat,
    IMarketSessionService sessions,
    AppDbContext db,
    TimeProvider clock) : ControllerBase
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
}
