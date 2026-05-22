using System.Net.WebSockets;
using System.Text;

namespace DeviceHub.Service.Api.WebSocket;

public sealed class PingService : BackgroundService
{
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PongTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<PingService> _logger;
    private readonly WebSocketHandler _wsHandler;

    public PingService(ILogger<PingService> logger, WebSocketHandler wsHandler)
    {
        _logger = logger;
        _wsHandler = wsHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebSocket heartbeat service started (interval {Interval}s, timeout {Timeout}s)",
            PingInterval.TotalSeconds, PongTimeout.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(PingInterval, stoppingToken);

            foreach (var (id, ws) in _wsHandler.ActiveConnections)
            {
                if (ws.State != WebSocketState.Open)
                {
                    _wsHandler.RemoveConnection(id);
                    continue;
                }

                if (!_wsHandler.IsConnectionHealthy(id))
                {
                    _logger.LogWarning("Connection {Id} heartbeat timeout, closing", id);
                    _wsHandler.RemoveConnection(id);
                    try
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Ping timeout", CancellationToken.None);
                    }
                    catch
                    {
                    }
                    continue;
                }

                var ping = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
                try
                {
                    await ws.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, stoppingToken);
                }
                catch
                {
                    _wsHandler.RemoveConnection(id);
                }
            }
        }
    }
}
