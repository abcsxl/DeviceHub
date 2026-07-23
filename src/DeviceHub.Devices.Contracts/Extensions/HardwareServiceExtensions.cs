using DeviceHub.Devices.Contracts.Abstractions.Services;
using DeviceHub.Devices.Contracts.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.Contracts.Extensions;

public static class HardwareServiceExtensions
{
    public static T? GetHardwareService<T>(this IServiceProvider sp) where T : class
        => sp.GetServices<IHardwareService>().OfType<T>().FirstOrDefault();

    public static T? CheckHardwareService<T>(this IServiceProvider sp, out IResult? error, string? displayName = null) where T : class
    {
        var service = sp.GetHardwareService<T>();
        if (service == null)
        {
            error = ApiResponseHelper.Error("DRIVER_NOT_FOUND", $"{(displayName ?? typeof(T).Name)} not registered", 503);
            return null;
        }

        if (service is IHardwareService hwSvc && hwSvc.Status != HardwareStatus.Running)
        {
            error = ApiResponseHelper.Error("SERVICE_NOT_RUNNING", $"{hwSvc.Name} is not running", 503);
            return null;
        }

        error = null;
        return service;
    }
}
