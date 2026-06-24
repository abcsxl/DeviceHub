using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeviceHub.Devices.Contracts;
using Microsoft.Extensions.Localization;

namespace DeviceHub.Service.Api.Endpoints;

public static class ConfigEndpoint
{
    private static volatile string? _defaultConfigJson;

    public static async Task InitializeDefaults(string configPath)
    {
        if (File.Exists(configPath))
            _defaultConfigJson = await File.ReadAllTextAsync(configPath, Encoding.UTF8);
    }

    public static WebApplication MapConfigEndpoint(this WebApplication app)
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
            ILoggerFactory loggerFactory,
            IStringLocalizer<Program> L) =>
        {
            var logger = loggerFactory.CreateLogger("Config");
            var current = BindConfig(config);
            var merged = current.Merge(update);

            var errors = merged.Validate();

            if (errors.Count > 0)
                return Results.Json(
                    new { error = "INVALID_PARAMETERS", message = string.Join("; ", errors) },
                    statusCode: 400);

            var configPath = Path.Combine(env.ContentRootPath, "appsettings.json");
            var existingJson = await File.ReadAllTextAsync(configPath, Encoding.UTF8);
            var existingObj = JsonNode.Parse(existingJson)?.AsObject() ?? new JsonObject();

            ApplyServerConfig(existingObj, merged.Server);
            ApplyDriversConfig(existingObj, merged.Drivers);
            ApplyLoggingConfig(existingObj, merged.Logging);
            existingObj["ConfigVersion"] = merged.ConfigVersion;

            var json = existingObj.ToJsonString(new JsonSerializerOptions
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
                logger.LogWarning("Driver enable state changed, restart required to take effect");
            }

            logger.LogInformation("Configuration updated, version {Version}", merged.ConfigVersion);
            return Results.Ok(merged);
        });

        app.MapPost("/api/config/reset", async (
            IConfiguration config,
            IWebHostEnvironment env,
            ILoggerFactory loggerFactory,
            IStringLocalizer<Program> L) =>
        {
            if (_defaultConfigJson == null)
                return Results.Json(
                    new { error = "HARDWARE_ERROR", message = L["NoDefaultConfig"].Value },
                    statusCode: 500);

            var logger = loggerFactory.CreateLogger("Config");
            var configPath = Path.Combine(env.ContentRootPath, "appsettings.json");
            await File.WriteAllTextAsync(configPath, _defaultConfigJson, Encoding.UTF8);

            if (config is IConfigurationRoot root)
                root.Reload();

            logger.LogInformation("Configuration reset to defaults");
            return Results.Ok(BindConfig(config));
        });

        return app;
    }

    private static void ApplyServerConfig(JsonObject root, ServerConfig server)
    {
        if (root["Server"] is not JsonObject serverObj)
        {
            serverObj = new JsonObject();
            root["Server"] = serverObj;
        }
        serverObj["HttpPort"] = server.HttpPort;
        serverObj["WebSocketPath"] = server.WebSocketPath;
    }

    private static void ApplyDriversConfig(JsonObject root, Dictionary<string, DriverConfig> drivers)
    {
        if (root["Drivers"] is not JsonObject driversObj)
        {
            driversObj = new JsonObject();
            root["Drivers"] = driversObj;
        }
        foreach (var (key, cfg) in drivers)
        {
            if (driversObj[key] is not JsonObject driverObj)
            {
                driverObj = new JsonObject();
                driversObj[key] = driverObj;
            }
            driverObj["Enabled"] = cfg.Enabled;
        }
    }

    private static void ApplyLoggingConfig(JsonObject root, LoggingConfig logging)
    {
        if (root["Logging"] is not JsonObject loggingObj)
        {
            loggingObj = new JsonObject();
            root["Logging"] = loggingObj;
        }
        loggingObj["RingBufferSize"] = logging.RingBufferSize;

        if (loggingObj["LogLevel"] is not JsonObject logLevelObj)
        {
            logLevelObj = new JsonObject();
            loggingObj["LogLevel"] = logLevelObj;
        }
        foreach (var (key, val) in logging.LogLevel)
            logLevelObj[key] = val;
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
