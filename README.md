# DeviceHub

![Build Status](https://travis-ci.org/user/repo.svg)
![License](https://img.shields.io/badge/license-Six%20Labs%20Split-blue)

硬件设备网关服务，将 PCSC 读卡器、证卡打印机、身份证读卡器等硬件能力抽象为 REST API + WebSocket。

## 项目结构

```
DeviceHub.slnx
src/
├── DeviceHub.Devices.Contracts/   # 抽象接口 + DTO + 配置模型
├── DeviceHub.Devices.PcscReader/  # PCSC 读卡器实现
└── DeviceHub.Service.Api/         # Minimal APIs 主程序
```

## 运行

```bash
dotnet run --project src/DeviceHub.Service.Api --urls http://localhost:5000
```

## 文档

| 文件 | 内容 |
|------|------|
| `doc/01-architecture-v1.0.0.md` | 三层架构设计与硬件抽象 |
| `doc/02-api-specification-v1.0.0.md` | REST + WebSocket 接口规范 |
| `doc/03-packaging-v1.0.0.md` | Windows / Linux 部署打包 |
| `doc/04-cross-platform-v1.0.0.md` | 跨平台注意事项 |
| `doc/06-testing-v1.0.0.md` | 测试指南 |
| `doc/05-tasks.md` | 任务追踪 |

## 发布

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## 协议

[MIT](LICENSE.txt)
