using System.Text;
using System.Text.Json;
using DeviceHub.Service.Api.Models;
using Microsoft.Extensions.Localization;

namespace DeviceHub.Service.Api.Endpoints;

public static class DriversEndpoints
{
    public static WebApplication MapDriversEndpoints(this WebApplication app)
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
                return Results.NotFound(new { error = "DRIVER_NOT_FOUND", message = L["DriverNotFound", name] });

            entry.Enabled = true;
            await entry.Service.InitAsync();
            await PersistDriverEnabled(env, config, name, true);
            logger.LogInformation("驱动 {Name} 已启用", name);
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
                return Results.NotFound(new { error = "DRIVER_NOT_FOUND", message = L["DriverNotFound", name] });

            entry.Enabled = false;
            await entry.Service.ShutdownAsync();
            await PersistDriverEnabled(env, config, name, false);
            logger.LogInformation("驱动 {Name} 已禁用", name);
            return Results.Ok(new { name, status = entry.Service.Status.ToString(), enabled = false });
        });
        return app;
    }

    private static async Task PersistDriverEnabled(IWebHostEnvironment env, IConfiguration config, string driverName, bool enabled)
    {
        var configPath = Path.Combine(env.ContentRootPath, "appsettings.json");
        var json = await File.ReadAllTextAsync(configPath, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options)
            ?? new Dictionary<string, object>();

        if (dict.TryGetValue("drivers", out var driversObj) && driversObj is JsonElement driversEl)
        {
            var driversDict = JsonSerializer.Deserialize<Dictionary<string, DriverConfig>>(driversEl.GetRawText(), options)
                ?? new Dictionary<string, DriverConfig>();

            if (driversDict.TryGetValue(driverName, out var driverConfig))
            {
                driverConfig.Enabled = enabled;
                driversDict[driverName] = driverConfig;
                dict["drivers"] = driversDict;
            }
        }

        var newJson = JsonSerializer.Serialize(dict, options);
        await File.WriteAllTextAsync(configPath, newJson, Encoding.UTF8);

        if (config is IConfigurationRoot root2)
            root2.Reload();
    }

    private class DriverConfig
    {
        public bool Enabled { get; set; }
    }
}
