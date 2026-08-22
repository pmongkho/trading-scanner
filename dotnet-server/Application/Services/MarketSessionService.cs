using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Enums;

namespace TradingScanner.Application.Services;

public sealed class MarketSessionService : IMarketSessionService
{
    private readonly TimeZoneInfo _eastern = ResolveEasternTimeZone();

    public MarketSession GetSession(DateTimeOffset instant)
    {
        var local = TimeZoneInfo.ConvertTime(instant, _eastern);
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return MarketSession.Closed;
        var time = local.TimeOfDay;
        if (time >= TimeSpan.FromHours(4) && time < new TimeSpan(9, 30, 0)) return MarketSession.Premarket;
        if (time >= new TimeSpan(9, 30, 0) && time < new TimeSpan(9, 45, 0)) return MarketSession.OpeningRange;
        if (time >= new TimeSpan(9, 45, 0) && time < TimeSpan.FromHours(16)) return MarketSession.Regular;
        if (time >= TimeSpan.FromHours(16) && time < TimeSpan.FromHours(20)) return MarketSession.AfterHours;
        return MarketSession.Closed;
    }

    private static TimeZoneInfo ResolveEasternTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    }
}
