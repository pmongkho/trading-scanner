using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingScanner._Data;
using TradingScanner.Application.Configuration;
using TradingScanner.Application.Interfaces;
using TradingScanner.Persistence.Entities;

namespace TradingScanner.Infrastructure.MarketData;

public sealed class AlpacaNewsService(
    IHttpClientFactory clients,
    IServiceScopeFactory scopes,
    ITickerStateManager states,
    ICatalystClassifier classifier,
    IOptions<ScannerSettings> options,
    TimeProvider clock,
    ILogger<AlpacaNewsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.News;
        var key = Environment.GetEnvironmentVariable("ALPACA_API_KEY");
        var secret = Environment.GetEnvironmentVariable("ALPACA_API_SECRET");
        if (!settings.Enabled || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
        {
            logger.LogInformation("Alpaca news ingestion disabled or credentials unavailable");
            return;
        }

        var client = clients.CreateClient(nameof(AlpacaNewsService));
        client.DefaultRequestHeaders.Add("APCA-API-KEY-ID", key);
        client.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", secret);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.PollSeconds), clock);
        do
        {
            try { await PollAsync(client, settings, stoppingToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            { logger.LogWarning(exception, "Alpaca news poll failed; the market stream remains available"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollAsync(HttpClient client, NewsSettings settings, CancellationToken token)
    {
        var since = clock.GetUtcNow().AddMinutes(-settings.LookbackMinutes).ToString("O");
        var uri = $"{settings.AlpacaUrl}?start={Uri.EscapeDataString(since)}&sort=desc&limit=50";
        var payload = await client.GetFromJsonAsync<AlpacaNewsResponse>(uri, token);
        if (payload?.News is not { Count: > 0 }) return;

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = payload.News.Select(x => x.Id.ToString()).ToArray();
        var existing = await db.NewsArticles.Where(x => ids.Contains(x.ProviderId))
            .Select(x => x.ProviderId).ToHashSetAsync(token);
        foreach (var item in payload.News.Where(x => !existing.Contains(x.Id.ToString())))
        {
            var classification = classifier.Classify(item.Headline, item.Summary);
            db.NewsArticles.Add(new NewsArticle
            {
                ProviderId = item.Id.ToString(), Headline = item.Headline, Summary = item.Summary,
                Source = item.Source, Symbols = string.Join(',', item.Symbols),
                CatalystType = classification.Type, CatalystQuality = classification.Quality,
                PublishedAt = item.CreatedAt
            });
            foreach (var symbol in item.Symbols) states.ApplyCatalyst(symbol, classification, item.Headline);
        }
        await db.SaveChangesAsync(token);
    }

    private sealed record AlpacaNewsResponse([property: JsonPropertyName("news")] List<AlpacaArticle> News);
    private sealed record AlpacaArticle(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("headline")] string Headline,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("source")] string? Source,
        [property: JsonPropertyName("symbols")] string[] Symbols,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);
}
