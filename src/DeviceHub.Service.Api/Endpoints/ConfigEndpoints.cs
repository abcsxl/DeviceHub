using System.Text;
using System.Text.Json;
using DeviceHub.Devices.Contracts;

namespace DeviceHub.Service.Api.Endpoints;

public static class ConfigEndpoints
{
    public static WebApplication MapConfigEndpoints(this WebApplication app)
    {
        app.MapGet("/api/config", async (IConfiguration config) =>
        {
            var appConfig = BindConfig(config);
            return Results.Json(appConfig, new JsonSerializerOptions { WriteIndented = true });
        });

        app.MapPut("/api/config", async (
            AppConfig update,
            IConfiguration config,
            IWebHostEnvironment env,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Config");
            var current = BindConfig(config);
            var merged = current.Merge(update);

            if (!merged.Validate())
                return Results.Problem(
                    statusCode: 400,
                    title: "配置校验失败",
                    detail: string.Join("; ", merged.ValidationErrors));

            var configPath = Path.Combine(env.ContentRootPath, "appsettings.json");
            var json = JsonSerializer.Serialize(merged, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await File.WriteAllTextAsync(configPath, json, Encoding.UTF8);

            if (config is IConfigurationRoot root)
                root.Reload();

            var afterBind = BindConfig(config);
            if (current.Drivers.Count != afterBind.Drivers.Count ||
                current.Drivers.Any(kv => kv.Value.Enabled !=
                    (afterBind.Drivers.TryGetValue(kv.Key, out var d) && d.Enabled)))
            {
                logger.LogWarning("驱动启用状态已变更，请重启服务以生效");
            }

            logger.LogInformation("配置已更新，版本号 {Version}", merged.ConfigVersion);
            return Results.Ok(merged);
        });
        return app;
    }

    private static AppConfig BindConfig(IConfiguration config)
    {
        var drivers = new Dictionary<string, DriverConfig>();
        foreach (var kv in config.GetSection("Drivers").GetChildren())
        {
            drivers[kv.Key] = new DriverConfig
            {
                Enabled = kv.GetValue<bool>("Enabled")
            };
        }

        return new AppConfig
        {
            Server = new ServerConfig
            {
                HttpPort = config.GetValue<int>("Server:HttpPort"),
                WebSocketPath = config.GetValue<string>("Server:WebSocketPath") ?? "/ws"
            },
            Drivers = drivers,
            Logging = new LoggingConfig
            {
                RingBufferSize = config.GetValue<int>("Logging:RingBufferSize"),
                LogLevel = config.GetSection("Logging:LogLevel")
                    .Get<Dictionary<string, string>>() ?? new()
            }
        };
    }
}
