# DeviceHub Demo — 接口测试工具

基于 Vue 3 + TypeScript + Vite 的 DeviceHub API 测试前端，用于调试和验证后端接口。

## 快速开始

```bash
# 1. 安装依赖
npm install

# 2. 确保后端服务已启动（默认 localhost:5000）

# 3. 启动开发服务器
npm run dev
```

浏览器访问提示的本地地址（默认 `http://localhost:5173`）。

## 前提条件

- 后端 DeviceHub 服务已在运行
- 开发服务器通过 Vite proxy 将 `/api` 和 `/ws` 转发到 `http://localhost:5000`
- 无需额外配置，开箱即用

## 功能面板

| 标签 | 说明 |
|------|------|
| STATUS | `GET /api/status` — 服务状态、版本、驱动列表 |
| CONFIG | `GET/PUT /api/config` — 读取/更新运行时配置 |
| LOGS | `GET /api/logs` — 查询内存日志，支持 level/tail/query 过滤 |
| DRIVERS | `GET /api/drivers` + 启用/禁用驱动 |
| SERVICE | `POST /api/service/restart` — 服务重启 |
| HEALTH | `GET /health` — 健康检查 |
| PCSC | PCSC 读卡器操作：列出读卡器、获取信息、ATR、APDU 透传 |
| WS | WebSocket 连接测试：连接/断开、发送消息、查看事件推送 |

## 使用提示

- **CONFIG 面板**：点 GET 后自动填充到编辑框，修改后点 PUT 提交
- **DRIVERS 面板**：点 GET 后显示驱动卡片，可逐一启用/禁用
- **PCSC 面板**：先点「列出读卡器」获取名称，再填入读卡器名称执行后续操作
- **WS 面板**：连接后发送 `target` + `action` 消息，下方实时显示收到的推送

## 构建

```bash
npm run build
```

产物输出到 `dist/`，可部署到任意静态服务器。
