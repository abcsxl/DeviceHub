using DeviceHub.Devices.Contracts.Helpers;
using DeviceHub.Service.Api.Models;

namespace DeviceHub.Service.Api.Endpoints;

public static class LogsEndpoint
{
    public static WebApplication MapLogsEndpoint(this WebApplication app)
    {
        app.MapGet("/api/logs", (InMemoryLogProvider provider, string? level, int tail = 100) =>
        {
            if (tail is < 1 or > 10000)
                tail = 100;

            var logs = provider.GetLogs(level, tail);
            return ApiResponseHelper.Ok(new { total = logs.Count, entries = logs });
        });

        app.MapGet("/api/logs/levels", (InMemoryLogProvider provider) =>
        {
            return ApiResponseHelper.Ok(provider.GetLogLevels());
        });

        app.MapPut("/api/logs/levels", (LogLevelOverride override_, InMemoryLogProvider provider) =>
        {
            if (string.IsNullOrEmpty(override_.Category) || string.IsNullOrEmpty(override_.Level))
                return ApiResponseHelper.BadRequest("INVALID_PARAMETERS", "category and level are required");

            if (!Enum.TryParse<LogLevel>(override_.Level, true, out var level))
                return ApiResponseHelper.BadRequest("INVALID_PARAMETERS", $"Invalid log level: {override_.Level}");

            provider.SetLogLevel(override_.Category, level);
            return ApiResponseHelper.Ok(new { category = override_.Category, level = level.ToString() });
        });

        app.MapDelete("/api/logs/levels", (string category, InMemoryLogProvider provider) =>
        {
            if (string.IsNullOrEmpty(category))
                return ApiResponseHelper.BadRequest("INVALID_PARAMETERS", "category is required");

            provider.RemoveLogLevel(category);
            return ApiResponseHelper.Ok(new { category, removed = true });
        });

        return app;
    }

    internal record LogLevelOverride(string Category, string Level);
}
