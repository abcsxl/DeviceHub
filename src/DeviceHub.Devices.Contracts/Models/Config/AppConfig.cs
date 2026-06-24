namespace DeviceHub.Devices.Contracts;

public class AppConfig
{
    public ServerConfig Server { get; set; } = new();
    public Dictionary<string, DriverConfig> Drivers { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    public int ConfigVersion { get; set; } = 1;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Server.HttpPort is < 1 or > 65535)
            errors.Add("Server.HttpPort must be between 1 and 65535");
        if (Logging.RingBufferSize < 10)
            errors.Add("Logging.RingBufferSize must be at least 10");
        return errors;
    }

    public AppConfig Merge(AppConfig update)
    {
        var mergedDrivers = new Dictionary<string, DriverConfig>(Drivers);
        if (update.Drivers != null)
        {
            foreach (var (key, value) in update.Drivers)
            {
                if (mergedDrivers.ContainsKey(key))
                    mergedDrivers[key] = value;
            }
        }

        return new AppConfig
        {
            ConfigVersion = ConfigVersion + 1,
            Server = new ServerConfig
            {
                HttpPort = update.Server?.HttpPort ?? Server.HttpPort,
                WebSocketPath = update.Server?.WebSocketPath ?? Server.WebSocketPath
            },
            Logging = new LoggingConfig
            {
                RingBufferSize = update.Logging?.RingBufferSize ?? Logging.RingBufferSize,
                LogLevel = update.Logging?.LogLevel != null
                    ? Logging.LogLevel
                        .Where(kv => !update.Logging.LogLevel.ContainsKey(kv.Key))
                        .Concat(update.Logging.LogLevel)
                        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                    : Logging.LogLevel
            },
            Drivers = mergedDrivers
        };
    }
}

public class ServerConfig
{
    public int HttpPort { get; set; } = 5000;
    public string WebSocketPath { get; set; } = "/ws";
}

public class DriverConfig
{
    public bool Enabled { get; set; } = false;
}

public class LoggingConfig
{
    public int RingBufferSize { get; set; } = 1000;
    public Dictionary<string, string> LogLevel { get; set; } = new()
    {
        ["Default"] = "Information",
        ["Microsoft.AspNetCore"] = "Warning"
    };
}
