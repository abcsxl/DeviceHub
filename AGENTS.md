# DeviceHub — AGENTS.md

## 项目信息
- 三个项目：`DeviceHub.Devices.Contracts`（抽象）→ `DeviceHub.Devices.PcscReader`（实现）→ `DeviceHub.Service.Api`（主程序）
- 目标框架 **net10.0**，Minimal APIs 项目
- 三层架构：Minimal APIs → Service → Hardware，**不做 DDD / CQRS / MediatR**
- 每种硬件独立接口（如 `IPcscService`），继承轻量基接口 `IHardwareService`（仅生命周期）
- 通过 `AddXxxService(IServiceCollection, IConfiguration)` 扩展方法条件注入
- **无持久化数据库**：日志为内存环形缓冲区，配置为 JSON 文件读写 + Reload
- 配置模型充血（`Validate()` + `Merge()`），硬件操作 DTO 使用贫血 record
- 端口动态分配：启动时自动检测冲突，若 `Server:HttpPort` 被占用则尝试 +1 至 +10

## 协议分工
| 场景 | 协议 |
|---|---|
| 配置管理、状态查询、日志、驱动启停、健康检查 | REST |
| 发送 APDU、读取身份证等一次性硬件操作 | REST 或 WS，均可 |
| 卡片插拔、硬件事件推送 | 仅 WS |

- WebSocket 做到**有状态的通道**：心跳保活（30s ping / 5s pong 超时）、事件订阅推送、requestId 回显匹配
- REST 侧重**管理**，也备选支持硬件操作，供 CLI 或非 WS 场景使用

## 部署形态
- **Windows**: `UseWindowsService()` + Inno Setup 打包（安装时勾选启用的硬件 + 端口选择）
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
- `WebSocketHandler.SendEventAsync` 使用 `SemaphoreSlim(5,5)` 控制背压
- 安装程序 `InitializeSetup` 先 `net stop` + `sc delete` 旧服务，确保新服务读取新配置

## 源码位置
- 架构设计：`doc/01-architecture-v1.0.1.md`（最新版）
- API 规范：`doc/02-api-specification-v1.0.1.md`（最新版）
- 部署打包：`doc/03-packaging-v1.0.1.md`（最新版）
- 跨平台/国产化：`doc/04-cross-platform-v1.0.0.md`
- 测试指南：`doc/05-testing-v1.0.1.md`（最新版）
- Vue demo：`demo/`（README 见 `demo/README.md`）

## 代码规范
- 命名空间 `DeviceHub.*`；异步方法 `Async` 后缀（如 `TransmitAsync`）
- 日志：`ILogger<T>` 记录启动/停止/错误/硬件事件
- 注释语言：中文
- 禁止 Unicode 装饰符号（× ≥ → ¥ 等语义符号不受限）
- 一次性交干净代码，不留明显可预见的缺陷
- **多语言支持**：默认英语（en-US），支持中文简体（zh-CN）
  - 所有用户可见文本必须通过 `IStringLocalizer<Program>` 获取
  - 资源文件位于 `src/DeviceHub.Service.Api/Resources/Strings.resx`（默认英语）和 `Strings.zh-CN.resx`（中文）
  - 新增字符串时，必须同时更新两个资源文件
  - 扩展方法位于 `Extensions/LocalizationExtensions.cs`

## 实施流程
1. 阅读相关设计文档（`doc/` 下最新版）
2. 输出修改方案（文件 → 改动点 → 预期结果）
3. **等待用户确认后才实施**（流程：方案 → 确认 → 实施）
4. 不自动执行 git add/commit/push，先询问
5. 不安装任何软件，不修改操作系统配置
6. 不执行超出用户明确要求的额外改动（包括但不限于：代码重构、文档更新、配置修改、依赖升级）
7. 涉及基础设施、打包、部署、CI/CD 的改动，即使看似必要，也必须先提出方案，等待确认

## 文档版本规则
- 文档命名：`NN-title-vX.Y.Z.md`，每次更新创建新版本文件，不修改旧版
- **语义化版本**：
  - `Z` 递增（v1.0.x）：bug 修复、小修正
  - `Y` 递增（v1.x.0）：功能更新、新增内容
  - `X` 递增（x.0.0）：架构级重大变更
- AGENTS.md 除外，始终为 `AGENTS.md` 不版本化
- 每个文档末尾保留版本历史表

## 已知待完成优化（P1/P2）
- P1-7: WebSocketHandler 改为注入单例（当前为静态类）
- P1-8: WS 事件订阅过滤（`?events=` 参数未实现）
- P1-11: DriverRegistry enable/disable 持久化到 appsettings.json
- P2: 移除 PcscReader 的 `Microsoft.AspNetCore.App` FrameworkReference
