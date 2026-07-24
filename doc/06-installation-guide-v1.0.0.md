# DeviceHub 安装部署指南 (v1.0.0)

## 系统要求

| 平台 | 最低要求 | 推荐 |
|------|----------|------|
| Windows | Windows 10 x64 / Windows Server 2019+ | Windows 11 |
| Linux | Ubuntu 20.04+, Debian 11+, CentOS 8+, openEuler 22.03+ | Ubuntu 22.04 LTS |

**依赖项（Linux）：**

```bash
# Ubuntu/Debian
sudo apt install pcscd libpcsclite-dev cups

# CentOS/RHEL
sudo yum install pcsc-lite pcsc-lite-devel cups-libs
```

**注意：** 读卡器驱动程序需按厂商说明单独安装。

---

## 安装包获取

从 GitHub Releases 页面下载对应平台的安装包：

| 平台 | 安装包 | 适用场景 |
|------|--------|----------|
| Windows | `DeviceHub-Setup-{version}.exe` | 图形化安装，推荐 |
| Linux | `DeviceHub-{version}-linux-x64.tar.gz` + `install.sh` | 通用 Linux |
| Debian/Ubuntu | `devicehub_{version}_{arch}.deb` | apt 管理 |
| CentOS/RHEL | `devicehub-{version}-1.{arch}.rpm` | yum/rpm 管理 |

每个安装包附带 `.sha256` 哈希文件用于完整性校验。

---

## Windows 安装

### 安装步骤

1. 双击运行 `DeviceHub-Setup-{version}.exe`
2. 选择安装目录（默认 `C:\Program Files\DeviceHub`）
3. 配置 HTTP 端口（默认 5000；端口被占用时提示修改）
4. 安装程序自动完成：
   - 停止旧版服务（覆盖安装时）
   - 复制文件
   - 注册 Windows 服务 `DeviceHub`
   - 启动服务

### 验证安装

打开浏览器访问 `http://localhost:5000/api/status`，应返回：

```json
{ "success": true, "data": { "service": "DeviceHub", "status": "running" } }
```

### 服务管理

```cmd
:: 查看状态
sc query DeviceHub

:: 停止
net stop DeviceHub

:: 启动
net start DeviceHub

:: 重启
net stop DeviceHub && net start DeviceHub
```

也可在 Windows 服务管理器（`services.msc`）中搜索 `DeviceHub` 进行管理。

### 日志查看

日志输出到 `%ProgramFiles%\DeviceHub\log.log`（如果配置了文件日志），或通过服务 API `GET /api/logs` 查看最近日志。

### 卸载

- **方式一：** 控制面板 → 程序和功能 → 找到 `DeviceHub` → 卸载
- **方式二：** 重新运行安装包，选择卸载

卸载自动停止服务、删除 Windows 服务、删除安装目录。

---

## Linux 通用安装（tar.gz）

### 安装

```bash
# 解压
tar xzf DeviceHub-{version}-linux-x64.tar.gz
cd devicehub-{version}

# 安装（需要 root）
sudo bash install.sh
```

`install.sh` 交互流程：

1. **语言选择**：English / 中文简体
2. **端口配置**：默认 5000，被占用时提示输入新端口
3. **覆盖安装检测**：自动读取旧配置端口
4. **停止旧服务**：自动停止，无法停止时询问是否强制结束
5. **安装步骤**：
   - 复制二进制到 `/usr/local/bin/devicehub/`
   - 配置 HTTP 端口
   - 注册 systemd 服务并启用开机自启
   - 启动服务

### 验证安装

```bash
curl http://localhost:5000/api/status
```

### 服务管理

```bash
# 查看状态
systemctl status devicehub

# 查看实时日志
journalctl -u devicehub -f

# 重启
systemctl restart devicehub

# 停止
systemctl stop devicehub

# 启动
systemctl start devicehub
```

### 修改配置

```bash
sudo nano /usr/local/bin/devicehub/appsettings.json
systemctl restart devicehub
```

### 卸载

```bash
# 进入安装目录
cd /usr/local/bin/devicehub/

# 执行卸载脚本（需要 root）
sudo ./uninstall.sh
```

卸载脚本完成：
1. 停止并禁用 `devicehub` 服务
2. 删除 systemd 服务文件
3. 删除 `/usr/local/bin/devicehub/` 目录
4. 重载 systemd

> 配置和日志保留在被删除的目录中。如彻底清除，卸载后手动执行 `sudo rm -rf /usr/local/bin/devicehub/`。

---

## Debian/Ubuntu 安装（.deb）

```bash
# 安装
sudo dpkg -i devicehub_{version}_{arch}.deb
# 或
sudo apt install ./devicehub_{version}_{arch}.deb

# 修复依赖（如有）
sudo apt --fix-broken install

# 验证
systemctl status devicehub
curl http://localhost:5000/api/status
```

安装后自动注册 systemd 服务并启动。

### 卸载

```bash
sudo apt remove devicehub
```

---

## CentOS/RHEL 安装（.rpm）

```bash
# 安装
sudo rpm -ivh devicehub-{version}-1.{arch}.rpm
# 或
sudo yum install ./devicehub-{version}-1.{arch}.rpm

# 验证
systemctl status devicehub
curl http://localhost:5000/api/status
```

### 卸载

```bash
sudo rpm -e devicehub
# 或
sudo yum remove devicehub
```

---

## 端口配置

默认端口 5000。可通过以下方式修改：

### 安装时指定

Windows 安装包在安装过程中提示输入端口；Linux `install.sh` 在启动时检测端口占用并提示修改。

### 安装后修改

编辑 `appsettings.json` 中的 `Server:HttpPort` 值，重启服务：

```json
{
  "Server": {
    "HttpPort": 5001,
    "WebSocketPath": "/ws"
  }
}
```

### 启动时临时指定（不修改配置文件）

```bash
# Linux
/usr/local/bin/devicehub/DeviceHub.Service.Api --port 5001

# Windows (命令行)
DeviceHub.Service.Api.exe --port 5001
```

> 若指定端口被占用，服务自动尝试 +1 至 +10，均被占用则启动失败。

---

## 驱动配置

| 驱动 | 默认状态 | 说明 |
|------|----------|------|
| PCSC 读卡器 | 启用 | 智能卡读写 |
| 打印机 | 启用 | ESC/POS / ZPL 打印 |
| 身份证 | 禁用 | 需配合身份证阅读器硬件 |

编辑 `appsettings.json` 的 `Drivers` 节：

```json
{
  "Drivers": {
    "Pcsc": { "Enabled": true, "Mock": false },
    "Printer": { "Enabled": true, "Mock": false },
    "IdCard": { "Enabled": false, "ComPort": "COM3" }
  }
}
```

修改后重启服务生效。

**Mock 模式：** 将 `Mock` 设为 `true` 可启用模拟服务，适用于开发和测试。Mock 服务返回固定数据，无需物理硬件。

---

## 常见问题

### 服务无法启动

```bash
# Linux — 查看详细错误
journalctl -u devicehub -n 50

# Windows — 查看事件查看器
# 运行 eventvwr.msc → Windows 日志 → 应用程序
```

常见原因：
- 端口被占用（更换端口或释放占用端口）
- 缺少 PCSC 驱动（Linux: `sudo apt install pcscd`）
- 配置文件语法错误

### 读卡器未识别

1. 确认读卡器已插入 USB 端口
2. 确认读卡器驱动已正确安装
3. 确认 PCSC 服务正在运行：
   - Windows: `services.msc` → `SCardSvr`
   - Linux: `systemctl status pcscd`
4. API 验证：`GET /api/hardware/pcsc/readers` 返回读卡器列表

### 访问被拒绝

浏览器访问 `localhost:5000` 时跨域请求被拦截：
- 确认启动参数包含 `--urls http://0.0.0.0:5000`（默认配置）
- HTTPS 页面访问 HTTP localhost 时可能需要添加 PNA 放行（后端已默认添加 `Access-Control-Allow-Private-Network: true`）
- WebSocket 连接使用 `ws://` 而非 `wss://`

### 端口冲突

```bash
# Linux — 查看端口占用
ss -tlnp | grep 5000

# Windows
netstat -ano | findstr :5000
```

服务启动时自动检测端口冲突，默认尝试 5000-5009 范围内的首个可用端口。

---

## 版本历史

- v1.0.0 (2026-07-24): 初版
