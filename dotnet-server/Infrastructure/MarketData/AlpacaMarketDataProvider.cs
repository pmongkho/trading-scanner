using System.Net.WebSockets;
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
