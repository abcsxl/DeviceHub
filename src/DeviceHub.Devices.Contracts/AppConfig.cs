using System.Text.Json.Serialization;

namespace DeviceHub.Devices.Contracts;

public class AppConfig
{
    public ServerConfig Server { get; set; } = new();
    public Dictionary<string, DriverConfig> Drivers { get; set; } = new();
    public LoggingConfig Logging { get; set; } = new();
    public int ConfigVersion { get; set; } = 1;

    [JsonIgnore]
    public IReadOnlyList<string> ValidationErrors => _validationErrors;
    private List<string> _validationErrors = [];

    public bool Validate()
    {
        _validationErrors = [];
        if (Server.HttpPort is < 1 or > 65535)
            _validationErrors.Add("Server.HttpPort must be between 1 and 65535");
        if (Logging.RingBufferSize < 10)
            _validationErrors.Add("Logging.RingBufferSize must be at least 10");
        return _validationErrors.Count == 0;
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
                RingBufferSize = update.Logging?.RingBufferSize ?? Logging.RingBufferSize
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
}
