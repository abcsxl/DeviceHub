# DeviceHub — AGENTS.md (v1.0.0)

## 项目信息
- 三个项目：`DeviceHub.Devices.Contracts`（抽象）→ `DeviceHub.Devices.PcscReader`（实现）→ `DeviceHub.Service.Api`（主程序）
- 目标框架 **net10.0**，Minimal APIs 项目
- 三层架构：Minimal APIs → Service → Hardware，**不做 DDD / CQRS / MediatR**
- 每种硬件独立接口（如 `IPcscService`），继承轻量基接口 `IHardwareService`（仅生命周期）
- 通过 `AddXxxService(IServiceCollection, IConfiguration)` 扩展方法条件注入
- **无持久化数据库**：日志为内存环形缓冲区，配置为 JSON 文件读写 + Reload
- 配置模型充血（`Validate()` + `Merge()`），硬件操作 DTO 使用贫血 record
- 当前自托管管理 UI 设计未定，预留未来嵌入 SPA 空间

## 协议分工
| 场景 | 协议 |
|---|---|
| 配置管理、状态查询、日志、驱动启停、健康检查 | REST |
| 发送 APDU、读取身份证等一次性硬件操作 | REST 或 WS，均可 |
| 卡片插拔、硬件事件推送 | 仅 WS |

- WebSocket 做到**有状态的通道**：心跳保活、事件订阅推送、requestId 回显匹配
- REST 侧重**管理**，也备选支持硬件操作，供 CLI 或非 WS 场景使用

## 部署形态
- **Windows**: `UseWindowsService()` + Inno Setup 打包（安装时勾选启用的硬件）
- **Linux**: `UseSystemd()` + systemd service 模板
- 发布方式：`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

## 源码位置
- 架构设计：`doc/01-architecture-v1.0.0.md`
- API 规范：`doc/02-api-specification-v1.0.0.md`
- 部署打包：`doc/03-packaging-v1.0.0.md`
- 跨平台/国产化：`doc/04-cross-platform-v1.0.0.md`
## 代码规范
- 命名空间 `DeviceHub.*`；异步方法 `Async` 后缀（如 `TransmitAsync`）
- 日志：`ILogger<T>` 记录启动/停止/错误/硬件事件
- 注释语言：中文
- 禁止 Unicode 装饰符号（× ≥ → ¥ 等语义符号不受限）
- 一次性交干净代码，不留明显可预见的缺陷

## 实施流程
1. 阅读相关设计文档（`doc/01-architecture-v1.0.0.md` 等）
2. 输出修改方案（文件 → 改动点 → 预期结果）
3. **等待用户确认后才实施**（流程：方案 → 确认 → 实施）
4. 不自动执行 git add/commit/push，先询问
5. 不安装任何软件，不修改操作系统配置
6. 不执行超出用户明确要求的额外改动（包括但不限于：代码重构、文档更新、配置修改、依赖升级）
7. 涉及基础设施、打包、部署、CI/CD 的改动，即使看似必要，也必须先提出方案，等待确认

---
