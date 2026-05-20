using System.Runtime.InteropServices;

namespace DeviceHub.Service.Api.Models;

public class ServiceState
{
    public DateTime StartTime { get; } = DateTime.UtcNow;

    public int HttpPort { get; set; }

    public string Version => typeof(ServiceState).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    private readonly string _platform;

    public string Platform => _platform;

    public string Architecture => RuntimeInformation.OSArchitecture.ToString().ToLower();

    public ServiceState()
    {
        _platform = ResolvePlatform();
    }

    private static string ResolvePlatform()
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
