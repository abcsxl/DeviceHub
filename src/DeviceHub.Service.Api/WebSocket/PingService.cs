using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace DeviceHub.Service.Api.WebSocket;

public sealed class PingService : BackgroundService
{
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PongTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<PingService> _logger;

    public PingService(ILogger<PingService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebSocket 心跳服务已启动 (间隔 {Interval}s, 超时 {Timeout}s)",
            PingInterval.TotalSeconds, PongTimeout.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(PingInterval, stoppingToken);

            foreach (var (id, ws) in WebSocketHandler.ActiveConnections)
            {
                if (ws.State != WebSocketState.Open)
                {
                    WebSocketHandler.RemoveConnection(id);
                    continue;
                }

                if (!WebSocketHandler.TryUpdatePingTime(id))
                {
                    _logger.LogWarning("连接 {Id} 心跳超时，关闭", id);
                    WebSocketHandler.RemoveConnection(id);
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
                    WebSocketHandler.RemoveConnection(id);
                }
            }
        }
    }
}
