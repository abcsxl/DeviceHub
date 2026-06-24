namespace DeviceHub.Service.Api.Endpoints;

public static class HealthEndpoint
{
    public static WebApplication MapHealthEndpoint(this WebApplication app)
    {
        app.MapGet("/api/health", () =>
        {
            return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        });
        return app;
    }
}
