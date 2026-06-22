using Microsoft.AspNetCore.Routing;

namespace DeviceHub.Devices.Contracts;

public interface IHardwareEndpointRegistrar
{
    void MapEndpoints(IEndpointRouteBuilder app);
}
