using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions;
using DeviceHub.Devices.Contracts.Helpers;
using DeviceHub.Devices.Contracts.Abstractions.Services;

using DeviceHub.Cards.TransitCard;
using DeviceHub.Cards.TransitCard.Models.Responses;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Service.Api.WebSocket;

public sealed class WebSocketHandler : IDisposable
{
    private readonly ConcurrentDictionary<Guid, ConnectionEntry> _connections = new();
    private readonly SemaphoreSlim _sendSemaphore = new(5, 5);
    private readonly ILogger<WebSocketHandler> _logger;
    private bool _disposed;

    public int ConnectionCount => _connections.Count;

    public WebSocketHandler(ILogger<WebSocketHandler> logger)
    {
        _logger = logger;
    }

    internal IEnumerable<KeyValuePair<Guid, System.Net.WebSockets.WebSocket>> ActiveConnections
        => !_disposed
            ? _connections
                .Where(kvp => kvp.Value.Socket.State == WebSocketState.Open)
                .Select(kvp => new KeyValuePair<Guid, System.Net.WebSockets.WebSocket>(kvp.Key, kvp.Value.Socket))
            : [];

    internal IReadOnlySet<string>? GetSubscribedEvents(Guid id)
    {
        return _connections.TryGetValue(id, out var entry) ? entry.SubscribedEvents : null;
    }

    internal void RemoveConnection(Guid id)
    {
        _connections.TryRemove(id, out _);
    }

    internal bool IsConnectionHealthy(Guid id)
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
                await context.Response.WriteAsync("Expected a WebSocket request");
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
            catch (WebSocketException ex)
            {
                _logger.LogDebug("WebSocket connection closed unexpectedly: {Message}", ex.Message);
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

            var snapshot = _connections.ToArray();
            var failedIds = new List<Guid>();

            foreach (var (id, entry) in snapshot)
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
                    failedIds.Add(id);
                }
            }

            foreach (var id in failedIds)
                _connections.TryRemove(id, out _);
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private async Task HandleConnectionAsync(Guid connectionId, System.Net.WebSockets.WebSocket ws, IServiceProvider services)
    {
        var buffer = new byte[1024 * 16];
        var messageBuffer = new List<byte>();

        using var connectionCts = new CancellationTokenSource();
        connectionCts.CancelAfter(TimeSpan.FromMinutes(5));

        while (ws.State == WebSocketState.Open && !connectionCts.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), connectionCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await CloseAsync(ws);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleTextMessageAsync(connectionId, ws, json, services);
                }
                else
                {
                    messageBuffer.Clear();
                    messageBuffer.AddRange(buffer.AsSpan(0, result.Count));
                    while (!result.EndOfMessage)
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), connectionCts.Token);
                        messageBuffer.AddRange(buffer.AsSpan(0, result.Count));
                    }
                    var fullJson = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    await HandleTextMessageAsync(connectionId, ws, fullJson, services);
                    messageBuffer.Clear();
                }
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
                    _connections[connectionId] = new ConnectionEntry(entry.Socket, DateTime.UtcNow, entry.SubscribedEvents);
                return;
            }

            var requestId = root.TryGetProperty("requestId", out var rid)
                ? rid.GetString()
                : Guid.NewGuid().ToString();
            var target = root.TryGetProperty("target", out var t) ? t.GetString() : null;
            var action = root.TryGetProperty("action", out var a) ? a.GetString() : null;

            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(action))
            {
                await SendErrorAsync(ws, requestId!, "INVALID_PARAMETERS", "target and action are required");
                return;
            }

            root.TryGetProperty("parameters", out var parameters);

            switch (target)
            {
                case "pcsc":
                    await HandlePcscAction(ws, services, parameters, requestId!, action);
                    break;
                case "transitcard":
                    await HandleTransitCardAction(ws, services, parameters, requestId!, action);
                    break;
                case "printer":
                    await HandlePrinterAction(ws, services, parameters, requestId!, action);
                    break;
                case "id-card":
                    await HandleIdCardAction(ws, services, parameters, requestId!, action);
                    break;
                default:
                {
                    var handled = false;
                    foreach (var handler in services.GetServices<IHardwareWebSocketHandler>())
                    {
                        if (handler.Target == target)
                        {
                            try
                            {
                                handled = await handler.TryHandleAsync(ws, action, parameters, requestId!, services);
                            }
                            catch (Exception ex)
                            {
                                await SendErrorAsync(ws, requestId!, "HARDWARE_ERROR", ex.Message);
                                handled = true;
                            }
                            break;
                        }
                    }
                    if (!handled)
                        await SendErrorAsync(ws, requestId!, "INVALID_ACTION", string.Format("Unknown target: {0}", target));
                    break;
                }
            }
        }
        catch (JsonException)
        {
            await SendErrorAsync(ws, null, "INVALID_PARAMETERS", "Invalid JSON");
        }
    }

    private async Task HandlePcscAction(
        System.Net.WebSockets.WebSocket ws, IServiceProvider services, JsonElement parameters,
        string requestId, string action)
    {
        var pcsc = services.GetService<IPcscService>();
        if (pcsc == null)
        {
            await SendErrorAsync(ws, requestId, "DRIVER_NOT_FOUND", "PCSC driver is not registered");
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
                    await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "readerName is required");
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
                    await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "readerName is required");
                    return;
                }
                var atr = await pcsc.GetAtrAsync(readerName);
                if (atr == null)
                {
                    await SendErrorAsync(ws, requestId, "CARD_NOT_PRESENT", "No card present in reader");
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
                    await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "readerName and apdu are required");
                    return;
                }
                var result = await pcsc.TransmitAsync(readerName, apdu);
                if (!result.Success)
                {
                    var errorCode = result.ErrorCode ?? "HARDWARE_ERROR";
                    var msg = result.ErrorMessage ?? "Transmit failed";
                    if (result.Sw1 != null)
                        msg += $" (SW={result.Sw1}{result.Sw2})";
                    await SendErrorAsync(ws, requestId, errorCode, msg);
                    return;
                }
                if (result.Sw1 != "90" || result.Sw2 != "00")
                {
                    var (code, _) = SwCodeHelper.ClassifySw(result.Sw1 ?? "FF", result.Sw2 ?? "FF");
                    var msg = $"Transmit failed (SW={result.Sw1}{result.Sw2})";
                    await SendErrorAsync(ws, requestId, code, msg);
                    return;
                }
                data = new { sw1 = result.Sw1, sw2 = result.Sw2, responseData = result.ResponseData };
                break;
            }

            default:
                await SendErrorAsync(ws, requestId, "INVALID_ACTION", string.Format("Unknown pcsc action: {0}", action));
                return;
        }

        var response = WsResponseHelper.Ok(requestId, data);

        var responseJson = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(responseJson);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task HandleTransitCardAction(
        System.Net.WebSockets.WebSocket ws, IServiceProvider services, JsonElement parameters,
        string requestId, string action)
    {
        var transitCard = services.GetService<ITransitCardService>();
        if (transitCard == null)
        {
            await SendErrorAsync(ws, requestId, "DRIVER_NOT_FOUND", "Transit card service not available");
            return;
        }

        object? data = null;
        try
        {
            switch (action)
            {
                case "read_card_info":
                {
                    var readerName = GetParam(parameters, "readerName");
                    var info = await transitCard.ReadCardInfoAsync(readerName);
                    data = info;
                    break;
                }

                case "reset":
                {
                    var readerName = GetParam(parameters, "readerName");
                    var atr = await transitCard.ResetCardAsync(readerName);
                    data = new { atr };
                    break;
                }

                case "read_balance":
                {
                    var readerName = GetParam(parameters, "readerName");
                    var balance = await transitCard.ReadBalanceAsync(readerName);
                    data = balance;
                    break;
                }

                case "read_transactions":
                {
                    var readerName = GetParam(parameters, "readerName");
                    var countStr = GetParam(parameters, "count");
                    var count = int.TryParse(countStr, out var c) ? c : 10;
                    var records = await transitCard.ReadTransactionsAsync(count, readerName);
                    data = new { records };
                    break;
                }

                case "recharge_init":
                {
                    var readerName = GetParam(parameters, "readerName");
                    var amountStr = GetParam(parameters, "amount");
                    if (!int.TryParse(amountStr, out var amount) || amount <= 0)
                    {
                        await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "Invalid amount");
                        return;
                    }
                    var initResult = await transitCard.RechargeInitAsync(amount, readerName);
                    data = initResult;
                    break;
                }

                case "recharge_execute":
                {
                    var sessionId = GetParam(parameters, "sessionId");
                    var macSignature = GetParam(parameters, "macSignature");
                    if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(macSignature))
                    {
                        await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "sessionId and macSignature are required");
                        return;
                    }
                    var execResult = await transitCard.RechargeExecuteAsync(sessionId, macSignature);
                    if (execResult is RechargeResult r && !r.Success)
                    {
                        var msg = r.ErrorMessage ?? "Recharge execution failed";
                        if (r.Sw1 != null) msg += $" (SW={r.Sw1}{r.Sw2})";
                        var (code, _) = SwCodeHelper.ClassifySw(r.Sw1 ?? "FF", r.Sw2 ?? "FF");
                        await SendErrorAsync(ws, requestId, code, msg);
                        return;
                    }
                    data = null;
                    break;
                }

                case "consume_init":
                {
                    var readerName = GetParam(parameters, "readerName");
                    var dealflagStr = GetParam(parameters, "dealflag");
                    var keyindexStr = GetParam(parameters, "keyindex");
                    var amountStr = GetParam(parameters, "amount");
                    var termainno = GetParam(parameters, "termainno");

                    var dealflag = int.TryParse(dealflagStr, out var df) ? df : 2;
                    var keyindex = int.TryParse(keyindexStr, out var ki) ? ki : 0;
                    if (!int.TryParse(amountStr, out var amount) || amount <= 0)
                    {
                        await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "amount must be greater than 0");
                        return;
                    }
                    if (string.IsNullOrEmpty(termainno) || termainno.Length != 12)
                        termainno = "000000000000";

                    var result = await transitCard.ConsumeInitAsync(dealflag, keyindex, amount, termainno, readerName);
                    data = result;
                    break;
                }

                case "consume_capp_init":
                {
                    var readerName = GetParam(parameters, "readerName");
                    var dealflagStr = GetParam(parameters, "dealflag");
                    var keyindexStr = GetParam(parameters, "keyindex");
                    var amountStr = GetParam(parameters, "amount");
                    var termainno = GetParam(parameters, "termainno");

                    var dealflag = int.TryParse(dealflagStr, out var df) ? df : 2;
                    var keyindex = int.TryParse(keyindexStr, out var ki) ? ki : 0;
                    if (!int.TryParse(amountStr, out var amount) || amount <= 0)
                    {
                        await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "amount must be greater than 0");
                        return;
                    }
                    if (string.IsNullOrEmpty(termainno) || termainno.Length != 12)
                        termainno = "000000000000";

                    var resultCapp = await transitCard.ConsumeCappInitAsync(dealflag, keyindex, amount, termainno, readerName);
                    data = resultCapp;
                    break;
                }

                case "consume_execute":
                {
                    var sessionId = GetParam(parameters, "sessionId");
                    var termdealnoStr = GetParam(parameters, "termdealno");
                    var dealtime = GetParam(parameters, "dealtime");
                    var mac1 = GetParam(parameters, "mac1");
                    if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(dealtime) || string.IsNullOrEmpty(mac1))
                    {
                        await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "sessionId, termdealno, dealtime and mac1 are required");
                        return;
                    }
                    var termdealno = int.TryParse(termdealnoStr, out var tdn) ? tdn : 0;
                    var execResult = await transitCard.ConsumeExecuteAsync(sessionId, termdealno, dealtime, mac1);
                    if (execResult is ConsumeResult cr && !cr.Success)
                    {
                        var msg = cr.ErrorMessage ?? "Consume execution failed";
                        if (cr.Sw1 != null) msg += $" (SW={cr.Sw1}{cr.Sw2})";
                        var (code, _) = SwCodeHelper.ClassifySw(cr.Sw1 ?? "FF", cr.Sw2 ?? "FF");
                        await SendErrorAsync(ws, requestId, code, msg);
                        return;
                    }
                    data = null;
                    break;
                }

                default:
                    await SendErrorAsync(ws, requestId, "INVALID_ACTION", "Unknown transitcard action");
                    return;
            }
        }
        catch (InvalidOperationException ex)
        {
            var (code, msg) = ErrorCodeHelper.ParseTransmitError(ex.Message);
            await SendErrorAsync(ws, requestId, code, msg);
            return;
        }

        var response = WsResponseHelper.Ok(requestId, data);

        var responseJson = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(responseJson);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task HandlePrinterAction(
        System.Net.WebSockets.WebSocket ws, IServiceProvider services, JsonElement parameters,
        string requestId, string action)
    {
        var printer = services.GetService<IPrinterService>();
        if (printer == null)
        {
            await SendErrorAsync(ws, requestId, "DRIVER_NOT_FOUND", "Printer service not available");
            return;
        }

        object? data = null;
        switch (action)
        {
            case "list":
                var printers = await printer.GetPrintersAsync();
                data = new { printers };
                break;

            case "print":
            {
                var text = GetParam(parameters, "text");
                var printerName = GetParam(parameters, "printerName");
                if (string.IsNullOrEmpty(text))
                {
                    await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "text is required");
                    return;
                }
                var ok = await printer.PrintTextAsync(text, printerName);
                if (!ok)
                {
                    await SendErrorAsync(ws, requestId, "PRINT_FAILED", "Print failed");
                    return;
                }
                data = null;
                break;
            }

            case "print_raw":
            {
                var rawHex = GetParam(parameters, "data");
                var printerName = GetParam(parameters, "printerName");
                if (string.IsNullOrEmpty(rawHex))
                {
                    await SendErrorAsync(ws, requestId, "INVALID_PARAMETERS", "data (hex) is required");
                    return;
                }
                var rawData = Convert.FromHexString(rawHex);
                var okRaw = await printer.PrintRawAsync(rawData, printerName);
                if (!okRaw)
                {
                    await SendErrorAsync(ws, requestId, "PRINT_FAILED", "Print failed");
                    return;
                }
                data = null;
                break;
            }

            default:
                await SendErrorAsync(ws, requestId, "INVALID_ACTION", "Unknown printer action");
                return;
        }

        var response = WsResponseHelper.Ok(requestId, data);

        var responseJson = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(responseJson);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task HandleIdCardAction(
        System.Net.WebSockets.WebSocket ws, IServiceProvider services, JsonElement parameters,
        string requestId, string action)
    {
        var idCard = services.GetService<IIdCardService>();
        if (idCard == null)
        {
            await SendErrorAsync(ws, requestId, "DRIVER_NOT_FOUND", "IdCard service not available");
            return;
        }

        object? data = null;
        try
        {
            switch (action)
            {
                case "readers":
                    var readers = await idCard.GetReadersAsync();
                    data = new { readers };
                    break;

                case "read":
                {
                    var readerName = GetParam(parameters, "readerName");
                    var info = await idCard.ReadCardAsync(readerName);
                    if (info == null)
                    {
                        await SendErrorAsync(ws, requestId, "CARD_NOT_FOUND", "No ID card detected");
                        return;
                    }
                    data = info;
                    break;
                }

                case "read_photo":
                {
                    var readerName = GetParam(parameters, "readerName");
                    var photo = await idCard.ReadPhotoAsync(readerName);
                    if (photo == null || photo.Length == 0)
                    {
                        await SendErrorAsync(ws, requestId, "PHOTO_NOT_FOUND", "No photo data");
                        return;
                    }
                    data = new { photo = Convert.ToBase64String(photo), format = "image/jpeg" };
                    break;
                }

                default:
                    await SendErrorAsync(ws, requestId, "INVALID_ACTION", "Unknown id-card action");
                    return;
            }
        }
        catch (InvalidOperationException ex)
        {
            await SendErrorAsync(ws, requestId, "CARD_NOT_FOUND", ex.Message);
            return;
        }

        var response = WsResponseHelper.Ok(requestId, data);

        var responseJson = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(responseJson);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static string? GetParam(JsonElement parameters, string key)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty(key, out var val))
            return null;
        return val.ValueKind switch
        {
            JsonValueKind.String => val.GetString(),
            JsonValueKind.Number => val.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }
    private async Task SendErrorAsync(System.Net.WebSockets.WebSocket ws, string? requestId, string code, string message)
    {
        var response = WsResponseHelper.Error(requestId ?? Guid.NewGuid().ToString(), code, message);

        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task CloseAsync(System.Net.WebSockets.WebSocket ws)
    {
        try
        {
            if (ws.State == WebSocketState.Open)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or WebSocketException)
        {
        }
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

        var snapshot = _connections.ToArray();
        foreach (var (_, entry) in snapshot)
        {
            try
            {
                if (entry.Socket.State == WebSocketState.Open)
                {
                    var ws = entry.Socket;
                    var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    _ = CloseSocketAsync(ws, closeCts);
                }
                else
                {
                    try { entry.Socket.Dispose(); } catch { }
                }
            }
            catch
            {
            }
        }

        _sendSemaphore.Dispose();
    }

    private static async Task CloseSocketAsync(System.Net.WebSockets.WebSocket ws, CancellationTokenSource cts)
    {
        using (cts)
        {
            try
            {
                await ws.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "Server shutting down", cts.Token);
            }
            catch
            {
            }
        }

        try { ws.Dispose(); } catch { }
    }

    private sealed record ConnectionEntry(
        System.Net.WebSockets.WebSocket Socket,
        DateTime LastPong,
        IReadOnlySet<string>? SubscribedEvents);
}
