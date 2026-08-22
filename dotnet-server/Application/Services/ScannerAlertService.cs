using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using TradingScanner._Data;
using TradingScanner.Application.Configuration;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Enums;
using TradingScanner.Domain.Models;
using TradingScanner.Hubs;
using TradingScanner.Persistence.Entities;

namespace TradingScanner.Application.Services;

/// <summary>Turns setup transitions into durable, idempotent workstation alerts.</summary>
public sealed class ScannerAlertService(
    ITickerStateManager states,
    IMarketSessionService sessions,
    IHubContext<MarketHub, IMarketClient> hub,
    IServiceScopeFactory scopes,
    IOptions<ScannerSettings> options,
    TimeProvider clock,
    ILogger<ScannerAlertService> logger) : BackgroundService
{
    private readonly Dictionary<string, SetupState> _active = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastAlert = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(options.Value.FrontendUpdateMilliseconds), clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await PublishTransitions(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception) { logger.LogCritical(exception, "Scanner alert service stopped unexpectedly"); }
    }

    private async Task PublishTransitions(CancellationToken cancellationToken)
    {
        foreach (var state in states.Snapshot())
        {
            _active.TryGetValue(state.Symbol, out var previous);
            if (state.CurrentSetup is SetupState.Waiting or SetupState.Failed or SetupState.Extended)
            {
                _active[state.Symbol] = state.CurrentSetup;
                continue;
            }
            if (state.CurrentSetup == previous || !IsMeaningful(state)) continue;

            var now = clock.GetUtcNow();
            var type = ToAlertType(state.CurrentSetup);
            var cooldownKey = $"{state.Symbol}:{type}";
            if (_lastAlert.TryGetValue(cooldownKey, out var last)
                && now - last < TimeSpan.FromSeconds(options.Value.AlertCooldownSeconds)) continue;
            var alert = new ScannerAlert(Guid.NewGuid(), now, state.Symbol, state.Price,
                state.APlusScore.TotalScore, state.APlusScore.Grade, type, state.CurrentSetup,
                state.MomentumState, Message(state.Symbol, state.CurrentSetup, state.Price));
            if (await Persist(alert, state, cancellationToken))
                await hub.Clients.All.ScannerAlertReceived(alert);
            _lastAlert[cooldownKey] = now;
            _active[state.Symbol] = state.CurrentSetup;
        }
    }

    private bool IsMeaningful(TickerState state) => state.Price > 0
        && state.APlusScore.Grade != ScoreGrade.Ignore
        && state.RelativeVolume >= options.Value.Setups.MinimumBreakoutRelativeVolume
        && sessions.GetSession(state.LastUpdated) is not MarketSession.Closed;

    private async Task<bool> Persist(ScannerAlert alert, TickerState state, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cooldown = options.Value.AlertCooldownSeconds;
        var bucket = cooldown > 0 ? alert.Timestamp.ToUnixTimeSeconds() / cooldown : alert.Timestamp.UtcTicks;
        var key = $"{alert.Symbol.ToUpperInvariant()}:{alert.Type}:{alert.Setup}:{bucket}";
        db.AlertHistory.Add(new AlertHistory
        {
            Id = alert.Id, Timestamp = alert.Timestamp, Symbol = alert.Symbol, DeduplicationKey = key,
            Price = alert.Price, Score = alert.Score, Grade = alert.Grade, AlertType = alert.Type,
            Setup = alert.Setup, MomentumState = alert.Momentum, Message = alert.Message
        });
        db.ScannerSignals.Add(new ScannerSignal
        {
            Symbol = state.Symbol, Timestamp = alert.Timestamp, Price = state.Price,
            Score = state.APlusScore.TotalScore, Grade = state.APlusScore.Grade, Setup = state.CurrentSetup,
            MomentumState = state.MomentumState, Catalyst = state.CatalystType,
            CatalystQuality = state.CatalystQuality, FloatShares = state.FloatShares,
            RelativeVolume = state.RelativeVolume, GapPercent = state.GapPercent, Volume = state.Volume,
            PremarketVolume = state.PremarketVolume, Vwap = state.Vwap,
            DistanceFromVwapPercent = state.Vwap == 0 ? 0 : (state.Price - state.Vwap) / state.Vwap * 100,
            SpreadPercent = state.SpreadPercent, MarketSession = sessions.GetSession(alert.Timestamp)
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            logger.LogDebug(exception, "Suppressed duplicate alert {DeduplicationKey}", key);
            return false;
        }
    }

    private static AlertType ToAlertType(SetupState setup) => setup switch
    {
        SetupState.FifteenMinuteOrb => AlertType.Orb15Breakout,
        SetupState.VwapReclaim => AlertType.VwapReclaim,
        SetupState.VwapHold => AlertType.VwapHold,
        SetupState.PremarketHighBreak => AlertType.PremarketHighBreak,
        SetupState.HighOfDayBreak => AlertType.HodBreak,
        SetupState.FirstPullback => AlertType.FirstPullback,
        _ => throw new ArgumentOutOfRangeException(nameof(setup), setup, null)
    };

    private static string Message(string symbol, SetupState setup, decimal price) =>
        $"{symbol} confirmed {setup} at {price:C2}";
}
