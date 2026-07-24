using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeviceHub.Devices.Contracts.Helpers;
using DeviceHub.Service.Api.Models;

namespace DeviceHub.Service.Api.Endpoints;

public static class DriversEndpoint
{
    public static WebApplication MapDriversEndpoint(this WebApplication app)
    {
        app.MapGet("/api/drivers", (DriverRegistry registry) =>
        {
            return ApiResponseHelper.Ok(registry.GetAll());
        });

        app.MapPost("/api/drivers/{name}/enable", async (
            string name,
            DriverRegistry registry,
            IWebHostEnvironment env,
            IConfiguration config,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Drivers");
            var entry = registry.Get(name);
            if (entry == null)
                return ApiResponseHelper.NotFound("DRIVER_NOT_FOUND", $"Driver '{name}' is not registered");

            entry.Enabled = true;
            try
            {
                await entry.Service.InitAsync();
            }
            catch (Exception ex)
            {
                entry.Enabled = false;
                return ApiResponseHelper.Error("HARDWARE_ERROR", ex.Message);
            }
            await PersistDriverEnabled(env, config, name, true);
            logger.LogInformation("Driver {Name} enabled", name);
            return ApiResponseHelper.Ok(new { name, status = entry.Service.Status.ToString(), enabled = true });
        });

        app.MapPost("/api/drivers/{name}/disable", async (
            string name,
            DriverRegistry registry,
            IWebHostEnvironment env,
            IConfiguration config,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Drivers");
            var entry = registry.Get(name);
            if (entry == null)
                return ApiResponseHelper.NotFound("DRIVER_NOT_FOUND", $"Driver '{name}' is not registered");

            entry.Enabled = false;
            try
            {
                await entry.Service.ShutdownAsync();
            }
            catch (Exception ex)
            {
                entry.Enabled = true;
                return ApiResponseHelper.Error("HARDWARE_ERROR", ex.Message);
            }
            await PersistDriverEnabled(env, config, name, false);
            logger.LogInformation("Driver {Name} disabled", name);
            return ApiResponseHelper.Ok(new { name, status = entry.Service.Status.ToString(), enabled = false });
        });
        return app;
    }

    private static async Task PersistDriverEnabled(IWebHostEnvironment env, IConfiguration config, string driverName, bool enabled)
    {
        var configPath = Path.Combine(env.ContentRootPath, "appsettings.json");
        var json = await File.ReadAllTextAsync(configPath, Encoding.UTF8);
        var root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();

        if (root["Drivers"] is JsonObject driversObj)
        {
            if (driversObj[driverName] is JsonObject driverEl)
            {
                driverEl["Enabled"] = enabled;
            }
        }

        var newJson = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var tempPath = configPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, newJson, Encoding.UTF8);
        File.Move(tempPath, configPath, overwrite: true);

        if (config is IConfigurationRoot root2)
            root2.Reload();
    }
}
