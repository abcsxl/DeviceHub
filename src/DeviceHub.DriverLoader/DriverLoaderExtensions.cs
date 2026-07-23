using System.Reflection;
using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions;
using DeviceHub.Devices.Contracts.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeviceHub.DriverLoader;

public static class DriverLoaderExtensions
{
    public static IServiceCollection LoadExternalDrivers(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger? logger = null)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "drivers");
        if (!Directory.Exists(dir))
        {
            logger?.LogDebug("External drivers directory not found: {Dir}", dir);
            return services;
        }

        foreach (var dllPath in Directory.GetFiles(dir, "*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(dllPath);
                foreach (var type in asm.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface)
                        continue;

                    if (typeof(IHardwareService).IsAssignableFrom(type))
                    {
                        var driverName = type.GetCustomAttribute<DriverNameAttribute>()?.Name
                            ?? type.Name.Replace("Service", "");

                        var enabled = configuration.GetValue<bool?>($"Drivers:{driverName}:Enabled");
                        if (enabled == false)
                        {
                            logger?.LogInformation("External driver {Name} not enabled, skipping", driverName);
                            continue;
                        }

                        services.AddSingleton(type);
                        services.AddSingleton(typeof(IHardwareService), sp => sp.GetRequiredService(type));

                        if (typeof(IHardwareEndpointRegistrar).IsAssignableFrom(type))
                        {
                            services.AddSingleton(typeof(IHardwareEndpointRegistrar), sp => sp.GetRequiredService(type));
                            logger?.LogInformation("External driver {Name} registered endpoint registrar", driverName);
                        }

                        logger?.LogInformation("External driver {Name} loaded from {Dll}", driverName, Path.GetFileName(dllPath));
                    }

                    if (typeof(IHardwareWebSocketHandler).IsAssignableFrom(type))
                    {
                        services.AddSingleton(type);
                        services.AddSingleton(typeof(IHardwareWebSocketHandler), sp => sp.GetRequiredService(type));
                        logger?.LogInformation("External driver registered WebSocket handler: {Type}", type.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to load external driver DLL: {Dll}", dllPath);
            }
        }

        return services;
    }
}
