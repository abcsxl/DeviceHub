# DeviceHub — 部署与打包 (v1.0.0)

## .NET 发布命令

```bash
# Windows x64 自包含单文件
dotnet publish src\DeviceHub.Service -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# Linux x64 自包含单文件
dotnet publish src/DeviceHub.Service -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# 龙芯 LoongArch64
dotnet publish src/DeviceHub.Service -c Release -r linux-loongarch64 --self-contained true
```

---

## Windows — Inno Setup 打包（框架描述）

```pascal
[Setup]
AppName=DeviceHub
AppVersion=1.0.0
DefaultDirName={pf}\DeviceHub

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Components]
Name: "pcsc"; Description: "PCSC 读卡器支持"; Types: full custom
Name: "printer"; Description: "证卡打印机支持"; Types: full custom

[Run]
Filename: "{app}\DeviceHub.Service.exe"; Parameters: "--install"; Flags: runhidden

[UninstallRun]
Filename: "{app}\DeviceHub.Service.exe"; Parameters: "--uninstall"; Flags: runhidden
```

安装程序根据勾选的组件写入 `appsettings.json` 中对应 `Drivers.Xxx.Enabled` 字段。

---

## Windows 服务管理

```batch
:: 安装服务
sc create DeviceHub binPath= "C:\Program Files\DeviceHub\DeviceHub.Service.exe" start= auto

:: 启动/停止
sc start DeviceHub
sc stop DeviceHub

:: 查询状态
sc query DeviceHub

:: 删除服务
sc delete DeviceHub
```

---

## Linux — systemd 服务文件

```ini
[Unit]
Description=DeviceHub Service
After=network.target

[Service]
Type=notify
ExecStart=/usr/local/bin/devicehub/DeviceHub.Service
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
# 安装服务
sudo cp devicehub.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable devicehub
sudo systemctl start devicehub

# 查看状态
sudo systemctl status devicehub

# 查看日志
journalctl -u devicehub -f
```

---

## 部署前检查清单

- [ ] 目标机器已安装 .NET 10 运行时（或使用自包含发布）
- [ ] Windows：读卡器厂商驱动已安装
- [ ] Linux：`sudo apt install pcscd libpcsclite-dev`
- [ ] 防火墙开放 5000 端口（如需远程访问）
- [ ] 配置文件 `appsettings.json` 端口与防火墙一致

---

## 版本历史
- v1.0.0 (2026-05-19): 初版，定义发布命令 + Inno Setup + systemd
