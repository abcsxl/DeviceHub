using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.Contracts;

/// <summary>
/// Extension methods for resolving driver service instances via DI.
/// </summary>
public static class HardwareServiceExtensions
{
    /// <summary>
    /// Resolves the first <see cref="IHardwareService"/> registration matching <typeparamref name="T"/>.
    /// </summary>
    public static T? GetHardwareService<T>(this IServiceProvider sp) where T : class
        => sp.GetServices<IHardwareService>().OfType<T>().FirstOrDefault();
}
