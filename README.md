# DeviceHub

[![License](https://img.shields.io/badge/license-MIT-blue)](https://opensource.org/licenses/MIT)
[![Build](https://github.com/abcsxl/DeviceHub/actions/workflows/build.yml/badge.svg)](https://github.com/abcsxl/DeviceHub/actions)
[![GitHub Release](https://img.shields.io/github/v/release/abcsxl/DeviceHub)](https://github.com/abcsxl/DeviceHub/releases)
![GitHub Language](https://img.shields.io/github/languages/top/abcsxl/DeviceHub)

硬件设备网关服务，将 PCSC 读卡器、系统打印机、身份证读卡器等硬件能力抽象为 REST API + WebSocket。默认英语（en-US），支持中文简体（zh-CN），通过 `Accept-Language` 请求头切换。

## 项目结构

```
DeviceHub.slnx
src/
├── DeviceHub.Devices.Contracts/    # 抽象接口 + DTO + 配置模型（NuGet 发布）
├── DeviceHub.Devices.PcscReader/   # PCSC 读卡器实现
├── DeviceHub.Devices.Printer/      # 系统打印机（PrintDocument + Win32 Spooler）
├── DeviceHub.Devices.IdCard/       # 身份证读卡器（stub，需厂商 SDK）
├── DeviceHub.Cards.TransitCard/    # 互联互通卡 JT/T 978 协议封装
├── DeviceHub.DriverLoader/         # 外部 DLL 插件加载器
└── DeviceHub.Service.Api/          # Minimal APIs 主程序
demo/                               # Vue 3 前端测试工具
doc/                                # 设计文档（架构/API/打包/测试）
deploy/
├── windows/                        # Inno Setup 安装脚本
└── linux/                          # systemd 服务 + 安装脚本
.github/workflows/                  # CI/CD (build + release)
```

## 运行

```bash
dotnet run --project src/DeviceHub.Service.Api --urls http://localhost:5000
```

## 文档

| 文件 | 内容 |
|------|------|
| `doc/01-architecture-v1.1.0.md` | 三层架构设计、硬件抽象、Mock 模式、多语言支持 |
| `doc/02-api-specification-v1.2.0.md` | REST + WebSocket 接口规范、Mock 行为说明 |
| `doc/03-packaging-v1.1.0.md` | Windows / Linux 部署打包 |
| `doc/04-cross-platform-v1.0.0.md` | 跨平台注意事项 |
| `doc/05-testing-v1.1.0.md` | 测试指南 |

## 快速体验

**推荐 Mock 模式**（无需物理读卡器即可体验全部功能）：

```bash
# Windows (PowerShell)
$env:Drivers__Pcsc__Mock="true"
dotnet run --project src\DeviceHub.Service.Api

# Linux / macOS
export Drivers__Pcsc__Mock=true
dotnet run --project src/DeviceHub.Service.Api --urls http://localhost:5000
```

```bash
# 启动 Demo 页面（另开终端）
cd demo
npm install
npm run dev
# → 浏览器打开 http://localhost:3000（或提示的地址）
```

Mock 模式下提供：2 个虚拟读卡器、固定 ATR、模拟 APDU 响应、每 10 秒自动卡片插拔事件。适用于前端联调、功能演示和自动化测试。

**真实硬件模式**：不设置或设置为 `false` 即可：

```bash
$env:Drivers__Pcsc__Mock="false"
dotnet run --project src\DeviceHub.Service.Api
```

## 发布

发布到 GitHub Releases（推 tag 自动构建）：

```bash
git tag v1.0.0
git push origin v1.0.0
```

Windows 安装包和 Linux tar.gz 自动生成并上传到 Release 页面。详见 `doc/03-packaging-v1.1.0.md`。

## 协议

[MIT](LICENSE.txt)
