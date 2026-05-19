using DeviceHub.Devices.Contracts;
using DeviceHub.Service.Api.Models;
using DeviceHub.Service.Api.WebSocket;

namespace DeviceHub.Service.Api.Endpoints;

public static class StatusEndpoints
{
    public static WebApplication MapStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/api/status", (ServiceState state, DriverRegistry registry) =>
        {
            var drivers = registry.GetAll();
            return new ServiceInfo(
                state.Version,
                $"{state.Platform}/{state.Architecture}",
                state.StartTime,
                DateTime.UtcNow - state.StartTime,
                WebSocketHandler.ConnectionCount,
                drivers
            );
        });
        return app;
    }
}
