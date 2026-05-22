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
        }
        else
        {
            services.AddSingleton<IPcscService, PcscService>();
        }

        return services;
    }
}
