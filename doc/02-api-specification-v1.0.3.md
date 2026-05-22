# DeviceHub — API 规范 (v1.0.2)

## 基础信息

- 端口：默认 `5000`，通过 `Server:HttpPort` 配置。启动时自动检测端口冲突，若默认端口被占用则尝试 `HttpPort+1` 至 `HttpPort+10`，均被占用则启动失败。安装程序会在默认端口被占用时提示用户指定新端口
- 协议：HTTP/1.1 + WebSocket
- 数据格式：JSON，UTF-8
- 鉴权：当前不实现，所有端点无鉴权
- CORS：允许所有来源（开发阶段），所有 REST 端点返回 `Access-Control-Allow-Origin: *`
- 时间戳：所有时间字段均为 ISO 8601 UTC

## 多语言支持

所有用户可见的错误消息和提示信息支持多语言。默认返回英语（en-US）。

### 语言切换方式

客户端通过 `Accept-Language` 请求头指定语言：

| 请求头值 | 返回语言 |
|----------|----------|
| `en-US` | 英语（默认） |
| `zh-CN` | 中文简体 |

**示例**：
```
GET /api/status
Accept-Language: zh-CN
```

也可通过查询字符串指定：`GET /api/config?culture=zh-CN`（优先级低于 `Accept-Language`）。

**错误消息示例**：

| 场景 | en-US | zh-CN |
|------|-------|-------|
| 驱动未注册 | `PCSC driver is not registered` | `PCSC 驱动未注册` |
| 无卡片 | `No card present in reader` | `读卡器中无卡片` |
| 参数缺失 | `readerName and apdu are required` | `readerName 和 apdu 为必填项` |

未指定语言或语言不支持时，默认返回英语。

---

## Mock 模式

通过 `Drivers:Pcsc:Mock` 配置项启用。Mock 模式下所有 REST 和 WebSocket 端点行为一致，返回固定模拟数据。

### 模拟读卡器

| 读卡器名称 | 初始卡片状态 | ATR |
|------------|-------------|-----|
| `Mock Reader CL` | 有卡（每 10 秒自动切换插拔） | `3B8F8001804F0CA0000003060300030000000068` |
| `Mock Reader SAM` | 无卡 | null |

### 模拟 Transmit 响应

| APDU 前缀 | SW1 | SW2 | ResponseData | 说明 |
|-----------|-----|-----|--------------|------|
| `00A4040007A000000003869807` | `90` | `00` | `0102030405` | SELECT AID |
| `805C000204` | `90` | `00` | `00000960` | GET BALANCE |
| `00B00000` | `90` | `00` | `123456789012345678` | GET CARD NUMBER |
| `00B2010C` | `90` | `00` | `0102030405060708090A` | GET TRANSACTION LOG |
| 其他 | `90` | `00` | `MOCK_RESPONSE_DATA` | 默认成功 |

### 模拟事件推送

每 10 秒自动触发 `card_status_changed` 事件，`Mock Reader CL` 的卡片状态在 `card_present` 和 `empty` 之间切换。

---

## 通用响应约定

### 成功响应

```json
{
  "字段1": "值1",
  ...
}
```

所有 2xx 响应直接返回业务数据对象，无通用外层包装。

### 错误响应

```json
{
  "error": "ERROR_CODE",
  "message": "人类可读的错误描述"
}
```

### HTTP 状态码使用规范

| 状态码 | 含义 | 适用场景 |
|--------|------|---------|
| 200 | 成功 | GET、PUT、POST 操作成功 |
| 202 | 已接受 | 异步操作（如重启） |
| 400 | 参数错误 | 请求体校验失败 |
| 404 | 未找到 | 驱动不存在 |
| 408 | 超时 | 硬件操作超时 |
| 500 | 服务端错误 | 硬件异常或内部错误 |

---

## REST API

### 1. `/api/status` — 服务状态

**GET** `/api/status`

获取服务运行状态、版本、各驱动状态、WebSocket 连接数等。前端管理页面启动后首先调用此接口。

**请求参数**：无

**响应 `200`**：
```json
{
  "version": "1.0.0.0",
  "platform": "windows/x64",
  "httpPort": 5000,
  "startTime": "2026-05-19T08:08:36Z",
  "uptime": "00:01:23.4567890",
  "webSocketConnections": 2,
  "drivers": [
    {
      "name": "Pcsc",
      "status": "Running",
      "enabled": true,
      "registeredAt": "2026-05-19T08:08:36Z",
      "details": null
    }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| version | string | 程序集版本号 |
| platform | string | `windows/x64` / `linux/arm64` / `kylin/x64` |
| httpPort | int | 实际监听的 HTTP 端口号 |
| startTime | string (ISO 8601) | 服务启动时间 |
| uptime | string (duration) | 已运行时长（hh:mm:ss.fff） |
| webSocketConnections | int | 当前 WebSocket 连接数 |
| drivers | array | 所有已注册驱动列表 |

---

### 2. `/api/config` — 配置管理

#### GET `/api/config`

获取当前完整配置。

**响应 `200`**：
```json
{
  "server": {
    "httpPort": 5000,
    "webSocketPath": "/ws"
  },
  "drivers": {
    "Pcsc": { "enabled": true, "autoReconnect": true },
    "Printer": { "enabled": false },
    "IdCard": { "enabled": false, "comPort": "COM3" }
  },
  "logging": {
    "ringBufferSize": 1000
  },
  "configVersion": 1
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| configVersion | int | 配置版本号，每次 PUT 递增 |

#### PUT `/api/config`

更新配置。**只传需要修改的字段**，其余保留原值（Merge 语义）。

**请求体**：传需要修改的部分即可
```json
{
  "server": {
    "httpPort": 8080
  },
  "drivers": {
    "Pcsc": { "enabled": true }
  }
}
```

**响应 `200`**：返回合并后的完整配置（结构同 GET）

**响应 `400`**：校验失败
```json
{
  "error": "INVALID_PARAMETERS",
  "message": "Server.HttpPort must be between 1 and 65535"
}
```

> **生效机制**：配置写入 `appsettings.json` 后即时调用 `IConfigurationRoot.Reload()`。服务端口的变更需重启生效，推送类配置即时生效。

#### POST `/api/config/reset`

恢复配置至出厂默认值。默认值在服务启动时从初始 `appsettings.json` 缓存，后续所有 PUT 修改均不影响缓存内容。

**响应 `200`**：返回恢复后的默认配置（结构同 GET）

**响应 `500`**：
```json
{
  "error": "HARDWARE_ERROR",
  "message": "No default configuration available"
}
```

---

### 3. `/api/logs` — 日志查询

**GET** `/api/logs?level=INFO&tail=100`

服务端内存环形缓冲区中查询最近日志。服务重启后日志清空。

**查询参数**：

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| level | string | 不筛选 | 日志级别过滤：`TRACE` / `DEBUG` / `INFO` / `WARN` / `ERROR`，不区分大小写 |
| tail | int | 100 | 返回条数，最大 10000 |

**响应 `200`**：
```json
{
  "total": 42,
  "entries": [
    {
      "timestamp": "2026-05-19T08:08:32.8877994Z",
      "level": "INFORMATION",
      "category": "Microsoft.Hosting.Lifetime",
      "message": "Now listening on: http://localhost:5000",
      "exception": null
    }
  ]
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| entries[].level | string | 全大写日志级别 |
| entries[].category | string | 日志来源分类名 |
| entries[].exception | string? | 异常堆栈，无异常时为 null |

---

### 4. `/api/drivers` — 驱动管理

#### GET `/api/drivers`

列出所有已注册驱动及其当前状态。

**响应 `200`**：
```json
[
  {
    "name": "Pcsc",
    "status": "Running",
    "enabled": true,
    "registeredAt": "2026-05-19T08:08:36Z",
    "details": null
  }
]
```

| 字段 | 类型 | 说明 |
|------|------|------|
| status | string | `Stopped` / `Initializing` / `Running` / `Error` |

#### POST `/api/drivers/{name}/enable`

启用指定驱动。写配置 + 初始化硬件。

**路径参数**：

| 参数 | 类型 | 说明 |
|------|------|------|
| name | string | 驱动名称，如 `Pcsc` |

**响应 `200`**：
```json
{
  "name": "Pcsc",
  "status": "Running",
  "enabled": true
}
```

**响应 `404`**：
```json
{
  "error": "DRIVER_NOT_FOUND",
  "message": "驱动 Pcsc 未注册"
}
```

#### POST `/api/drivers/{name}/disable`

禁用指定驱动。写配置 + 关闭硬件。

**响应 `200`**：
```json
{
  "name": "Pcsc",
  "status": "Stopped",
  "enabled": false
}
```

---

### 5. `/api/service/restart` — 重启服务

**POST** `/api/service/restart`

触发服务重启。服务端延迟 1 秒后退出进程，由系统服务管理器（Windows Service / systemd）自动拉起。

**响应 `202`**：
```json
{
  "message": "服务将在 1 秒后重启"
}
```

> 注意：调用此接口后 TCP 连接随即断开，客户端应做好重试准备。

---

### 6. `/api/health` — 健康检查

**GET** `/api/health`

给 Docker 容器编排 / systemd 健康检查使用的轻量端点。

给 Docker 容器编排 / systemd 健康检查使用的轻量端点。

**响应 `200`**：
```json
{
  "status": "healthy",
  "timestamp": "2026-05-19T08:08:37Z"
}
```

---

### 7. PCSC 硬件操作端点（REST）

命名规则：`/api/hardware/pcsc/...`。一次性操作推荐走 REST，实时交互推荐走 WebSocket。

#### GET `/api/hardware/pcsc/readers`

获取所有读卡器列表，含卡片存在状态和 ATR。

**响应 `200`**：
```json
{
  "readers": [
    {
      "name": "OMNIKEY CardMan 5x21-CL 0",
      "isCardPresent": true,
      "atr": "3B8F8001804F0CA0000003060300030000000068"
    },
    {
      "name": "OMNIKEY CardMan 5x21-CL 1",
      "isCardPresent": false,
      "atr": null
    }
  ]
}
```

**响应 `500`**（驱动未初始化/未启用）：
```json
{
  "error": "HARDWARE_ERROR",
  "message": "Pcsc driver is not running"
}
```

#### GET `/api/hardware/pcsc/readers/{name}`

获取指定读卡器的详细状态。

**路径参数**：
| 参数 | 类型 | 说明 |
|------|------|------|
| name | string | 读卡器名称（URL 编码） |

**响应 `200`**：
```json
{
  "name": "OMNIKEY CardMan 5x21-CL 0",
  "isCardPresent": true,
  "atr": "3B8F8001804F0CA0000003060300030000000068"
}
```

**响应 `404`**：
```json
{
  "error": "DRIVER_NOT_FOUND",
  "message": "Reader not found: OMNIKEY CardMan 5x21-CL 0"
}
```

#### GET `/api/hardware/pcsc/readers/{name}/atr`

获取指定读卡器中卡片的 ATR（Answer to Reset）。

**响应 `200`**：
```json
{
  "atr": "3B8F8001804F0CA0000003060300030000000068"
}
```

**响应 `404`**（卡片未插入或读卡器不存在）：
```json
{
  "error": "CARD_NOT_PRESENT",
  "message": "No card present in reader"
}
```

#### POST `/api/hardware/pcsc/transmit`

向指定读卡器发送 APDU 指令。

**请求体**：
```json
{
  "readerName": "OMNIKEY CardMan 5x21-CL 0",
  "apdu": "00A4040008A00102030405060700"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| readerName | string | 是 | 读卡器名称 |
| apdu | string | 是 | 十六进制 APDU 字符串（不含空格、不含 `0x` 前缀） |

**响应 `200`**：
```json
{
  "sw1": "90",
  "sw2": "00",
  "responseData": "A1B2C3D4E5F6",
  "success": true
}
```

**响应 `400`**：
```json
{
  "error": "INVALID_PARAMETERS",
  "message": "apdu must be a hex string"
}
```

**响应 `404`**：
```json
{
  "error": "CARD_NOT_PRESENT",
  "message": "No card present in reader"
}
```

**响应 `408`**：
```json
{
  "error": "TIMEOUT",
  "message": "Card operation timed out"
}
```

---

## WebSocket 协议

WebSocket 是本服务的**核心实时通道**，支持双向消息通信和服务端主动推送。

### 连接

```
ws://localhost:5000/ws
```

支持 URL 参数订阅事件过滤：

```
ws://localhost:5000/ws?events=pcsc.card_status,pcsc.reader_arrival,printer.*
```

未指定 `events` 参数时，接收所有事件。

### 生命周期

```
客户端连接 → [心跳保持] ↔ [请求/响应] ← [服务端推送] → 客户端断开
```

### 心跳机制

| 方向 | 消息 | 说明 |
|------|------|------|
| Server → Client | `{"type":"ping"}` | 每 30 秒发送 |
| Client → Server | `{"type":"pong"}` | 客户端收到 ping 后应在 5 秒内回复 pong，否则服务端断开连接 |

### 请求/响应（Client → Server → Client）

客户端发送操作请求，服务端处理完成后返回对应响应。

#### 请求格式

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "target": "pcsc",
  "action": "transmit",
  "parameters": {
    "readerName": "OMNIKEY CardMan 5x21-CL 0",
    "apdu": "00A4040008A00102030405060700"
  }
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| requestId | string | 否 | 客户端请求标识，不传时服务端自动生成 UUID。响应中原样回显 |
| target | string | 是 | 操作目标，见下方 target 列表 |
| action | string | 是 | 操作名称，见下方 action 列表 |
| parameters | object | 否 | 操作参数，具体字段因 action 而异 |

**target 合法值**：

| target | 说明 |
|--------|------|
| `pcsc` | PCSC 读卡器操作 |
| `printer` | 证卡打印机操作（未来） |
| `idcard` | 身份证读卡器操作（未来） |
| `system` | 系统操作（如 ping） |

**action 枚举（pcsc target）**：

| action | 说明 | parameters 字段 | data 响应 |
|--------|------|-----------------|-----------|
| `list_readers` | 获取读卡器列表（含卡片状态） | 无 | `{ readers: [{ name, isCardPresent, atr }] }` |
| `get_reader_status` | 获取单个读卡器状态 | `readerName` (string) | `{ name, isCardPresent, atr }` |
| `get_atr` | 获取卡片 ATR | `readerName` (string) | `{ atr: "3B..." }` |
| `transmit` | 发送 APDU 指令 | `readerName` (string), `apdu` (string) | `{ sw1, sw2, responseData, success }` |

#### 响应格式

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "success": true,
  "data": {
    "sw1": "90",
    "sw2": "00",
    "responseData": "A1B2C3D4E5F6"
  },
  "timestamp": "2026-05-19T00:00:00.123Z"
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| requestId | string | 回显客户端传入的 requestId |
| success | bool | 操作是否成功 |
| data | object | 业务数据，因 action 而异 |
| timestamp | string | 服务端处理完成时间 |

#### 错误响应

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "success": false,
  "error": {
    "code": "CARD_NOT_PRESENT",
    "message": "读卡器中未检测到卡片"
  },
  "timestamp": "2026-05-19T00:00:00.123Z"
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| error.code | string | 错误码，见错误码表 |
| error.message | string | 人类可读的错误描述 |

### 服务端推送（Server → Client，主动事件）

当硬件状态变化时，服务端主动向所有连接（或匹配订阅的连接）推送事件消息。

```json
{
  "type": "event",
  "target": "pcsc",
  "event": "card_status_changed",
  "data": {
    "readerName": "OMNIKEY CardMan 5x21-CL 0",
    "oldStatus": "empty",
    "newStatus": "card_present"
  },
  "timestamp": "2026-05-19T00:00:00Z"
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| type | string | 固定为 `"event"`，用于区分响应和推送 |
| target | string | 事件来源硬件 |
| event | string | 事件名称 |
| data | object | 事件负载，因事件类型而异 |
| timestamp | string | 事件发生时间 |

**事件枚举**：

| target | event | data 字段 | 说明 |
|--------|-------|-----------|------|
| pcsc | `card_status_changed` | `readerName`, `oldStatus`, `newStatus` | 卡片插入或拔出 |
| pcsc | `reader_arrival` | `readerName` | 新读卡器接入 |
| pcsc | `reader_removal` | `readerName` | 读卡器移除 |
| printer | `job_completed` | `jobId`, `printerName` | 打印任务完成（未来） |
| printer | `job_error` | `jobId`, `printerName`, `errorMessage` | 打印任务失败（未来） |

---

## 错误码

| 错误码 | HTTP 状态 | WS 场景 | 说明 |
|--------|-----------|---------|------|
| `DRIVER_NOT_FOUND` | 404 | 支持 | 驱动不存在或未注册 |
| `INVALID_PARAMETERS` | 400 | 支持 | 请求参数错误或缺少必填字段 |
| `INVALID_ACTION` | 400 | 支持 | 操作的 action 不支持 |
| `CARD_NOT_PRESENT` | 404 | 支持 | 卡片未插入 |
| `READER_NOT_FOUND` | 404 | 支持 | 读卡器不存在 |
| `HARDWARE_ERROR` | 500 | 支持 | 硬件操作异常 |
| `TIMEOUT` | 408 | 支持 | 硬件操作超时 |

## 版本历史

- v1.0.3 (2026-05-21): 新增 `POST /api/config/reset` 重置配置接口；修正健康检查端点为 `/api/health`；新增 PCSC Mock 模式说明
- v1.0.2 (2026-05-21): 增加多语言支持说明，`Accept-Language` 请求头控制返回语言
- v1.0.1 (2026-05-20): 端口冲突自动检测与处理 + 配置模型增加 LogLevel + 修复 6 个 P0 bug
- v1.0.0 (2026-05-19): 初版，定义 REST 管理端点 + WebSocket 协议规范
