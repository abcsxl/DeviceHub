using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Printer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.Printer.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddPrinterService(
        this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Drivers:Printer:Enabled"))
            return services;

        var useMock = configuration.GetValue<bool>("Drivers:Printer:Mock");
        if (useMock)
            services.AddSingleton<IPrinterService, MockPrinterService>();
        else
            services.AddSingleton<IPrinterService, PrinterService>();

        services.AddSingleton<IHardwareEndpointRegistrar>(sp =>
            (IHardwareEndpointRegistrar)sp.GetRequiredService<IPrinterService>());

        return services;
    }
}
