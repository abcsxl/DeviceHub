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
            ILoggerFactory loggerFactory,
            IStringLocalizer<Program> L) =>
        {
            var logger = loggerFactory.CreateLogger("Drivers");
            var entry = registry.Get(name);
            if (entry == null)
                return Results.NotFound(new { error = "DRIVER_NOT_FOUND", message = L["DriverNotFound", name] });

            entry.Enabled = true;
            await entry.Service.InitAsync();
            logger.LogInformation("驱动 {Name} 已启用", name);
            return Results.Ok(new { name, status = entry.Service.Status.ToString(), enabled = true });
        });

        app.MapPost("/api/drivers/{name}/disable", async (
            string name,
            DriverRegistry registry,
            ILoggerFactory loggerFactory,
            IStringLocalizer<Program> L) =>
        {
            var logger = loggerFactory.CreateLogger("Drivers");
            var entry = registry.Get(name);
            if (entry == null)
                return Results.NotFound(new { error = "DRIVER_NOT_FOUND", message = L["DriverNotFound", name] });

            entry.Enabled = false;
            await entry.Service.ShutdownAsync();
            logger.LogInformation("驱动 {Name} 已禁用", name);
            return Results.Ok(new { name, status = entry.Service.Status.ToString(), enabled = false });
        });
        return app;
    }
}
