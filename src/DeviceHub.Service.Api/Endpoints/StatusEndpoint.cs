using DeviceHub.Devices.Contracts;
using DeviceHub.Service.Api.Models;
using DeviceHub.Service.Api.WebSocket;

namespace DeviceHub.Service.Api.Endpoints;

public static class StatusEndpoint
{
    public static WebApplication MapStatusEndpoint(this WebApplication app)
    {
        app.MapGet("/api/status", (ServiceState state, DriverRegistry registry, WebSocketHandler wsHandler) =>
        {
            var drivers = registry.GetAll();
            return new ServiceInfo(
                state.Version,
                $"{state.Platform}/{state.Architecture}",
                state.HttpPort,
                state.StartTime,
                DateTime.UtcNow - state.StartTime,
                wsHandler.ConnectionCount,
                drivers
            );
        });
        return app;
    }
}
