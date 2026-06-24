using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.PcscReader.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.PcscReader.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddPcscService(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Drivers:Pcsc:Enabled"))
            return services;

        var useMock = configuration.GetValue<bool>("Drivers:Pcsc:Mock");
        if (useMock)
        {
            services.AddSingleton<IPcscService, MockPcscService>();
            services.AddSingleton<IHardwareEndpointRegistrar>(sp =>
                (IHardwareEndpointRegistrar)sp.GetRequiredService<IPcscService>());
        }
        else
        {
            services.AddSingleton<IPcscService, PcscService>();
            services.AddSingleton<IHardwareEndpointRegistrar>(sp =>
                (IHardwareEndpointRegistrar)sp.GetRequiredService<IPcscService>());
        }

        return services;
    }
}
