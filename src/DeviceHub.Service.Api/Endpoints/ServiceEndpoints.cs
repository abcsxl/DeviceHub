namespace DeviceHub.Service.Api.Endpoints;

public static class ServiceEndpoints
{
    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        app.MapPost("/api/service/restart", (
            IHostApplicationLifetime lifetime,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Service");
            logger.LogWarning("服务即将重启");
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                lifetime.StopApplication();
            });
            return Results.Accepted((string?)null, new { message = "服务将在 1 秒后重启" });
        });
        return app;
    }
}
