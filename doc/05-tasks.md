# Batch 1 — v1.0.0 完成

## 任务清单

- [x] 更新 `AGENTS.md` → `AGENTS-v1.0.0.md`（精简，匹配实际架构）
- [x] 重写 `doc/01-architecture-v1.0.0.md`（三层架构 + PCSC 接口 + IC card 规划）
- [x] 重写 `doc/02-api-specification-v1.0.0.md`（完整 REST 管理 + WS 协议 + PCSC 硬件操作）
- [x] 新建 `doc/03-packaging-v1.0.0.md`（Inno Setup + systemd + 服务管理）
- [x] 更新 `doc/04-cross-platform-v1.0.0.md`
- [x] 新建 `DeviceHub.slnx`（3 个项目）
- [x] 创建 `DeviceHub.Devices.Contracts/`（接口 + DTO + 充血配置模型）
- [x] 创建 `DeviceHub.Devices.PcscReader/`（P/Invoke 原生 PCSC 实现）
- [x] 创建 `DeviceHub.Service.Api/`（7 个管理端点 + 4 个 PCSC 硬件端点 + WS 路由 + 环形缓冲区日志）
- [x] 项目重命名（Abstractions→Contracts, PcscService→PcscReader, Service→Service.Api）
- [x] 验证：`dotnet build` 0 错误 0 警告，`/api/status` 正常返回

## 下一阶段 v1.1.0 计划

- 交通部互联互通卡（JT/T 978）专用业务层接口
- 自托管管理 UI
