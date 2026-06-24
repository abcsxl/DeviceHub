using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeviceHub.Service.Api.Models;
using Microsoft.Extensions.Localization;

namespace DeviceHub.Service.Api.Endpoints;

public static class DriversEndpoint
{
    public static WebApplication MapDriversEndpoint(this WebApplication app)
    {
        app.MapGet("/api/drivers", (DriverRegistry registry) =>
        {
            return Results.Ok(registry.GetAll());
        });

        app.MapPost("/api/drivers/{name}/enable", async (
            string name,
            DriverRegistry registry,
            IWebHostEnvironment env,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            IStringLocalizer<Program> L) =>
        {
            var logger = loggerFactory.CreateLogger("Drivers");
            var entry = registry.Get(name);
            if (entry == null)
                return Results.NotFound(new { error = "DRIVER_NOT_FOUND", message = L["DriverNotFound", name].Value });

            entry.Enabled = true;
            try
            {
                await entry.Service.InitAsync();
            }
            catch
            {
                entry.Enabled = false;
                throw;
            }
            await PersistDriverEnabled(env, config, name, true);
            logger.LogInformation("Driver {Name} enabled", name);
            return Results.Ok(new { name, status = entry.Service.Status.ToString(), enabled = true });
        });

        app.MapPost("/api/drivers/{name}/disable", async (
            string name,
            DriverRegistry registry,
            IWebHostEnvironment env,
            IConfiguration config,
            ILoggerFactory loggerFactory,
            IStringLocalizer<Program> L) =>
        {
            var logger = loggerFactory.CreateLogger("Drivers");
            var entry = registry.Get(name);
            if (entry == null)
                return Results.NotFound(new { error = "DRIVER_NOT_FOUND", message = L["DriverNotFound", name].Value });

            entry.Enabled = false;
            try
            {
                await entry.Service.ShutdownAsync();
            }
            catch
            {
                entry.Enabled = true;
                throw;
            }
            await PersistDriverEnabled(env, config, name, false);
            logger.LogInformation("Driver {Name} disabled", name);
            return Results.Ok(new { name, status = entry.Service.Status.ToString(), enabled = false });
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
