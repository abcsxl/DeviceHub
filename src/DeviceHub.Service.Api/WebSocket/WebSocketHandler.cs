using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DeviceHub.Devices.Contracts;
using Microsoft.Extensions.Localization;

namespace DeviceHub.Service.Api.WebSocket;

public sealed class WebSocketHandler : IDisposable
{
    private readonly ConcurrentDictionary<Guid, ConnectionEntry> _connections = new();
    private readonly SemaphoreSlim _sendSemaphore = new(5, 5);
    private readonly IStringLocalizer<Program> _localizer;
    private bool _disposed;

    public int ConnectionCount => _connections.Count;

    public WebSocketHandler(IStringLocalizer<Program> localizer)
    {
        _localizer = localizer;
    }

    internal IEnumerable<KeyValuePair<Guid, System.Net.WebSockets.WebSocket>> ActiveConnections
        => _connections
            .Where(kvp => kvp.Value.Socket.State == WebSocketState.Open)
            .Select(kvp => new KeyValuePair<Guid, System.Net.WebSockets.WebSocket>(kvp.Key, kvp.Value.Socket));

    internal IReadOnlySet<string>? GetSubscribedEvents(Guid id)
    {
        return _connections.TryGetValue(id, out var entry) ? entry.SubscribedEvents : null;
    }

    internal void RemoveConnection(Guid id)
    {
        _connections.TryRemove(id, out _);
    }

    internal bool TryUpdatePingTime(Guid id)
    {
        if (_connections.TryGetValue(id, out var entry) && entry.Socket.State == WebSocketState.Open)
        {
            var now = DateTime.UtcNow;
            if (now - entry.LastPong > TimeSpan.FromSeconds(35))
                return false;

            return true;
        }
        return false;
    }

    internal void MapRoutes(WebApplication app, string path = "/ws")
    {
        app.Map(path, async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(_localizer["ExpectedWebSocketRequest"]);
                return;
            }

            var ws = await context.WebSockets.AcceptWebSocketAsync();
            var connectionId = Guid.NewGuid();

            var subscribedEvents = ParseSubscribedEvents(context.Request.Query["events"]);
            _connections.TryAdd(connectionId, new ConnectionEntry(ws, DateTime.UtcNow, subscribedEvents));

            try
            {
                await HandleConnectionAsync(connectionId, ws, context.RequestServices);
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                _connections.TryRemove(connectionId, out _);
            }
        });
    }

    public async Task SendEventAsync(string target, string eventName, object data)
    {
        var acquired = await _sendSemaphore.WaitAsync(TimeSpan.FromSeconds(1));
        if (!acquired) return;

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                type = "event",
                target,
                @event = eventName,
                data,
                timestamp = DateTime.UtcNow
            });

            var bytes = Encoding.UTF8.GetBytes(payload);
            var segment = new ArraySegment<byte>(bytes);

            foreach (var (id, entry) in _connections)
            {
                if (entry.Socket.State != WebSocketState.Open)
                    continue;

                if (entry.SubscribedEvents != null && !entry.SubscribedEvents.Contains(eventName) && !entry.SubscribedEvents.Contains("*"))
                    continue;

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await entry.Socket.SendAsync(segment, WebSocketMessageType.Text, true, cts.Token);
                }
                catch
                {
                    _connections.TryRemove(id, out _);
                }
            }
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private async Task HandleConnectionAsync(Guid connectionId, System.Net.WebSockets.WebSocket ws, IServiceProvider services)
    {
        var buffer = new byte[1024 * 16];

        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await HandleTextMessageAsync(connectionId, ws, json, services);
            }
        }
    }

    private async Task HandleTextMessageAsync(Guid connectionId, System.Net.WebSockets.WebSocket ws, string json, IServiceProvider services)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "pong")
            {
                if (_connections.TryGetValue(connectionId, out var entry))
                    _connections.TryUpdate(connectionId, new ConnectionEntry(entry.Socket, DateTime.UtcNow, entry.SubscribedEvents), entry);
                return;
            }

            var requestId = root.TryGetProperty("requestId", out var rid)
                ? rid.GetString()
                : Guid.NewGuid().ToString();
            var target = root.TryGetProperty("target", out var t) ? t.GetString() : null;
            var action = root.TryGetProperty("action", out var a) ? a.GetString() : null;

            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(action))
            {
                await SendErrorAsync(ws, requestId!, "INVALID_PARAMETERS", _localizer["TargetAndActionRequired"]);
                return;
            }

            root.TryGetProperty("parameters", out var parameters);

            switch (target)
            {
                case "pcsc":
                    await HandlePcscAction(ws, services, parameters, requestId!, action);
                    break;
                default:
                    await SendErrorAsync(ws, requestId!, "INVALID_ACTION", string.Format(_localizer["UnknownTarget"], target));
                    break;
            }
        }
        catch (JsonException)
        {
            await SendErrorAsync(ws, null, "INVALID_PARAMETERS", _localizer["InvalidJson"]);
        }
    }

    private async Task HandlePcscAction(
        System.Net.WebSockets.WebSocket ws, IServiceProvider services, JsonElement parameters,
        string requestId, string action)
    {
        var pcsc = services.GetService<IPcscService>();
        if (pcsc == null)
        {
            await SendErrorAsync(ws, requestId, "DRIVER_NOT_FOUND", _localizer["PcscDriverNotRegistered"]);
            return;
        }

        object? data = null;
        switch (action)
        {
            case "list_readers":
                var readers = await pcsc.ListReadersAsync();
                data = new { readers };
                break;

            case "get_reader_status":
            {
                var readerName = GetParam(parameters, "readerName");
                if (string.IsNullOrEmpty(readerName))
                {
                    await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", _localizer["ReaderNameRequired"]);
                    return;
                }
                var info = await pcsc.GetReaderInfoAsync(readerName);
                data = info;
                break;
            }

            case "get_atr":
            {
                var readerName = GetParam(parameters, "readerName");
                if (string.IsNullOrEmpty(readerName))
                {
                    await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", _localizer["ReaderNameRequired"]);
                    return;
                }
                var atr = await pcsc.GetAtrAsync(readerName);
                if (atr == null)
                {
                    await SendErrorAsync(ws, requestId, "CARD_NOT_PRESENT", _localizer["CardNotPresent"]);
                    return;
                }
                data = new { atr };
                break;
            }

            case "transmit":
            {
                var readerName = GetParam(parameters, "readerName");
                var apdu = GetParam(parameters, "apdu");
                if (string.IsNullOrEmpty(readerName) || string.IsNullOrEmpty(apdu))
                {
                    await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", _localizer["ReaderNameAndApduRequired"]);
                    return;
                }
                var result = await pcsc.TransmitAsync(readerName, apdu);
                if (!result.Success)
                {
                    await SendErrorAsync(ws, requestId, "HARDWARE_ERROR", result.ErrorMessage ?? "Transmit failed");
                    return;
                }
                data = new { sw1 = result.Sw1, sw2 = result.Sw2, responseData = result.ResponseData, success = result.Success };
                break;
            }

            default:
                await SendErrorAsync(ws, requestId, "INVALID_ACTION", string.Format(_localizer["UnknownPcscAction"], action));
                return;
        }

        var response = new
        {
            requestId,
            success = true,
            data,
            timestamp = DateTime.UtcNow
        };

        var responseJson = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(responseJson);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static string? GetParam(JsonElement parameters, string key)
        => parameters.ValueKind == JsonValueKind.Object
           && parameters.TryGetProperty(key, out var val)
            ? val.GetString()
            : null;

    private async Task SendErrorAsync(System.Net.WebSockets.WebSocket ws, string? requestId, string code, string message)
    {
        var response = new
        {
            requestId = requestId ?? Guid.NewGuid().ToString(),
            success = false,
            error = new { code, message },
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static IReadOnlySet<string>? ParseSubscribedEvents(string? eventsParam)
    {
        if (string.IsNullOrWhiteSpace(eventsParam))
            return null;

        var events = eventsParam
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return events.Count > 0 ? events : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sendSemaphore.Dispose();
    }

    private sealed record ConnectionEntry(
        System.Net.WebSockets.WebSocket Socket,
        DateTime LastPong,
        IReadOnlySet<string>? SubscribedEvents);
}
