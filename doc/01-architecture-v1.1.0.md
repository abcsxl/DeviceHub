# DeviceHub — 架构设计 (v1.1.0)

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
│    ITransitCardService (协议封装)                 │
│    扩展方法: AddXxxService() / LoadExternalDrivers│
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
| Service | 驱动注册与管理、协议封装（TransitCard）、配置读写合并、日志环形缓冲区 | 单例服务 + DI + DriverLoader |
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

### 交通卡协议封装（TransitCard）

TransitCard 是**纯服务层**，不注册为 `IHardwareService`。它基于 `IPcscService` 实现 JT/T 978 互联互通卡的高层协议封装，提供面向业务的接口。

```csharp
public record CardInfo(
    string CardNumber,
    string? IssuerCode,
    string? CardType,
    string? ExpiryDate,
    string[]? OtherData
);

public record BalanceInfo(int Balance, string Currency = "CNY");

public record TransactionRecord(
    string Type, int Amount, DateTime Timestamp, string? Location
);

public record RechargeInitResult(
    string SessionId, string UnsignedApdu, string SignData
);

public record RechargeResult(
    bool Success, string? Sw1, string? Sw2, string? ErrorMessage = null
);

public interface ITransitCardService
{
    Task<string[]> GetAvailableReadersAsync(CancellationToken ct = default);
    Task<CardInfo> ReadCardInfoAsync(string? readerName = null, CancellationToken ct = default);
    Task<BalanceInfo> ReadBalanceAsync(string? readerName = null, CancellationToken ct = default);
    Task<List<TransactionRecord>> ReadTransactionsAsync(int count = 10, string? readerName = null, CancellationToken ct = default);
    Task<RechargeInitResult> RechargeInitAsync(decimal amount, string? readerName = null, CancellationToken ct = default);
    Task<RechargeResult> RechargeExecuteAsync(string sessionId, string macSignature, CancellationToken ct = default);
}
```

所有操作支持传入 `readerName` 指定读卡器，不传时自动选择任意有卡的读卡器。

### 充值两步流程

```
POST /api/hardware/transitcard/recharge/init
  → 返回 { sessionId, unsignedApdu, signData }
     signData 由外部 HSM/加密机签名后得到 macSignature

POST /api/hardware/transitcard/recharge/execute
  → 参数 { sessionId, macSignature }
  → 返回 { success, sw1, sw2 }
```

DeviceHub 不接触加密密钥。`signData` 由业务系统签名，`macSignature` 送回 DeviceHub 完成充值。

### 注册模式

```csharp
public static class PcscServiceExtensions
{
    public static IServiceCollection AddPcscService(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Drivers:Pcsc:Enabled"))
            return services;

        var useMock = configuration.GetValue<bool>("Drivers:Pcsc:Mock");
        if (useMock)
            services.AddSingleton<IPcscService, MockPcscService>();
        else
            services.AddSingleton<IPcscService, PcscService>();

        return services;
    }
}
```

TransitCard 自动跟随 Pcsc 启用状态注册，无需独立配置开关。

### Mock 模式

通过 `Drivers:Pcsc:Mock` 配置项切换真实硬件与模拟实现。Mock 模式下 `MockPcscService` 和 `MockTransitCardService` 分别提供完整的 `IPcscService` 和 `ITransitCardService` 接口模拟。

**PCSC 模拟能力**：

| 功能 | 模拟行为 |
|------|----------|
| 读卡器列表 | 2 个读卡器：`Mock Reader CL`（带卡片）、`Mock Reader SAM`（无卡片） |
| 卡片存在状态 | `Mock Reader CL` 初始有卡，每 10 秒自动切换插拔状态 |
| ATR | 固定值 `3B8F8001804F0CA0000003060300030000000068` |
| Transmit | 根据 APDU 前缀返回固定响应 |
| CardStatusChanged | 每 10 秒触发一次卡片状态变化事件 |
| Init/Shutdown | 返回成功，启动后台状态监控线程 |

**TransitCard 模拟能力**：

| 功能 | 模拟行为 |
|------|----------|
| 卡片信息 | 返回固定卡号 `1234567890123456`、发行商 `001` |
| 余额 | 返回固定值 `5000`（分） |
| 交易记录 | 返回 10 条模拟记录，交替 DEBIT/CREDIT |
| 充值 | init 返回模拟 session，execute 始终成功 |

**配置示例**：
```json
{
  "Drivers": {
    "Pcsc": {
      "Enabled": true,
      "Mock": true
    }
  }
}
```

> Mock 模式适用于：无硬件环境下的前端联调、CI 自动化测试、功能演示。

## 4. 插件加载（DriverLoader）

闭源驱动的加载机制，启动时自动发现并注册外部驱动。

### 工作方式

```
drivers/                     ← 运行目录下的 drivers 文件夹
├── DeviceHub.YourCard.dll   ← 闭源驱动，实现 IHardwareService
└── DeviceHub.Another.dll
```

### 扩展方法

```csharp
public static IServiceCollection LoadExternalDrivers(
    this IServiceCollection services,
    IConfiguration configuration,
    ILogger? logger = null)
```

### 外部驱动需满足

1. 实现 `IHardwareService` 接口
2. 添加 `[DriverName("YourCard")]` 属性标注名称
3. 在 `appsettings.json` 的 `Drivers` 段配置 `Enabled` 状态

```json
{
  "Drivers": {
    "YourCard": { "Enabled": true }
  }
}
```

### NuGet 包发布

`DeviceHub.Devices.Contracts`（MIT 许可）发布到 nuget.org，供闭源驱动引用接口：

```xml
<PackageId>DeviceHub.Devices.Contracts</PackageId>
<Version>1.0.0</Version>
<Authors>abcsxl</Authors>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
```

闭源驱动通过 `dotnet add package DeviceHub.Devices.Contracts` 引用接口，独立编译后产出 `.dll` 放入 `drivers/` 目录即可。

## 5. 协议分工

| 场景 | 协议 | 理由 |
|------|------|------|
| 配置管理、状态查询、日志、驱动启停 | REST | 无状态，简单 |
| 发送 APDU、读取身份证、交通卡操作 | REST 或 WS | 两者均可 |
| 卡片插拔、打印完成、硬件事件推送 | 仅 WS | 服务端主动推送 |
| 心跳保活、多操作同一会话 | WS | 有状态通道 |

## 6. 配置管理

- 配置文件：`appsettings.json`
- 读取：`IOptions<T>` / `IConfiguration`
- 写入：PUT `/api/config` → 写入 JSON 文件 → `IConfigurationRoot.Reload()` 即时生效
- 配置模型充血，自带 `Validate()` 和 `Merge()` 方法
- 硬件配置通过 `Drivers.Xxx.Enabled` 字段控制，默认全部启用

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

## 7. 日志

- 内存环形缓冲区（`ConcurrentQueue` + `ILoggerProvider`）
- 保留最近 N 条（默认 1000，可通过 `Logging:RingBufferSize` 配置）
- 管理端点 GET `/api/logs?level=ERROR&tail=100` 查询
- 无持久化，服务重启后日志清空

## 8. 多语言支持

- 默认语言：英语（en-US）
- 支持语言：中文简体（zh-CN）
- 实现方式：ASP.NET Core `RequestLocalization` 中间件 + `.resx` 资源文件
- 语言检测优先级：
  1. `Accept-Language` 请求头（如 `zh-CN` 或 `en-US`）
  2. 查询字符串 `?culture=zh-CN`
  3. 默认回退到 en-US
- 资源文件位置：`src/DeviceHub.Service.Api/Resources/Strings.resx`（默认英语）和 `Strings.zh-CN.resx`（中文）
- 所有用户可见文本（错误消息、提示信息等）必须通过 `IStringLocalizer<Program>` 获取
- 新增字符串时，必须同时更新两个资源文件

## 9. 部署

| 平台 | 托管方式 | 打包 |
|------|----------|------|
| Windows | `UseWindowsService()` | Inno Setup（自动启用所有驱动） |
| Linux | `UseSystemd()` + systemd service | 提供 .service 模板 |
| 通用 | 命令行直接运行 | `dotnet publish` 自包含单文件 |

## 10. 错误码

| 错误码 | HTTP 状态 | 说明 |
|--------|-----------|------|
| `DRIVER_NOT_FOUND` | 404 | 驱动未注册 |
| `INVALID_PARAMETERS` | 400 | 参数错误 |
| `HARDWARE_ERROR` | 500 | 硬件错误 |
| `TIMEOUT` | 408 | 操作超时 |

---

## 版本历史
- v1.1.0 (2026-05-22): 新增互联互通卡（TransitCard）协议封装 + DriverLoader 插件加载机制 + Contracts NuGet 发布
- v1.0.3 (2026-05-21): 新增 PCSC Mock 模式支持，安装包多语言选择（Linux）
- v1.0.2 (2026-05-21): 增加多语言支持（i18n），默认英语，支持中文简体
- v1.0.1 (2026-05-20): 端口冲突自动检测与处理 + 配置模型增加 LogLevel + 修复 6 个 P0 bug
- v1.0.0 (2026-05-19): 初版，确立三层架构、协议分工、服务注册模式
