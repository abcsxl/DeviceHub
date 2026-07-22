using System.Text.Json;

namespace DeviceHub.Devices.Contracts.Abstractions;

/// <summary>
/// 允许插件驱动注册 WebSocket action 处理器。
/// 实现类通过 DI 注册为 IHardwareWebSocketHandler，WS 路由自动发现并分发。
/// </summary>
public interface IHardwareWebSocketHandler
{
    /// <summary>WS target 名称，如 "kacyber-go-card"。</summary>
    string Target { get; }

    /// <summary>
    /// 尝试处理 WS action。
    /// 返回 true 表示已处理（包括返回错误），false 表示此 target 不由当前处理器负责。
    /// </summary>
    Task<bool> TryHandleAsync(
        System.Net.WebSockets.WebSocket ws,
        string action,
        JsonElement parameters,
        string requestId,
        IServiceProvider services);
}
