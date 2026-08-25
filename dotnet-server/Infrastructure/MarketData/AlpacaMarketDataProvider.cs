using System.Net.WebSockets;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TradingScanner.Application.Configuration;
using TradingScanner.Application.Interfaces;
using TradingScanner.Domain.Models;

namespace TradingScanner.Infrastructure.MarketData;

/// <summary>Owns a single Alpaca stock websocket and translates its wire messages into domain events.</summary>
public sealed class AlpacaMarketDataProvider(
    IOptions<ScannerSettings> settings,
    IHttpClientFactory clientFactory,
    ILogger<AlpacaMarketDataProvider> logger) : IMarketDataProvider
{
    private readonly MarketStreamSettings _settings = settings.Value.MarketStream;
    private readonly AlpacaSettings _alpaca = settings.Value.Alpaca;

    public event Func<MarketTrade, ValueTask>? TradeReceived;
    public event Func<MarketQuote, ValueTask>? QuoteReceived;
    public event Func<MinuteBar, ValueTask>? MinuteBarReceived;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var key = Environment.GetEnvironmentVariable("ALPACA_API_KEY") ?? _alpaca.ApiKey;
        var secret = Environment.GetEnvironmentVariable("ALPACA_API_SECRET") ?? _alpaca.ApiSecret;
        var feed = Environment.GetEnvironmentVariable("ALPACA_DATA_FEED") ?? _alpaca.DataFeed;
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Alpaca API key and secret are not configured.");
        if (string.IsNullOrWhiteSpace(feed))
            throw new InvalidOperationException("An Alpaca data feed (for example, iex or sip) is not configured.");
        if (_settings.Symbols.Length == 0) throw new InvalidOperationException("At least one market-stream symbol is required.");

        await BootstrapSnapshotsAsync(key, secret, feed, cancellationToken);

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(BuildStreamUri(_settings.AlpacaUrl, feed), cancellationToken);
        await SendAsync(socket, new { action = "auth", key, secret }, cancellationToken);
        await SendAsync(socket, new
        {
            action = "subscribe",
            trades = _settings.Symbols,
            quotes = _settings.Symbols,
            bars = _settings.Symbols
        }, cancellationToken);
        logger.LogInformation("Connected to Alpaca {DataFeed} market data for {SymbolCount} symbols",
            feed, _settings.Symbols.Length);

        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) break;
            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;
            if (result.MessageType == WebSocketMessageType.Text)
                await ProcessMessageAsync(message.GetBuffer().AsMemory(0, checked((int)message.Length)));
            message.SetLength(0);
        }
    }

    /// <summary>
    /// Seeds the in-memory projection from Alpaca's latest snapshots before listening for new
    /// websocket messages. A stream is silent while the market is closed, so without this step a
    /// fresh server would show an empty dashboard all night and throughout the weekend.
    /// </summary>
    private async Task BootstrapSnapshotsAsync(string key, string secret, string feed, CancellationToken cancellationToken)
    {
        try
        {
            var symbols = string.Join(',', _settings.Symbols.Distinct(StringComparer.OrdinalIgnoreCase));
            var separator = _alpaca.SnapshotsUrl.Contains("?", StringComparison.Ordinal) ? '&' : '?';
            var url = $"{_alpaca.SnapshotsUrl}{separator}symbols={Uri.EscapeDataString(symbols)}&feed={Uri.EscapeDataString(feed)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("APCA-API-KEY-ID", key);
            request.Headers.Add("APCA-API-SECRET-KEY", secret);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await clientFactory.CreateClient().SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var snapshots = document.RootElement.TryGetProperty("snapshots", out var wrapped)
                ? wrapped
                : document.RootElement;
            if (snapshots.ValueKind != JsonValueKind.Object) return;

            var count = 0;
            foreach (var property in snapshots.EnumerateObject())
            {
                if (await PublishSnapshotAsync(property.Name, property.Value)) count++;
            }
            logger.LogInformation("Seeded {SnapshotCount} Alpaca stock snapshots before opening the live stream", count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            // Snapshot hydration improves closed-market startup but must not prevent the live stream
            // from connecting if Alpaca temporarily rejects or cannot serve the REST request.
            logger.LogWarning(exception, "Could not seed Alpaca snapshots; continuing with the live stream");
        }
    }

    private async ValueTask<bool> PublishSnapshotAsync(string symbol, JsonElement snapshot)
    {
        var published = false;
        if (snapshot.TryGetProperty("minuteBar", out var minuteBar) && TryReadBar(symbol, minuteBar, out var bar))
        {
            if (MinuteBarReceived is not null) await MinuteBarReceived(bar);
            published = true;
        }
        else if (snapshot.TryGetProperty("dailyBar", out var dailyBar) && TryReadBar(symbol, dailyBar, out bar))
        {
            if (MinuteBarReceived is not null) await MinuteBarReceived(bar);
            published = true;
        }
        if (snapshot.TryGetProperty("latestTrade", out var latestTrade)
            && TryReadDecimal(latestTrade, "p", out var price)
            && TryReadLong(latestTrade, "s", out var size)
            && TryReadTimestamp(latestTrade, out var tradeAt))
        {
            if (TradeReceived is not null) await TradeReceived(new(symbol, price, Math.Max(1, size), tradeAt));
            published = true;
        }
        if (snapshot.TryGetProperty("latestQuote", out var latestQuote)
            && TryReadDecimal(latestQuote, "bp", out var bid)
            && TryReadDecimal(latestQuote, "ap", out var ask)
            && TryReadTimestamp(latestQuote, out var quoteAt))
        {
            if (QuoteReceived is not null) await QuoteReceived(new(symbol, bid, ask, quoteAt));
            published = true;
        }
        return published;
    }

    private static bool TryReadBar(string symbol, JsonElement value, out MinuteBar bar)
    {
        bar = default!;
        if (!TryReadDecimal(value, "o", out var open) || !TryReadDecimal(value, "h", out var high)
            || !TryReadDecimal(value, "l", out var low) || !TryReadDecimal(value, "c", out var close)
            || !TryReadLong(value, "v", out var volume) || !TryReadTimestamp(value, out var timestamp)) return false;
        bar = new(symbol, open, high, low, close, volume, timestamp);
        return true;
    }

    private static bool TryReadDecimal(JsonElement value, string name, out decimal result)
    {
        result = 0;
        return value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty(name, out var property) && property.TryGetDecimal(out result);
    }

    private static bool TryReadLong(JsonElement value, string name, out long result)
    {
        result = 0;
        return value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty(name, out var property) && property.TryGetInt64(out result);
    }

    private static bool TryReadTimestamp(JsonElement value, out DateTimeOffset result)
    {
        result = default;
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty("t", out var property)
            && property.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(property.GetString(), out result);
    }

    internal async ValueTask ProcessMessageAsync(ReadOnlyMemory<byte> payload)
    {
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("t", out var timestampElement)
                || !DateTimeOffset.TryParse(timestampElement.GetString(), out var timestamp)
                || !item.TryGetProperty("S", out var symbolElement)) continue;
            var symbol = symbolElement.GetString();
            if (string.IsNullOrWhiteSpace(symbol) || !item.TryGetProperty("T", out var typeElement)) continue;
            switch (typeElement.GetString())
            {
                case "t" when TradeReceived is not null:
                    await TradeReceived(new(symbol, item.GetProperty("p").GetDecimal(), item.GetProperty("s").GetInt64(), timestamp));
                    break;
                case "q" when QuoteReceived is not null:
                    await QuoteReceived(new(symbol, item.GetProperty("bp").GetDecimal(), item.GetProperty("ap").GetDecimal(), timestamp));
                    break;
                case "b" when MinuteBarReceived is not null:
                    await MinuteBarReceived(new(symbol, item.GetProperty("o").GetDecimal(), item.GetProperty("h").GetDecimal(),
                        item.GetProperty("l").GetDecimal(), item.GetProperty("c").GetDecimal(), item.GetProperty("v").GetInt64(), timestamp));
                    break;
            }
        }
    }

    private static Task SendAsync(ClientWebSocket socket, object value, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        return socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
    }

    internal static Uri BuildStreamUri(string configuredUrl, string feed)
    {
        var normalizedFeed = feed.Trim().ToLowerInvariant();
        if (normalizedFeed.Length == 0 || normalizedFeed.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new InvalidOperationException("ALPACA_DATA_FEED contains invalid characters.");

        var configured = new Uri(configuredUrl, UriKind.Absolute);
        if (configured.Scheme is not "wss" and not "ws")
            throw new InvalidOperationException("The Alpaca market stream URL must use ws or wss.");

        var builder = new UriBuilder(configured);
        var versionRoot = builder.Path[..(builder.Path.LastIndexOf('/') + 1)];
        builder.Path = $"{versionRoot}{normalizedFeed}";
        return builder.Uri;
    }
}
