using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions;
using DeviceHub.Devices.Contracts.Abstractions.Services;
using DeviceHub.Devices.IdCard.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.IdCard.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddIdCardService(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Drivers:IdCard:Enabled"))
            return services;

        var useMock = configuration.GetValue<bool>("Drivers:IdCard:Mock");
        if (useMock)
            services.AddSingleton<IIdCardService, MockIdCardService>();
        else
            services.AddSingleton<IIdCardService, IdCardService>();

        services.AddSingleton<IHardwareEndpointRegistrar>(sp =>
            (IHardwareEndpointRegistrar)sp.GetRequiredService<IIdCardService>());

        return services;
    }
}
