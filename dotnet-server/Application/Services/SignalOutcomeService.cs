using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingScanner._Data;
using TradingScanner.Application.Configuration;
using TradingScanner.Application.Interfaces;
using TradingScanner.Persistence.Entities;

namespace TradingScanner.Application.Services;

/// <summary>Completes durable signal outcomes from the latest in-memory market price.</summary>
public sealed class SignalOutcomeService(
    IServiceScopeFactory scopes,
    ITickerStateManager states,
    IOptions<ScannerSettings> options,
    TimeProvider clock,
    ILogger<SignalOutcomeService> logger) : BackgroundService
{
    internal static readonly ActivitySource ActivitySource = new("TradingScanner.Outcomes");
    private static readonly Meter Meter = new("TradingScanner.Outcomes");
    private static readonly Histogram<double> BatchDuration = Meter.CreateHistogram<double>("scanner.outcomes.batch.duration", "ms");
    private static readonly Counter<long> Measurements = Meter.CreateCounter<long>("scanner.outcomes.measurements");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.Outcomes.PollSeconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) await Measure(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception) { logger.LogCritical(exception, "Signal outcome service stopped unexpectedly"); }
    }

    private async Task Measure(CancellationToken token)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = ActivitySource.StartActivity("measure-signal-outcomes");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = clock.GetUtcNow();
        var signals = await db.ScannerSignals.Include(x => x.Performance)
            .Where(x => x.Timestamp <= now.AddMinutes(-1)
                && (x.Performance == null || x.Performance.Return30Minutes == null))
            .OrderByDescending(x => x.Timestamp).Take(options.Value.Outcomes.BatchSize).ToListAsync(token);
        long count = 0;
        foreach (var signal in signals)
        {
            if (!states.TryGet(signal.Symbol, out var state) || state is null || state.Price <= 0) continue;
            var performance = signal.Performance ??= new HistoricalPerformance { ScannerSignal = signal, UpdatedAt = now };
            var age = now - signal.Timestamp;
            var measuredReturn = Return(signal.Price, state.Price);
            if (age >= TimeSpan.FromMinutes(1) && performance.Return1Minute is null)
            { performance.Price1Minute = state.Price; performance.Return1Minute = measuredReturn; count++; }
            if (age >= TimeSpan.FromMinutes(5) && performance.Return5Minutes is null)
            { performance.Price5Minutes = state.Price; performance.Return5Minutes = measuredReturn; count++; }
            if (age >= TimeSpan.FromMinutes(15) && performance.Return15Minutes is null)
            { performance.Price15Minutes = state.Price; performance.Return15Minutes = measuredReturn; count++; }
            if (age >= TimeSpan.FromMinutes(30) && performance.Return30Minutes is null)
            { performance.Price30Minutes = state.Price; performance.Return30Minutes = measuredReturn; count++; }
            var currentReturn = Return(signal.Price, state.Price);
            performance.MaximumFavorableExcursion = Math.Max(performance.MaximumFavorableExcursion ?? currentReturn, currentReturn);
            performance.MaximumAdverseExcursion = Math.Min(performance.MaximumAdverseExcursion ?? currentReturn, currentReturn);
            performance.UpdatedAt = now;
        }
        if (count > 0) await db.SaveChangesAsync(token);
        Measurements.Add(count);
        BatchDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        activity?.SetTag("measurements", count);
    }

    private static decimal Return(decimal entry, decimal current) => entry == 0 ? 0 : Math.Round((current - entry) / entry * 100, 4);
}
