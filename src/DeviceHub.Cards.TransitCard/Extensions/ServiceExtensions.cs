using DeviceHub.Devices.Contracts;
using DeviceHub.Cards.TransitCard.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Cards.TransitCard.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddTransitCardService(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Drivers:Pcsc:Enabled"))
            return services;

        var useMock = configuration.GetValue<bool>("Drivers:Pcsc:Mock");
        if (useMock)
            services.AddSingleton<ITransitCardService, MockTransitCardService>();
        else
            services.AddSingleton<ITransitCardService, TransitCardService>();

        return services;
    }
}
