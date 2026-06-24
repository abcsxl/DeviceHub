using DeviceHub.Service.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Service.Api.Endpoints;

public static class ConfigStoreEndpoint
{
    public static WebApplication MapConfigStoreEndpoint(this WebApplication app)
    {
        var group = app.MapGroup("/api/config-store");

        group.MapGet("/", async (ConfigStoreService store) =>
        {
            var entries = await store.GetAllAsync();
            return Results.Ok(new { entries });
        });

        group.MapGet("/{key}", async (string key, ConfigStoreService store) =>
        {
            var value = await store.GetAsync(key);
            return value != null
                ? Results.Ok(new { key, value })
                : Results.NotFound(new { error = "NOT_FOUND", message = $"Key '{key}' not found" });
        });

        group.MapPut("/{key}", async (string key, SetConfigValueRequest req, ConfigStoreService store) =>
        {
            await store.SetAsync(key, req.Value);
            return Results.Ok(new { success = true, key, value = req.Value });
        });

        group.MapDelete("/{key}", async (string key, ConfigStoreService store) =>
        {
            var deleted = await store.DeleteAsync(key);
            return deleted
                ? Results.Ok(new { success = true })
                : Results.NotFound(new { error = "NOT_FOUND", message = $"Key '{key}' not found" });
        });

        group.MapDelete("/", async (ConfigStoreService store) =>
        {
            await store.ClearAsync();
            return Results.Ok(new { success = true });
        });

        return app;
    }
}

internal record SetConfigValueRequest(string Value);
