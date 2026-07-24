# DeviceHub — AGENTS.md

## 项目信息
- 六个项目：`DeviceHub.Devices.Contracts`（抽象/接口 + NuGet 发布）→ `DeviceHub.Devices.PcscReader`（PCSC 实现）、`DeviceHub.Devices.Printer`（系统打印）、`DeviceHub.Devices.IdCard`（身份证阅读）、`DeviceHub.Cards.TransitCard`（互联互通卡 JT/T 978 协议封装）→ `DeviceHub.DriverLoader`（外部 DLL 插件加载）→ `DeviceHub.Service.Api`（主程序）
- 命名体系：`DeviceHub.Devices.*` 硬件抽象，`DeviceHub.Cards.*` 卡协议封装
- 目标框架 **net10.0**，Minimal APIs 项目
- 三层架构：Minimal APIs → Service → Hardware，**不做 DDD / CQRS / MediatR**
- 每种硬件独立接口（如 `IPcscService`），继承轻量基接口 `IHardwareService`（仅生命周期）
- `ITransitCardService` 为纯服务层（非 `IHardwareService`），基于 `IPcscService` 实现高层协议封装
- 通过 `AddXxxService(IServiceCollection, IConfiguration)` 扩展方法条件注入
- 闭源驱动通过 `DriverLoader` 扫描 `drivers/` 目录加载，实现 `IHardwareService` + `[DriverName]` 属性
- **无持久化数据库**：日志为内存环形缓冲区，配置为 JSON 文件读写 + Reload
- 配置模型充血（`Validate()` + `Merge()`），硬件操作 DTO 使用贫血 record
- 端口动态分配：启动时自动检测冲突，若 `Server:HttpPort` 被占用则尝试 +1 至 +10

## 协议分工
| 场景 | 协议 |
|---|---|
| 配置管理、状态查询、日志、驱动启停、健康检查 | REST |
| 发送 APDU、读取身份证、交通卡操作 | REST 或 WS，均可 |
| 卡片插拔、硬件事件推送 | 仅 WS |

- WebSocket 做到**有状态的通道**：心跳保活（30s ping / 5s pong 超时）、事件订阅推送、requestId 回显匹配
- REST 侧重**管理**，也备选支持硬件操作，供 CLI 或非 WS 场景使用

## 部署形态
- **Windows**: `UseWindowsService()` + Inno Setup 打包（自动启用所有驱动，仅配置端口）
- **Linux**: `UseSystemd()` + systemd service 模板
- 发布方式：`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
- CI/CD：`.github/workflows/release.yml`，release:published 或 workflow_dispatch 触发
- 部署目录：`deploy/windows/`（devicehub.iss, publish.ps1, ChineseSimplified.isl）+ `deploy/linux/`（devicehub.service, install.sh, publish.sh）

## 关键实现细节
- `Program.cs` 使用 `CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory })` — 确保 Windows Service 模式下（工作目录为 System32）能正确加载 appsettings.json
- `InMemoryLogProvider` 尊重 `Logging:LogLevel` 配置，按 category 匹配最小日志级别
- `ConfigEndpoints.BindConfig` 动态从 `Drivers` 配置段读取所有驱动 key，不硬编码
- `PcscService` 使用 `_syncLock` 保护 `_context` 字段，所有硬件操作在锁内执行
- `PcscService.Dispose()` 调用 `ShutdownAsync().GetAwaiter().GetResult()` 统一清理路径
- `WebSocketHandler` 为注入单例（非静态类），通过 DI 注册，`PingService` 构造函数注入
- `WebSocketHandler.SendEventAsync` 使用 `SemaphoreSlim(5,5)` 控制背压
- `WebSocketHandler` 支持 `?events=` 参数订阅过滤，未订阅的事件不推送
- `DriverRegistry` enable/disable 操作持久化到 `appsettings.json`，重启不丢失
- `PcscService` 支持 Mock 模式（`Drivers:Pcsc:Mock=true`），`MockPcscService` 模拟 2 个读卡器、卡片插拔事件、固定 ATR 和 Transmit 响应；`MockTransitCardService` 模拟交通卡读卡/余额/充值
- `TransitCardService` 自动跟随 Pcsc 启用状态注册（无独立配置开关），所有操作支持指定 `readerName` 或自动选择有卡读卡器
- 充值两步流程：init 返回 unsignedApdu + signData 供外部 HSM 签名，execute 接收 macSignature 完成
- `DeviceHub.Devices.Contracts` 以 MIT 许可发布到 nuget.org，供闭源驱动引用
- 闭源驱动通过 `DriverLoader.LoadExternalDrivers()` 扫描 `drivers/*.dll` 加载，需实现 `IHardwareService` + `[DriverName]` 属性
- 闭源驱动可继承 `HardwareDriverBase`（定义在 Contracts），获得自描述配置加载能力：约定在 DLL 同级放置 `{DriverName}.json`，通过 `LoadConfig<T>()` 读取自身配置，无需侵入 DeviceHub 主配置体系
- 安装程序 `InitializeSetup` 先 `net stop` + `sc delete` 旧服务，确保新服务读取新配置
- 安装包多语言：Windows Inno Setup 双语（English/中文简体），Linux install.sh 启动时选择语言
- `GeneratePackageOnBuild` 在 Contracts.csproj 中启用，每次构建产出 nupkg

## 源码位置
- 架构设计：`doc/01-architecture-v1.1.0.md`（最新版）
- API 规范：`doc/02-api-specification-v1.3.0.md`（最新版）
- 部署打包：`doc/03-packaging-v1.1.0.md`（最新版）
- 跨平台/国产化：`doc/04-cross-platform-v1.0.0.md`
- 测试指南：`doc/05-testing-v1.1.0.md`（最新版）
- Vue demo：`demo/`（README 见 `demo/README.md`）

## 项目目录结构
- 接口定义（`IXxxService`）位于项目根目录
- 服务实现位于 `Services/`（真实服务 + Mock 服务）
- 端点映射位于 `Endpoints/`（命名为 `*Endpoint.cs`，内部调用同名 `MapEndpoints`)
- 请求 DTO 位于 `Models/Requests/`
- 响应 DTO 位于 `Models/Responses/`
- DI 注册位于 `Extensions/`（`ServiceExtensions.cs`，命名 `AddXxxService`）
- 工具类位于 `Helpers/`（如 `ApduBuilder`）
- 常量位于 `Constants/`（如 `SwConstants`）
- 内置设备项目（`DeviceHub.Devices.*`）均实现 `IHardwareEndpointRegistrar`，端点由 `Program.cs` 的 `IHardwareEndpointRegistrar` 循环自动注册

## Contracts 模型分类
`DeviceHub.Devices.Contracts/Models/` 按语义分三个层级：

| 目录 | 内容 | 示例 |
|------|------|------|
| `Models/` 根 | 操作返回结果、事件参数 | `TransmitResult`, `BalanceInfo`, `TransactionRecord`, `RechargeResult`, `CardStatusEventArgs` |
| `Models/Hardware/` | 硬件实体描述 | `CardInfo`, `ReaderInfo`, `PrinterInfo`, `IdCardInfo` |
| `Models/Config/` | 系统配置模型 | `AppConfig`, `DriverInfo`, `LogEntry`, `ServiceInfo` |

## 代码规范
- 命名空间 `DeviceHub.*`；异步方法 `Async` 后缀（如 `TransmitAsync`）
- 日志：`ILogger<T>` 记录启动/停止/错误/硬件事件
- 注释语言：中文
- 禁止 Unicode 装饰符号（× ≥ → ¥ 等语义符号不受限）
- 一次性交干净代码，不留明显可预见的缺陷

## REST 响应格式
所有硬件及系统 API 端点统一使用 `ApiResponse<T>` 格式：

| 场景 | 响应结构 |
|------|----------|
| 成功 | `{ "success": true, "data": { ...业务数据... } }` |
| 成功（无数据） | `{ "success": true, "data": null }` |
| 参数错误 | `{ "success": false, "error": "INVALID_PARAMETERS", "message": "错误描述" }` |
| 资源未找到 | `{ "success": false, "error": "CARD_NOT_FOUND", "message": "错误描述" }` |
| 硬件错误 | `{ "success": false, "error": "SECURITY_ERROR", "message": "失败描述 (SW=6988)" }` |

- 端点必须通过 `ApiResponseHelper.Ok()` / `.BadRequest()` / `.Error()` / `.NotFound()` 返回，禁止直接返回 `Results.Ok` / `Results.Json` / 匿名对象
- APDU 执行失败使用 `SwCodeHelper.ClassifySw(sw1, sw2)` 自动映射错误码，SW 码始终包含在 `message` 中
- execute 端点成功时返回 `data: null`，不返回 `sw1`/`sw2`（成功即隐含 SW=9000）
- 二进制文件下载（如 IdCard 照片）可通过 `Results.File` 返回，属于合理例外

## WebSocket 响应格式
WS 响应与 REST 共用相同的 `{ success, data, error }` 语义，增加 WS 特有的 `requestId`（请求关联）和 `timestamp`（ISO 8601）：

| 场景 | 响应结构 |
|------|----------|
| 成功 | `{ "requestId": "...", "success": true, "data": { ... }, "timestamp": "2026-07-22T12:00:00Z" }` |
| 失败 | `{ "requestId": "...", "success": false, "error": { "code": "CARD_NOT_PRESENT", "message": "..." }, "timestamp": "..." }` |

- WS handler 必须通过 `WsResponseHelper.Ok()` / `WsResponseHelper.Error()` 构造响应，禁止内联 `new { ... }`
- 错误码与 REST 共用同一定义表（CARD_NOT_PRESENT、SECURITY_ERROR、INVALID_PARAMETERS 等）
- 执行类 action 成功时 `data` 返回 `null`（与 REST execute 端点一致），不应在 `data` 内嵌套 `{ success: true }`
- `SendResponse`/`SendError` 方法内部调用 `WsResponseHelper`，统一序列化发送

## WebSocket 协议
- 所有硬件操作均可通过 WS 完成（REST 功能完整超集），外加实时事件推送
- 内置 target：`pcsc`、`transitcard`、`printer`、`id-card`
- 插件 target：通过 `IHardwareWebSocketHandler` 接口注册（如 `kacyber-go-card`）
- action 命名约定：下划线分隔小写，如 `read_card_info`、`update_binary_init`
- 结果中涉及二进制数据用 base64 编码（如身份证照片 `photo` 字段）
- 新增插件驱动时，同时实现 `IHardwareWebSocketHandler` 并在 `ServiceExtensions` 中注册

## 实施流程
1. 阅读相关设计文档（`doc/` 下最新版）
2. 输出修改方案（文件 → 改动点 → 预期结果）
3. **等待用户确认后才实施**（流程：方案 → 确认 → 实施）
4. 不自动执行 git add/commit/push，先询问
5. 不安装任何软件，不修改操作系统配置
6. 不执行超出用户明确要求的额外改动（包括但不限于：代码重构、文档更新、配置修改、依赖升级）
7. 涉及基础设施、打包、部署、CI/CD 的改动，即使看似必要，也必须先提出方案，等待确认
8. **代码改动后必须同步更新 demo 和文档**：每次 API 新增或变更，需同步更新后端端点、demo（按钮、结果展示、输入字段）和文档（端点、参数、响应示例）

## 文档版本规则
- 文档命名：`NN-title-vX.Y.Z.md`，每次更新创建新版本文件，不修改旧版
- **语义化版本**：
  - `Z` 递增（v1.0.x）：bug 修复、小修正
  - `Y` 递增（v1.x.0）：功能更新、新增内容
  - `X` 递增（x.0.0）：架构级重大变更
- AGENTS.md 除外，始终为 `AGENTS.md` 不版本化
- 每个文档末尾保留版本历史表

## 已知待完成优化（P1/P2）
- 无（P1-7/P1-8/P1-11/P2 均已完成）
