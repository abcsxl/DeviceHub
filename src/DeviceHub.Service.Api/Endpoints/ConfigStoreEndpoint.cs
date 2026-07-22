using DeviceHub.Devices.Contracts.Helpers;
using DeviceHub.Service.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace DeviceHub.Service.Api.Endpoints;

public static class ConfigStoreEndpoint
{
    public static WebApplication MapConfigStoreEndpoint(this WebApplication app)
    {
        var group = app.MapGroup("/api/config-store");

        group.MapGet("/", async (ConfigStoreService store) =>
        {
            var entries = await store.GetAllAsync();
            return ApiResponseHelper.Ok(new { entries });
        });

        group.MapGet("/{key}", async (string key, ConfigStoreService store, IStringLocalizer<Program> L) =>
        {
            var value = await store.GetAsync(key);
            return value != null
                ? ApiResponseHelper.Ok(new { key, value })
                : ApiResponseHelper.NotFound("NOT_FOUND", L["ConfigKeyNotFound", key].Value);
        });

        group.MapPut("/{key}", async (string key, SetConfigValueRequest req, ConfigStoreService store) =>
        {
            await store.SetAsync(key, req.Value);
            return ApiResponseHelper.Ok(new { key, value = req.Value });
        });

        group.MapDelete("/{key}", async (string key, ConfigStoreService store, IStringLocalizer<Program> L) =>
        {
            var deleted = await store.DeleteAsync(key);
            return deleted
                ? ApiResponseHelper.Ok()
                : ApiResponseHelper.NotFound("NOT_FOUND", L["ConfigKeyNotFound", key].Value);
        });

        group.MapDelete("/", async (ConfigStoreService store) =>
        {
            await store.ClearAsync();
            return ApiResponseHelper.Ok();
        });

        return app;
    }
}

internal record SetConfigValueRequest(string Value);
