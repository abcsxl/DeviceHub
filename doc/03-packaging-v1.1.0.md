# DeviceHub — 部署与打包 (v1.1.0)

## 目录结构

```
deploy/
├── windows/
│   ├── devicehub.iss        # Inno Setup 安装脚本
│   └── publish.ps1          # Windows 本地打包脚本
└── ubuntu/
    ├── devicehub.service    # systemd 服务单元文件
    ├── install.sh           # Linux 一键安装脚本
    └── publish.sh           # Linux 本地打包脚本

.github/workflows/
├── build.yml                # PR 验证构建
└── release.yml              # 打 tag 自动构建+发布
```

---

## 自动发布流程（GitHub Actions）

推送形如 `v1.1.0` 的 tag 时自动触发 `release.yml`：

```
推送 tag v1.1.0
    │
    ├─ windows-latest runner
    │   ├─ dotnet publish -r win-x64 (自包含多文件)
    │   └─ ISCC 编译 installer → 上传
    │
    ├─ ubuntu-latest runner
    │   ├─ dotnet publish -r linux-x64 (自包含多文件)
    │   └─ 打包 tar.gz (含 binary + service + install.sh) → 上传
    │
    └─ release job
        ├─ 汇总产物 + 生成 versions.json
        └─ gh release create → 上传所有附件
```

Release 名格式：`DeviceHub v1.1.0`，自动生成 release notes。

---

## 发布产物

| 平台 | 产物 | 内容 |
|------|------|------|
| Windows | `DeviceHub-Setup-1.1.0.exe` + `.sha256` | Inno Setup 安装包 |
| Linux | `DeviceHub-1.1.0-linux-x64.tar.gz` + `.sha256` | 二进制 + service + install.sh |

每次 Release 还附带 `versions.json`（用于自更新检查版本）。

---

## Windows 本地打包

```powershell
# 需要先安装 Inno Setup 6
.\deploy\windows\publish.ps1 -Version "1.1.0"
```

参数：

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `-Version` | 1.0.0 | 版本号 |
| `-Configuration` | Release | 构建配置 |
| `-Runtime` | win-x64 | 目标运行时 |
| `-SkipInstaller` | - | 跳过安装包编译，只发布 |

---

## Linux 本地打包

```bash
chmod +x deploy/linux/publish.sh
./deploy/linux/publish.sh "1.1.0" "Release" "linux-x64"
```

参数依次为：版本号、构建配置、运行时标识。

---

## Windows 安装包行为

安装流程：

1. 选择安装目录（默认 `%ProgramFiles%\DeviceHub`）
2. 选择 HTTP 端口（默认 5000）
3. 注册 Windows 服务 `DeviceHub` 并启动

卸载流程：

1. 停止并删除 Windows 服务
2. 删除安装目录

---

## Linux 安装流程

```bash
# 解压
tar xzf DeviceHub-1.1.0-linux-x64.tar.gz
cd devicehub-1.1.0

# 安装（需要 root）
sudo bash install.sh
```

`install.sh` 自动完成：
1. 复制二进制到 `/usr/local/bin/devicehub/`
2. 注册 systemd 服务并启用开机自启
3. 启动服务

手动管理：

```bash
systemctl status devicehub
journalctl -u devicehub -f
systemctl stop devicehub
```

---

## versions.json（自更新用）

每次 Release 自动生成，格式如下：

```json
{
  "version": "1.1.0.0",
  "pubTime": "2026-06-01T00:00:00Z",
  "platforms": {
    "win-x64": {
      "url": "https://github.com/anomalyco/DeviceHub/releases/download/v1.1.0/DeviceHub-Setup-1.1.0.exe",
      "hash": "ab12cd34..."
    },
    "linux-x64": {
      "url": "https://github.com/anomalyco/DeviceHub/releases/download/v1.1.0/DeviceHub-1.1.0-linux-x64.tar.gz",
      "hash": "ef56gh78..."
    }
  }
}
```

客户端以 raw URL 获取：`https://github.com/anomalyco/DeviceHub/releases/latest/download/versions.json`

---

## 检查清单

- [ ] 目标机器无需 .NET 运行时（自包含发布）
- [ ] Windows：读卡器厂商驱动已安装
- [ ] Linux：`sudo apt install pcscd libpcsclite-dev`
- [ ] 防火墙开放服务端口（默认 5000）
- [ ] 安装时若默认端口被占用，安装程序会提示选择新端口

---

## 版本历史

- v1.0.0 (2026-05-20): 安装程序增加端口冲突检测与选择功能
- v1.0.0 (2026-05-19): 初版，定义 CI/CD 流程 + 双平台打包 + versions.json
