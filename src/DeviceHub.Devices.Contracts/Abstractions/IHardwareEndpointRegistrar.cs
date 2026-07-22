using Microsoft.AspNetCore.Routing;

namespace DeviceHub.Devices.Contracts.Abstractions;

public interface IHardwareEndpointRegistrar
{
    void MapEndpoints(IEndpointRouteBuilder app);
}
