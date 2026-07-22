using DeviceHub.Devices.Contracts.Helpers;
using Microsoft.Extensions.Localization;

namespace DeviceHub.Service.Api.Endpoints;

public static class ServiceEndpoint
{
    public static WebApplication MapServiceEndpoint(this WebApplication app)
    {
        app.MapPost("/api/service/restart", (
            IHostApplicationLifetime lifetime,
            ILoggerFactory loggerFactory,
            IStringLocalizer<Program> L) =>
        {
            var logger = loggerFactory.CreateLogger("Service");
            logger.LogWarning("Service is about to restart");
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                lifetime.StopApplication();
            });
            return ApiResponseHelper.Accepted(new { message = L["ServiceRestarting"].Value });
        });
        return app;
    }
}
