namespace DeviceHub.Devices.Contracts;

public abstract class HardwareDriverBase : IHardwareService
{
    public abstract string Name { get; }

    public HardwareStatus Status { get; protected set; }

    public abstract Task InitAsync(CancellationToken ct = default);

    public abstract Task ShutdownAsync(CancellationToken ct = default);

    protected TConfig? LoadConfig<TConfig>(string? configPath = null) where TConfig : class
    {
        configPath ??= Path.ChangeExtension(GetType().Assembly.Location, ".json");
        if (!File.Exists(configPath))
            return null;

        try
        {
            var json = File.ReadAllText(configPath);
            return System.Text.Json.JsonSerializer.Deserialize<TConfig>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }
        catch
        {
            return null;
        }
    }
}
