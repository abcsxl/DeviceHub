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
            await File.WriteAllTextAsync(configPath, json);

            if (config is IConfigurationRoot root)
                root.Reload();

            logger.LogInformation("配置已更新，版本号 {Version}", merged.ConfigVersion);
            return Results.Ok(merged);
        });
        return app;
    }

    private static AppConfig BindConfig(IConfiguration config)
    {
        return new AppConfig
        {
            Server = new ServerConfig
            {
                HttpPort = config.GetValue<int>("Server:HttpPort"),
                WebSocketPath = config.GetValue<string>("Server:WebSocketPath") ?? "/ws"
            },
            Drivers = new Dictionary<string, DriverConfig>
            {
                ["Pcsc"] = new() { Enabled = config.GetValue<bool>("Drivers:Pcsc:Enabled") },
                ["Printer"] = new() { Enabled = config.GetValue<bool>("Drivers:Printer:Enabled") },
                ["IdCard"] = new() { Enabled = config.GetValue<bool>("Drivers:IdCard:Enabled") }
            },
            Logging = new LoggingConfig
            {
                RingBufferSize = config.GetValue<int>("Logging:RingBufferSize")
            }
        };
    }
}
