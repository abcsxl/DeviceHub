# DeviceHub — 跨平台与国产化适配 (v1.0.0)

## .NET 10 在各平台的支持

| 平台 | CPU 架构 | .NET 10 来源 | 状态 |
|------|----------|--------------|------|
| Windows | x64/ARM64 | 微软官方 | 正式版 |
| 银河麒麟 V10 | x64/ARM64 | 系统源/龙芯源 | 可用 |
| 统信 UOS V20 | x64/ARM64 | 系统源 | 可用 |
| 龙芯 LoongArch64 | LoongArch | 龙芯社区源 | 10.0.5-1 已发布 |

## 运行时检测

```csharp
public static class PlatformDetector
{
    public static string GetPlatform()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsLinux())
        {
            if (File.Exists("/etc/kylin-release")) return "kylin";
            if (File.Exists("/etc/uos-release")) return "uos";
            return "linux";
        }
        return "unknown";
    }

    public static string GetArchitecture()
        => RuntimeInformation.OSArchitecture.ToString().ToLower();
}
```

## PC/SC 在不同平台的实现

| 平台 | PC/SC 实现 | 安装命令 |
|------|-----------|---------|
| Windows | Windows PC/SC API | 厂商提供驱动 |
| Linux / 国产系统 | pcsc-lite | `sudo apt install pcscd libpcsclite-dev` |

## 龙芯 NuGet 源

```xml
<!-- NuGet.config -->
<packageSources>
    <add key="loongnix" value="https://nuget.loongnix.cn/v3/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources>

---

## 跨平台配套脚本

| 目录 | 文件 | 用途 |
|------|------|------|
| `deploy/linux/` | `devicehub.service` | systemd 服务单元 |
| `deploy/linux/` | `install.sh` | Linux 一键安装 |
| `deploy/linux/` | `publish.sh` | Linux 本地发布打包 |
| `deploy/windows/` | `devicehub.iss` | Inno Setup 安装脚本 |
| `deploy/windows/` | `publish.ps1` | Windows 本地发布打包 |

详情见 `doc/03-packaging-v1.0.0.md`。

---

## 版本历史
- v1.0.0 (2026-05-19): 初版，补充服务管理命令 + deploy/ 配套脚本
```
