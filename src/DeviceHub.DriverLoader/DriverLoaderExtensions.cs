using System.Reflection;
using DeviceHub.Devices.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeviceHub.DriverLoader;

[AttributeUsage(AttributeTargets.Class)]
public class DriverNameAttribute : Attribute
{
    public string Name { get; }
    public DriverNameAttribute(string name) => Name = name;
}

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
                    if (type.IsAbstract || !typeof(IHardwareService).IsAssignableFrom(type))
                        continue;

                    var driverName = type.GetCustomAttribute<DriverNameAttribute>()?.Name
                        ?? type.Name.Replace("Service", "");

                    if (!configuration.GetValue<bool>($"Drivers:{driverName}:Enabled"))
                    {
                        logger?.LogInformation("External driver {Name} not enabled, skipping", driverName);
                        continue;
                    }

                    services.AddSingleton(typeof(IHardwareService), type);
                    logger?.LogInformation("External driver {Name} loaded from {Dll}", driverName, Path.GetFileName(dllPath));
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
