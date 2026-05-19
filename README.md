# DeviceHub

[![Build](https://github.com/abcsxl/DeviceHub/actions/workflows/build.yml/badge.svg)](https://github.com/abcsxl/DeviceHub/actions)
[![License](https://img.shields.io/badge/license-MIT-blue)](https://opensource.org/licenses/MIT)
[![GitHub Release](https://img.shields.io/github/v/release/abcsxl/DeviceHub)](https://github.com/abcsxl/DeviceHub/releases)

硬件设备网关服务，将 PCSC 读卡器、证卡打印机、身份证读卡器等硬件能力抽象为 REST API + WebSocket。

## 项目结构

```
DeviceHub.slnx
src/
├── DeviceHub.Devices.Contracts/    # 抽象接口 + DTO + 配置模型
├── DeviceHub.Devices.PcscReader/   # PCSC 读卡器实现
└── DeviceHub.Service.Api/          # Minimal APIs 主程序
deploy/
├── windows/                        # Inno Setup 安装脚本
└── ubuntu/                         # systemd 服务 + 安装脚本
.github/workflows/                  # CI/CD (build + release)
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
| `doc/05-testing-v1.0.0.md` | 测试指南 |

## 快速体验

```bash
# 启动后端
dotnet run --project src/DeviceHub.Service.Api --urls http://localhost:5000

# 启动 Demo 页面（另开终端）
cd demo
npm install
npm run dev
# → 浏览器打开 http://localhost:3000
```

## 发布

发布到 GitHub Releases（推 tag 自动构建）：

```bash
git tag v1.0.0
git push origin v1.0.0
```

Windows 安装包和 Linux tar.gz 自动生成并上传到 Release 页面。详见 `doc/03-packaging-v1.0.0.md`。

## 协议

[MIT](LICENSE.txt)
