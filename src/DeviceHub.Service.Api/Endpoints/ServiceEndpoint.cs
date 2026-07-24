using DeviceHub.Devices.Contracts.Helpers;

namespace DeviceHub.Service.Api.Endpoints;

public static class ServiceEndpoint
{
    public static WebApplication MapServiceEndpoint(this WebApplication app)
    {
        app.MapPost("/api/service/restart", (
            IHostApplicationLifetime lifetime,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Service");
            logger.LogWarning("Service is about to restart");
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                lifetime.StopApplication();
            });
            return ApiResponseHelper.Accepted(new { message = "Service will restart in 1 second" });
        });
        return app;
    }
}
