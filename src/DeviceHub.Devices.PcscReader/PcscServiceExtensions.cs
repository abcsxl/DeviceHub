using DeviceHub.Devices.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.PcscReader;

public static class PcscServiceExtensions
{
    public static IServiceCollection AddPcscService(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Drivers:Pcsc:Enabled"))
            return services;

        services.AddSingleton<IPcscService, PcscService>();

        return services;
    }
}
