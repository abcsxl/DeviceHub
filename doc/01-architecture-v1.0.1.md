# DeviceHub — 架构设计 (v1.0.1)

## 1. 整体架构

```
┌──────────────────────────────────────────────────┐
│               Presentation Layer                 │
│   REST (管理)  +  WebSocket (实时)  +  SPA (UI)  │
└──────────────┬───────────────────┬───────────────┘
               │                   │
               ▼                   ▼
┌──────────────────────────────────────────────────┐
│               Service Layer                      │
│    DriverRegistry  +  ConfigService  +  Logs     │
│    扩展方法: AddPcscService() / AddXxxService()   │
└──────────────┬───────────────────┬───────────────┘
               │                   │
               ▼                   ▼
┌──────────────────────────────────────────────────┐
│              Hardware Layer                      │
│  IHardwareService (基接口)  ←  IPcscService       │
│                                IPrinterService    │
│                                IIdCardService     │
└──────────────────────────────────────────────────┘
```

## 2. 三层职责

| 层级 | 职责 | 技术 |
|------|------|------|
| Presentation | 接收 HTTP/WebSocket 请求，路由到对应 handler | ASP.NET Core Minimal APIs |
| Service | 驱动注册与管理、配置读写与合并、日志环形缓冲区 | 单例服务 + DI |
| Hardware | 硬件抽象接口 + 各自实现 lib | `IHardwareService` 基接口 + 独立接口 |

路由由 ASP.NET Core 原生中间件完成，**不设置自定义 MessageRouter**。

## 3. 硬件抽象设计

### 基接口（仅生命周期）

```csharp
public interface IHardwareService
{
    string Name { get; }
    HardwareStatus Status { get; }
    Task InitAsync(CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}

public enum HardwareStatus { Stopped, Initializing, Running, Error }
```

### PCSC 读卡器

```csharp
public record ReaderInfo(
    string Name,
    bool IsCardPresent,
    string? Atr = null
);

public record TransmitResult(
    bool Success,
    string? Sw1 = null,
    string? Sw2 = null,
    string? ResponseData = null,
    string? ErrorMessage = null
);

public interface IPcscService : IHardwareService
{
    Task<IReadOnlyList<ReaderInfo>> ListReadersAsync(CancellationToken ct = default);
    Task<ReaderInfo> GetReaderInfoAsync(string readerName, CancellationToken ct = default);
    Task<TransmitResult> TransmitAsync(string readerName, string apdu, CancellationToken ct = default);
    Task<string?> GetAtrAsync(string readerName, CancellationToken ct = default);
    event EventHandler<CardStatusEventArgs>? CardStatusChanged;
}
```

每种硬件独立接口，不强求统一方法签名。后续打印机、身份证等各自定义自己的能力接口。

### 注册模式

```csharp
public static class PcscServiceExtensions
{
    public static IServiceCollection AddPcscService(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("Drivers:Pcsc:Enabled"))
            services.AddSingleton<IPcscService, PcscService>();
        return services;
    }
}
```

## 4. 协议分工

| 场景 | 协议 | 理由 |
|------|------|------|
| 配置管理、状态查询、日志、驱动启停 | REST | 无状态，简单 |
| 发送 APDU、读取身份证 | REST 或 WS | 两者均可 |
| 卡片插拔、打印完成、硬件事件推送 | 仅 WS | 服务端主动推送 |
| 心跳保活、多操作同一会话 | WS | 有状态通道 |

## 5. 配置管理

- 配置文件：`appsettings.json`
- 读取：`IOptions<T>` / `IConfiguration`
- 写入：PUT `/api/config` → 写入 JSON 文件 → `IConfigurationRoot.Reload()` 即时生效
- 配置模型充血，自带 `Validate()` 和 `Merge()` 方法
- 硬件配置通过 `Drivers.Xxx.Enabled` 字段控制，安装时勾选

### 配置模型结构

```json
{
  "Server": {
    "HttpPort": 5000,
    "WebSocketPath": "/ws"
  },
  "Drivers": {
    "Pcsc": {
      "Enabled": true,
      "AutoReconnect": true
    },
    "Printer": {
      "Enabled": false
    },
    "IdCard": {
      "Enabled": false,
      "ComPort": "COM3"
    }
  },
  "Logging": {
    "RingBufferSize": 1000,
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

| 路径 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| Server.HttpPort | int | 5000 | HTTP 监听端口。启动时自动检测冲突，若被占用则尝试 +1 至 +10，均被占用则启动失败 |
| Server.WebSocketPath | string | "/ws" | WebSocket 端点路径 |
| Drivers.*.Enabled | bool | false | 是否启用该硬件 |
| Logging.RingBufferSize | int | 1000 | 日志环形缓冲区大小 |

> 配置修改通过 PUT `/api/config` 接口完成，不建议手动编辑 `appsettings.json`。

## 6. 日志

- 内存环形缓冲区（`ConcurrentQueue` + `ILoggerProvider`）
- 保留最近 N 条（默认 1000，可通过 `Logging:RingBufferSize` 配置）
- 管理端点 GET `/api/logs?level=ERROR&tail=100` 查询
- 无持久化，服务重启后日志清空

## 7. 部署

| 平台 | 托管方式 | 打包 |
|------|----------|------|
| Windows | `UseWindowsService()` | Inno Setup（安装时勾选硬件） |
| Linux | `UseSystemd()` + systemd service | 提供 .service 模板 |
| 通用 | 命令行直接运行 | `dotnet publish` 自包含单文件 |

## 8. 错误码

| 错误码 | HTTP 状态 | 说明 |
|--------|-----------|------|
| `DRIVER_NOT_FOUND` | 404 | 驱动未注册 |
| `INVALID_PARAMETERS` | 400 | 参数错误 |
| `HARDWARE_ERROR` | 500 | 硬件错误 |
| `TIMEOUT` | 408 | 操作超时 |

---

## 版本历史
- v1.0.1 (2026-05-20): 端口冲突自动检测与处理 + 配置模型增加 LogLevel + 修复 6 个 P0 bug
- v1.0.0 (2026-05-19): 初版，确立三层架构、协议分工、服务注册模式
