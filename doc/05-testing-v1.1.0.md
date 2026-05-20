# DeviceHub — 测试指南 (v1.0.0)

## 运行服务

```bash
cd src/DeviceHub.Service.Api
dotnet run --urls http://localhost:5000
```

开发模式下 `launchSettings.json` 中 `ASPNETCORE_ENVIRONMENT=Development` 会自动启用详细日志。

---

## 管理接口测试（无硬件依赖）

以下测试**不需要任何读卡器**，确认服务正常运行即可。

### 健康检查

```bash
curl http://localhost:5000/health
```

预期：`{"status":"healthy","timestamp":"..."}`

### 服务状态

```bash
curl http://localhost:5000/api/status
```

预期：返回 version、platform、httpPort、uptime、drivers 列表。`httpPort` 字段显示实际监听的端口号。

### 日志查询

```bash
curl "http://localhost:5000/api/logs?tail=5"
curl "http://localhost:5000/api/logs?level=ERROR&tail=10"
```

### 配置管理

```bash
# 读取配置
curl http://localhost:5000/api/config

# 更新配置（Merge 语义，只传要改的字段）
curl -X PUT http://localhost:5000/api/config ^
  -H "Content-Type: application/json" ^
  -d "{\"logging\":{\"ringBufferSize\":500}}"
```

### 驱动管理

```bash
# 列表（目前为空）
curl http://localhost:5000/api/drivers

# 启用不存在的驱动 → 404
curl -X POST http://localhost:5000/api/drivers/Foo/enable
```

### 重启服务

```bash
curl -X POST http://localhost:5000/api/service/restart
```

返回 202，服务 1 秒后退出（Windows Service / systemd 自动拉起的场景需要配置服务管理）。

### 硬件操作端点（驱动未注册 → 503）

```bash
curl http://localhost:5000/api/hardware/pcsc/readers
```

预期：503 `PCSC 驱动未注册`

---

## PCSC 硬件测试（需要读卡器）

### 前置条件

1. Windows：读卡器驱动已装好，设备管理器中可见
2. Linux：`sudo apt install pcscd libpcsclite-dev`
3. 修改 `appsettings.json` 开启 Pcsc 驱动：

```json
{
  "Drivers": {
    "Pcsc": { "Enabled": true }
  }
}
```

### 确认 PCSC 服务启动

```bash
curl http://localhost:5000/api/drivers
```

预期：
```json
[
  {
    "name": "Pcsc",
    "status": "Running",
    "enabled": true,
    ...
  }
]
```

### 读卡器列表

```bash
curl http://localhost:5000/api/hardware/pcsc/readers
```

无卡时：
```json
{ "readers": [{"name":"OMNIKEY CardMan...","isCardPresent":false}] }
```

插卡后重试：
```json
{ "readers": [{"name":"OMNIKEY CardMan...","isCardPresent":true,"atr":"3B..."}] }
```

### 获取 ATR

```bash
# 注意：读卡器名含空格需要 URL 编码
curl "http://localhost:5000/api/hardware/pcsc/readers/OMNIKEY%20CardMan/atr"
```

### 发送 APDU

```bash
curl -X POST http://localhost:5000/api/hardware/pcsc/transmit ^
  -H "Content-Type: application/json" ^
  -d "{\"readerName\":\"OMNIKEY CardMan\",\"apdu\":\"00A4040008A00102030405060700\"}"
```

无卡时返回 404 `CARD_NOT_PRESENT`，有卡时返回 SW1/SW2/responseData。

---

## WebSocket 测试

### 使用 wscat

```bash
# 安装
npm install -g wscat

# 连接
wscat -c ws://localhost:5000/ws

# 发送 APDU 请求
{"requestId":"test-1","target":"pcsc","action":"transmit","parameters":{"readerName":"OMNIKEY CardMan","apdu":"00A4040008A00102030405060700"}}

# 列出读卡器
{"requestId":"test-2","target":"pcsc","action":"list_readers"}

# 驱动未注册时预期响应
{"requestId":"test-1","success":false,"error":{"code":"DRIVER_NOT_FOUND","message":"PCSC 驱动未注册"}}
```

### 心跳

服务端每 30 秒发送 `{"type":"ping"}`，客户端回复 `{"type":"pong"}`。

---

## 自动化测试框架

### 项目结构（可选，v1.1.0+）

```
tests/
├── DeviceHub.Service.Tests/            # 单元测试
│   ├── AppConfigTests.cs
│   ├── DriverRegistryTests.cs
│   └── InMemoryLogProviderTests.cs
└── DeviceHub.Integration.Tests/        # 集成测试
    └── ApiEndpointsTests.cs            # 使用 TestServer
```

### 集成测试关键代码

```csharp
// 使用 Microsoft.AspNetCore.TestHost 模拟 HTTP 请求
using var host = await new HostBuilder()
    .ConfigureWebHost(webBuilder =>
    {
        webBuilder.UseTestServer()
            .UseStartup<Program>();
    })
    .StartAsync();

var client = host.GetTestClient();
var response = await client.GetAsync("/api/status");
response.EnsureSuccessStatusCode();
```

### 测试场景清单

| 场景 | 类型 | 优先级 |
|------|------|--------|
| /api/status 返回正确字段 | 集成 | P0 |
| PUT /api/config Merge 语义正确 | 单元 | P0 |
| 配置 Validate 校验端口范围 | 单元 | P0 |
| 日志环形缓冲区不超上限 | 单元 | P0 |
| Drivers enable/disable 状态切换 | 集成 | P0 |
| WS 心跳 ping/pong | 集成 | P1 |
| WS pcsc.transmit 路由 | 集成 | P1 |
| PCSC 读卡器插拔事件推送 | 手动 | P1 |

---

## 版本历史

- v1.0.0 (2026-05-20): 增加 httpPort 字段验证说明
- v1.0.0 (2026-05-19): 初版，覆盖管理接口 + PCSC 硬件 + WebSocket 测试
