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
        return app;
    }
}
