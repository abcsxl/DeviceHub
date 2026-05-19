using System.Runtime.InteropServices;

namespace DeviceHub.Service.Api.Models;

public class ServiceState
{
    public DateTime StartTime { get; } = DateTime.UtcNow;

    public string Version => typeof(ServiceState).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    public string Platform
    {
        get
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
    }

    public string Architecture => RuntimeInformation.OSArchitecture.ToString().ToLower();
}
