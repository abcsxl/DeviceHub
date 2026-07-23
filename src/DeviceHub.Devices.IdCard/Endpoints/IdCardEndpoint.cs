using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions.Services;
using DeviceHub.Devices.Contracts.Extensions;
using DeviceHub.Devices.Contracts.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.IdCard.Endpoints;

internal static class IdCardEndpoint
{
    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hardware/id-card");

        group.MapGet("/readers", async (HttpContext context) =>
        {
            var service = context.RequestServices.CheckHardwareService<IIdCardService>(out var error);
if (service == null) return error;

            var readers = await service.GetReadersAsync();
            return ApiResponseHelper.Ok(new { readers });
        });

        group.MapPost("/read", async (ReadRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.CheckHardwareService<IIdCardService>(out var error);
if (service == null) return error;

            var info = await service.ReadCardAsync(req.ReaderName);
            if (info == null)
                return ApiResponseHelper.Error("CARD_NOT_FOUND", "No ID card detected", 404);

            return ApiResponseHelper.Ok(info);
        });

        group.MapPost("/read-photo", async (ReadRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.CheckHardwareService<IIdCardService>(out var error);
if (service == null) return error;

            var photo = await service.ReadPhotoAsync(req.ReaderName);
            if (photo == null)
                return ApiResponseHelper.Error("PHOTO_NOT_FOUND", "No photo data", 404);

            return Results.File(photo, "image/jpeg");
        });
    }

    internal record ReadRequest(string? ReaderName);
}
