# DeviceHub — API 规范 (v1.5.0)

## 基础信息

- 端口：默认 `5000`，通过 `Server:HttpPort` 配置。启动时自动检测端口冲突，若默认端口被占用则尝试 `HttpPort+1` 至 `HttpPort+10`，均被占用则启动失败。安装程序会在默认端口被占用时提示用户指定新端口
- 协议：HTTP/1.1 + WebSocket
- 数据格式：JSON，UTF-8
- 鉴权：当前不实现，所有端点无鉴权
- CORS：允许所有来源（开发阶段），所有 REST 端点动态回显请求来源
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
| 驱动未注册 | `{Name} not registered` | `{Name} 未注册` |
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

### 模拟交通卡数据

| 功能 | 模拟值 |
|------|--------|
| 卡片信息 | 卡号 `1234567890123456`，发行商 `001`，卡类型 `01`，有效期 `202812` |
| 余额 | `5000`（分，即 50 元） |
| 交易记录 | 10 条，交替 DEBIT/CREDIT，金额递增 |
| 充值 init | 返回随机 sessionId，模拟 unsignedApdu 和 signData |
| 充值 execute | 始终成功，返回 SW=9000 |

### 模拟打印机数据

`Drivers:Printer:Mock=true` 时启用。

| 功能 | 模拟值 |
|------|--------|
| 打印机列表 | 2 台：`Mock Printer 1`（默认）和 `Mock Printer 2` |
| 打印文本 | 始终成功，日志记录内容 |
| 打印原始数据 | 始终成功，日志记录字节数 |

---

## 通用响应约定

### 成功响应

```json
{
  "success": true,
  "data": { ...业务数据... }
}
```

成功（无数据）：
```json
{
  "success": true,
  "data": null
}
```

### 错误响应

```json
{
  "success": false,
  "error": "ERROR_CODE",
  "message": "人类可读的错误描述"
}
```

### 通用错误码

| 错误码 | 说明 |
|--------|------|
| `DRIVER_NOT_FOUND` | 驱动不存在或未注册 |
| `SERVICE_NOT_RUNNING` | 服务未运行 |
| `CARD_NOT_PRESENT` | 卡片未插入或无法选择应用 |
| `READER_NOT_FOUND` | 读卡器不存在 |
| `INVALID_PARAMETERS` | 请求参数错误或缺少必填字段 |
| `HARDWARE_ERROR` | 硬件操作异常 |

### APDU 专用错误码

| 错误码 | 说明 |
|--------|------|
| `FILE_NOT_FOUND` | 文件未找到（SW=6A82/6A88） |
| `UNSUPPORTED_COMMAND` | 不支持的命令（SW=6D00/6E00） |
| `SECURITY_ERROR` | 安全状态不满足（SW=6982/6985/6988） |
| `INVALID_DATA` | 数据无效（SW=6A80/6984） |
| `CARD_FULL` | 卡片存储已满（SW=6A84） |

### WebSocket 响应格式

WebSocket 响应使用与 REST 相同的语义，增加 WS 特有的 `requestId` 和 `timestamp` 字段。

```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "success": true,
  "data": { ...业务数据... },
  "timestamp": "2026-07-22T12:00:00Z"
}
```

错误响应：
```json
{
  "requestId": "550e8400-e29b-41d4-a716-446655440000",
  "success": false,
  "error": {
    "code": "CARD_NOT_PRESENT",
    "message": "错误描述"
  },
  "timestamp": "2026-07-22T12:00:00Z"
}
```

### HTTP 状态码使用规范

| 状态码 | 含义 | 适用场景 |
|--------|------|---------|
| 200 | 成功 | GET、PUT、POST 操作成功 |
| 202 | 已接受 | 异步操作（如重启） |
| 400 | 参数错误 | 请求体校验失败 |
| 404 | 未找到 | 驱动不存在、卡片不在读卡器中 |
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
  "success": true,
  "data": {
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
  "success": true,
  "data": {
    "server": {
      "httpPort": 5000,
      "webSocketPath": "/ws"
    },
    "drivers": {
      "Pcsc": { "enabled": true, "autoReconnect": true },
      "Printer": { "enabled": true, "mock": false },
      "IdCard": { "enabled": false, "comPort": "COM3" }
    },
    "logging": {
      "ringBufferSize": 1000
    },
    "configVersion": 1
  }
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
  "success": false,
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
  "success": false,
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
  "success": true,
  "data": {
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
{
  "success": true,
  "data": [
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
  "success": true,
  "data": {
    "name": "Pcsc",
    "status": "Running",
    "enabled": true
  }
}
```

**响应 `404`**：
```json
{
  "success": false,
  "error": "DRIVER_NOT_FOUND",
  "message": "Driver Pcsc not found"
}
```

#### POST `/api/drivers/{name}/disable`

禁用指定驱动。写配置 + 关闭硬件。

**响应 `200`**：
```json
{
  "success": true,
  "data": {
    "name": "Pcsc",
    "status": "Stopped",
    "enabled": false
  }
}
```

---

### 5. `/api/service/restart` — 重启服务

**POST** `/api/service/restart`

触发服务重启。服务端延迟 1 秒后退出进程，由系统服务管理器（Windows Service / systemd）自动拉起。

**响应 `202`**：
```json
{
  "success": true,
  "data": {
    "message": "Service will restart in 1 second"
  }
}
```

> 注意：调用此接口后 TCP 连接随即断开，客户端应做好重试准备。

---

### 6. `/api/health` — 健康检查

**GET** `/api/health`

给 Docker 容器编排 / systemd 健康检查使用的轻量端点。

**响应 `200`**：
```json
{
  "success": true,
  "data": {
    "status": "healthy",
    "timestamp": "2026-05-19T08:08:37Z"
  }
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
  "success": true,
  "data": {
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
}
```

**响应 `503`**（驱动未注册）：
```json
{
  "success": false,
  "error": "DRIVER_NOT_FOUND",
  "message": "PCSC driver is not registered"
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
  "success": true,
  "data": {
    "name": "OMNIKEY CardMan 5x21-CL 0",
    "isCardPresent": true,
    "atr": "3B8F8001804F0CA0000003060300030000000068"
  }
}
```

**响应 `404`**：
```json
{
  "success": false,
  "error": "READER_NOT_FOUND",
  "message": "Reader not found: OMNIKEY CardMan 5x21-CL 0"
}
```

#### GET `/api/hardware/pcsc/readers/{name}/atr`

获取指定读卡器中卡片的 ATR（Answer to Reset）。

**响应 `200`**：
```json
{
  "success": true,
  "data": {
    "atr": "3B8F8001804F0CA0000003060300030000000068"
  }
}
```

**响应 `404`**（卡片未插入或读卡器不存在）：
```json
{
  "success": false,
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
  "success": true,
  "data": {
    "sw1": "90",
    "sw2": "00",
    "responseData": "A1B2C3D4E5F6"
  }
}
```

**响应 `400`**：
```json
{
  "success": false,
  "error": "INVALID_PARAMETERS",
  "message": "apdu must be a hex string"
}
```

**响应 `404`**：
```json
{
  "success": false,
  "error": "CARD_NOT_PRESENT",
  "message": "No card present in reader"
}
```

**响应 `408`**：
```json
{
  "success": false,
  "error": "TIMEOUT",
  "message": "Card operation timed out"
}
```

---

### 8. 交通卡操作端点（REST）

命名规则：`/api/hardware/transitcard/...`。提供 JT/T 978 互联互通卡的高层业务接口，基于 PCSC APDU 层封装。

#### GET `/api/hardware/transitcard/readers`

返回当前有卡的读卡器列表（可用于交通卡操作的读卡器）。

**响应 `200`**：
```json
{
  "success": true,
  "data": {
    "readers": ["Mock Reader CL"]
  }
}
```

**响应 `503`**：
```json
{
  "success": false,
  "error": "DRIVER_NOT_FOUND",
  "message": "Transit card service is not registered"
}
```

#### GET `/api/hardware/transitcard/info?readerName=xxx`

读取交通卡基本信息（卡号等）。

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| readerName | string | 否 | 读卡器名称，不传自动选择有卡的读卡器 |

**响应 `200`**：
```json
{
  "success": true,
  "data": {
    "cardNumber": "1234567890123456",
    "issuerCode": "001",
    "cardType": "01",
    "expiryDate": "202812",
    "otherData": ["Mock data"]
  }
}
```

**响应 `404`**：
```json
{
  "success": false,
  "error": "CARD_NOT_PRESENT",
  "message": "No card present in any reader"
}
```

#### GET `/api/hardware/transitcard/balance?readerName=xxx`

读取交通卡余额。

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| readerName | string | 否 | 读卡器名称，不传自动选择 |

**响应 `200`**：
```json
{
  "success": true,
  "data": {
    "balance": 5000,
    "currency": "CNY"
  }
}
```

`balance` 单位为分（1 元 = 100 分）。

#### GET `/api/hardware/transitcard/transactions?count=10&readerName=xxx`

读取交通卡最近交易记录。

**查询参数**：

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| count | int | 10 | 返回条数，最大 50 |
| readerName | string | 自动 | 读卡器名称 |

**响应 `200`**：
```json
{
  "success": true,
  "data": {
    "records": [
      {
        "type": "DEBIT",
        "amount": 200,
        "timestamp": "2026-05-21T12:00:00Z",
        "location": "Station_0"
      },
      {
        "type": "CREDIT",
        "amount": 500,
        "timestamp": "2026-05-20T12:00:00Z",
        "location": "Station_1"
      }
    ]
  }
}
```

#### POST `/api/hardware/transitcard/recharge/init`

充值初始化——生成用于外部队签名的数据。

**请求体**：
```json
{
  "amount": 5000,
  "readerName": "Mock Reader CL"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| amount | number | 是 | 充值金额（分） |
| readerName | string | 否 | 读卡器名称 |

**响应 `200`**：
```json
{
  "success": true,
  "data": {
    "sessionId": "a1b2c3d4e5f6...",
    "unsignedApdu": "8054000008000013880000000000000000",
    "signData": "a1b2c3d4e5f6...00001388"
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| sessionId | string | 会话标识，execute 阶段需要传入 |
| unsignedApdu | string | 不含 MAC 的充值 APDU，外部 HSM 签名后得到 macSignature |
| signData | string | 待签名数据，由外部加密机/H S M 签名 |

**响应 `400`**：
```json
{
  "success": false,
  "error": "INVALID_PARAMETERS",
  "message": "Recharge amount must be greater than 0"
}
```

#### POST `/api/hardware/transitcard/recharge/execute`

充值执行——传入签名后的 MAC 完成充值。

**请求体**：
```json
{
  "sessionId": "a1b2c3d4e5f6...",
  "macSignature": "AABBCCDDEEFF0011..."
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| sessionId | string | 是 | init 阶段返回的 sessionId |
| macSignature | string | 是 | 外部 HSM 对 signData 的签名结果 |

**响应 `200`**：
```json
{
  "success": true,
  "data": null
}
```

**响应 `500`**：
```json
{
  "success": false,
  "error": "HARDWARE_ERROR",
  "message": "Session not found or expired"
}
```

---

### 9. 打印机操作端点（REST）

命名规则：`/api/hardware/printer/...`。提供系统打印机枚举、文本打印和原始数据打印功能。基于 `System.Drawing.Printing`（Windows）或 `lp` 命令（Linux）实现。

#### GET `/api/hardware/printer/printers`

获取所有系统已安装打印机列表。

**响应 `200`**：
```json
{
  "success": true,
  "data": {
    "printers": [
      {
        "name": "Microsoft Print to PDF",
        "status": "ready",
        "isDefault": true,
        "description": null,
        "location": null
      },
      {
        "name": "Brother QL-820NWB",
        "status": "ready",
        "isDefault": false,
        "description": null,
        "location": null
      }
    ]
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| name | string | 打印机名称 |
| status | string | `ready` / `error` / `busy` |
| isDefault | bool | 是否为系统默认打印机 |
| description | string? | 打印机描述（当前未实现） |
| location | string? | 打印机位置（当前未实现） |

**响应 `503`**：
```json
{
  "success": false,
  "error": "DRIVER_NOT_FOUND",
  "message": "Printer service not available"
}
```

#### POST `/api/hardware/printer/print`

打印文本。在 Windows 上使用 `PrintDocument` 以 Microsoft YaHei 10pt 字体渲染后打印；在 Linux 上使用 `lp` 命令输出。

**请求体**：
```json
{
  "text": "Hello, DeviceHub!",
  "printerName": "Microsoft Print to PDF"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| text | string | 是 | 要打印的文本内容 |
| printerName | string | 否 | 打印机名称，不传使用系统默认打印机 |

**响应 `200`**：
```json
{
  "success": true,
  "data": null
}
```

**错误响应**：

| 状态码 | 场景 |
|--------|------|
| 400 | text 为空 |
| 500 | 打印失败 |
| 503 | 驱动未注册 |

#### POST `/api/hardware/printer/print-raw`

打印原始字节流，不经过打印驱动渲染。用于 ESC/POS、ZPL、CPCL 等打印机指令集。

**请求体**：
```json
{
  "data": "1B405B010A48656C6C6F0A1B40",
  "printerName": "Brother QL-820NWB"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| data | string | 是 | 十六进制编码的原始打印数据 |
| printerName | string | 否 | 打印机名称，不传使用系统默认打印机 |

**响应 `200`**：
```json
{
  "success": true,
  "data": null
}
```

**错误响应**：

| 状态码 | 场景 |
|--------|------|
| 400 | data 为空或非合法 hex 字符串 |
| 500 | 打印失败 |
| 503 | 驱动未注册 |

---

### 10. `/api/config-store` — 自定义配置存储

基于 JSON 文件的 Key-Value 持久化存储，供用户自由存取自定义配置项。文件位于 `{ContentRoot}/data/config.json`，服务启动时自动创建。

#### GET `/api/config-store`

列出所有配置条目。

**响应 200:**

```json
{
  "success": true,
  "data": {
    "entries": [
      { "key": "setting1", "value": "value1" },
      { "key": "setting2", "value": "value2" }
    ]
  }
}
```

---

#### GET `/api/config-store/{key}`

获取指定键的值。

**响应 200:**

```json
{
  "success": true,
  "data": {
    "key": "setting1",
    "value": "value1"
  }
}
```

**响应 404:**

```json
{ "success": false, "error": "NOT_FOUND", "message": "Key 'xxx' not found" }
```

---

#### PUT `/api/config-store/{key}`

设置指定键的值（新增或覆盖）。

**请求体:**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| value | string | 是 | 要存储的值 |

```json
{
  "value": "my-config-value"
}
```

**响应 200:**

```json
{
  "success": true,
  "data": {
    "key": "setting1",
    "value": "my-config-value"
  }
}
```

---

#### DELETE `/api/config-store/{key}`

删除指定键的配置项。

**响应 200:**

```json
{ "success": true, "data": null }
```

**响应 404:**

```json
{ "success": false, "error": "NOT_FOUND", "message": "Key 'xxx' not found" }
```

---

#### DELETE `/api/config-store`

清空所有配置条目。

**响应 200:**

```json
{ "success": true, "data": null }
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
| `pcsc` | PCSC 读卡器操作（APDU 级别） |
| `transitcard` | 交通卡操作（JT/T 978 高层业务） |
| `printer` | 打印机操作 |
| `id-card` | 身份证读卡器操作 |
| `kacyber-go-card` | KaCyberGo 交通卡操作（插件驱动，通过 `IHardwareWebSocketHandler` 注册） |

**action 枚举（pcsc target）**：

| action | 说明 | parameters 字段 | data 响应 |
|--------|------|-----------------|-----------|
| `list_readers` | 获取读卡器列表（含卡片状态） | 无 | `{ readers: [{ name, isCardPresent, atr }] }` |
| `get_reader_status` | 获取单个读卡器状态 | `readerName` (string) | `{ name, isCardPresent, atr }` |
| `get_atr` | 获取卡片 ATR | `readerName` (string) | `{ atr: "3B..." }` |
| `transmit` | 发送 APDU 指令 | `readerName` (string), `apdu` (string) | `{ sw1, sw2, responseData }` |

**action 枚举（transitcard target）**：

| action | 说明 | parameters 字段 | data 响应 |
|--------|------|-----------------|-----------|
| `read_card_info` | 读取交通卡信息 | `readerName` (string 可选) | `{ cardNumber, issuerCode, cardType, expiryDate }` |
| `read_balance` | 读取交通卡余额 | `readerName` (string 可选) | `{ balance, currency }` |
| `read_transactions` | 读取交易记录 | `count` (int 可选, 默认10), `readerName` (string 可选) | `{ records: [...] }` |
| `recharge_init` | 充值初始化 | `amount` (number), `readerName` (string 可选) | `{ sessionId, unsignedApdu, signData }` |
| `recharge_execute` | 充值执行 | `sessionId` (string), `macSignature` (string) | null |

**action 枚举（printer target）**：

| action | 说明 | parameters 字段 | data 响应 |
|--------|------|-----------------|-----------|
| `list` | 获取打印机列表 | 无 | `{ printers: [...] }` |
| `print` | 打印文本 | `text` (string), `printerName` (string 可选) | null |
| `print_raw` | 打印原始数据 | `data` (string hex), `printerName` (string 可选) | null |

**action 枚举（id-card target）**：

| action | 说明 | parameters 字段 | data 响应 |
|--------|------|-----------------|-----------|
| `readers` | 获取身份证读卡器列表 | 无 | `{ readers: [string] }` |
| `read` | 读取身份证信息 | `readerName` (string 可选) | `{ name, idNumber, address, issuingAuthority, validityStart, validityEnd }` |
| `read_photo` | 读取身份证照片 | `readerName` (string 可选) | `{ photo (base64), format: "image/jpeg" }` |

**action 枚举（kacyber-go-card target）**：

插件驱动 `kacyber-go-card` 通过 `IHardwareWebSocketHandler` 接口注册，action 枚举详见 KaCyberGoCard 私有仓库文档。主要操作包括：

| action | 说明 |
|--------|------|
| `readers` | 获取有卡读卡器列表 |
| `info` / `cardholder` / `management_info` / `full_info` | 卡片信息读取 |
| `balance` / `transactions` / `monthly_ticket` / `transit_process_records` | 余额与交易记录 |
| `atr` / `random` / `verify_pin` / `select` | 安全与选择 |
| `recharge_init` / `recharge_execute` | 充值两步流程 |
| `update_binary` / `update_binary_init` / `update_binary_execute` | 二进制更新三步流程 |
| `update_card_info_init` / `update_cardholder_info_init` / `update_management_info_init` | 便捷更新 |

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
    "message": "No card present in the reader"
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
| pcsc | `reader_arrival` | `readerName`, `status` | 新读卡器接入 |
| pcsc | `reader_removal` | `readerName`, `status` | 读卡器移除 |
| id-card | `card_inserted` | `readerName`, `oldStatus`, `newStatus` | 身份证插入 |
| id-card | `card_removed` | `readerName`, `oldStatus`, `newStatus` | 身份证拔出 |
| printer | `job_completed` | `jobId`, `printerName`, `status` | 打印任务完成 |
| printer | `job_error` | `jobId`, `printerName`, `status`, `errorMessage` | 打印任务失败 |

插件驱动（如 `kacyber-go-card`）通过 `IHardwareWebSocketHandler` 注册后，其操作通过 WS 暴露（见各插件文档）。插件本身的事件推送遵循各插件的 target 命名。

### 推荐用法

**操作走 REST，事件走 WS** 是推荐的成熟架构。两个通道职责分离、状态独立：

```
客户端状态：
  ┌─────────────────────────────────┐
  │  HTTP 请求（REST）              │ ← 每次调用独立，无状态
  │  GET /api/hardware/pcsc/readers  │
  │  POST /api/hardware/printer/print│
  └─────────────────────────────────┘
  
  ┌─────────────────────────────────┐
  │  WS 长连接（仅接收事件）        │ ← 连上后只收不发
  │  ws://host:port/ws?events=pcsc.*│
  │  → 收到 card_status_changed     │
  │  → 收到 reader_arrival          │
  └─────────────────────────────────┘
```

典型客户端实现（约 30 行）：

```ts
// 1. REST — 直接 fetch
const cards = await fetch('/api/hardware/pcsc/readers').then(r => r.json())

// 2. WS — 只收事件
const ws = new WebSocket('ws://localhost:5000/ws?events=pcsc.card_status')
ws.onmessage = (e) => {
  const msg = JSON.parse(e.data)
  if (msg.type === 'event') {
    // 卡片插拔 → 刷新 UI 或重新读取卡片信息
  }
}
```

| 端点 / 操作 | 推荐协议 | 原因 |
|-------------|----------|------|
| **管理类** | | |
| `GET /api/status`, `/api/config`, `/api/logs` | REST | curl / Swagger / CLI 可直接调试 |
| `PUT /api/config` | REST | 配置修改天然无状态 |
| `POST /api/config/reset` | REST | 一次性的管理操作 |
| `GET /api/drivers`, 驱动 enable/disable | REST | 状态查询与变更 |
| `GET /api/health` | REST | 健康检查通常由负载均衡器触发 |
| **PCSC 硬件操作** | | |
| `GET /api/hardware/pcsc/readers` | REST | 查询操作，无状态幂等 |
| `GET /api/hardware/pcsc/atr` | REST | 同 |
| `POST /api/hardware/pcsc/transmit` | REST | 同 |
| **交通卡 (TransitCard) 操作** | | |
| `GET info`, `GET balance`, `GET transactions` | REST | 查询操作，无状态幂等 |
| `POST recharge/init`, `POST recharge/execute` | REST | 充值流程不依赖 WS 有状态 |
| **打印机操作** | | |
| `GET /api/hardware/printer/printers` | REST | 查询操作 |
| `POST /api/hardware/printer/print` | REST | 打印发送不需要保持连接 |
| `POST /api/hardware/printer/print-raw` | REST | 同 |
| **身份证读卡器操作** | | |
| `POST /api/hardware/id-card/read` | REST | 读证一次调用即完成 |
| `POST /api/hardware/id-card/read-photo` | REST | 返回 base64 大照片数据 |
| **WS 事件推送** | | |
| `card_status_changed`（卡片插拔） | WS 仅接收 | 服务端主动推送，无需轮询 |
| `reader_arrival` / `reader_removal` | WS 仅接收 | 被动事件，无 REST 对应 |
| `job_completed` / `job_error`（打印） | WS 仅接收 | 异步完成通知 |
| **WS 自定义请求** | | |
| 通过 WS 发送 target/action/parameters | REST 或 WS 均可 | 功能等价，统一走 WS 可省一条 HTTP 连接 |
| 实时监控面板（混合模式） | REST + WS | 操作走 fetch，事件走 onmessage |

WS 的核心不可替代价值在于**所有事件推送**，其余场景 REST 更简洁通用。两个通道彼此独立——WS 断线不影响 REST 调用。

如果业务场景全部在浏览器 SPA 内完成且对网络稳定性有自信，也可统一走 WS 获得单通道的简洁性。所有 REST 操作在 WS 上都有对应的 action，功能完全等价，按需选择即可。

---

## 错误码

| 错误码 | HTTP 状态 | WS 场景 | 说明 |
|--------|-----------|---------|------|
| `DRIVER_NOT_FOUND` | 404 | 支持 | 驱动不存在或未注册 |
| `SERVICE_NOT_RUNNING` | 503 | 支持 | 服务未运行 |
| `INVALID_PARAMETERS` | 400 | 支持 | 请求参数错误或缺少必填字段 |
| `INVALID_ACTION` | 400 | 支持 | 操作的 action 不支持 |
| `CARD_NOT_PRESENT` | 404 | 支持 | 卡片未插入或无法选择交通卡应用 |
| `READER_NOT_FOUND` | 404 | 支持 | 读卡器不存在 |
| `HARDWARE_ERROR` | 500 | 支持 | 硬件操作异常 |
| `TIMEOUT` | 408 | 支持 | 硬件操作超时 |
| `FILE_NOT_FOUND` | 500 | 支持 | 文件未找到（SW=6A82/6A88） |
| `UNSUPPORTED_COMMAND` | 500 | 支持 | 不支持的命令（SW=6D00/6E00） |
| `SECURITY_ERROR` | 500 | 支持 | 安全状态不满足（SW=6982/6985/6988） |
| `INVALID_DATA` | 500 | 支持 | 数据无效（SW=6A80/6984） |
| `CARD_FULL` | 500 | 支持 | 卡片存储已满（SW=6A84） |

## 版本历史

| 版本 | 日期 | 变更内容 |
|------|------|----------|
| v1.6.0 | 2026-07-23 | 实现全部 WS 事件：pcsc（card_status_changed/reader_arrival/reader_removal），id-card（card_inserted/card_removed），printer（job_completed/job_error）；重构 MockIdCardService 支持事件轮询；新增 PrinterJobEventArgs 模型 |
| v1.5.0 | 2026-07-22 | 统一所有端点为 ApiResponse 格式（`{success, data}` / `{success, error, message}`）；WebSocket 响应使用 `WsResponseHelper`；Service restart 端点改为 202 Accepted 统一格式；ConfigStore 端点移除 data 内冗余 `success` 字段；Drivers 端点 catch 改为返回 ApiResponse 错误而非 throw |
| v1.4.1 | 2026-07-16 | 统一错误消息格式；移除不存在的 id-card WS 事件（card_inserted/card_removed） |
| v1.4.0 | 2026-06-24 | 新增 config-store 自定义配置存储（JSON 文件），提供 `/api/config-store` CRUD 端点，文件位于 `{ContentRoot}/data/config.json` |
| v1.3.0 | 2026-06-24 | 新增 id-card 身份证 WS action 枚举；新增 kacyber-go-card 插件 target 说明；修正 printer WS action 名称（list_printers→list, print_text→print）；新增 id-card 事件定义；target 表 idcard 修正为 id-card；新增 WS 插件化扩展机制说明；新增"推荐用法"章节（ASCII 架构图 + 客户端示例代码 + 场景对照表） |
| v1.2.0 | 2026-06-24 | 新增打印机（Printer）REST 端点 + WS action 枚举，扩展 target 支持 printer |
| v1.1.0 | 2026-05-22 | 新增交通卡（TransitCard）REST 端点 + WS action 枚举，扩展 target 支持 transitcard |
| v1.0.3 | 2026-05-21 | 新增 `POST /api/config/reset` 重置配置接口；修正健康检查端点为 `/api/health`；新增 PCSC Mock 模式说明 |
| v1.0.2 | 2026-05-21 | 增加多语言支持说明，`Accept-Language` 请求头控制返回语言 |
| v1.0.1 | 2026-05-20 | 端口冲突自动检测与处理 + 配置模型增加 LogLevel + 修复 6 个 P0 bug |
| v1.0.0 | 2026-05-19 | 初版，定义 REST 管理端点 + WebSocket 协议规范 |
