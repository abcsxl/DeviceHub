namespace DeviceHub.Service.Api.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", () =>
        {
            return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        });
        return app;
    }
}
