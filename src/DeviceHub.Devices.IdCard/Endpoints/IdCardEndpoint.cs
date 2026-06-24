using DeviceHub.Devices.Contracts;
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
            var service = context.RequestServices.GetService<IIdCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "IdCard service not available" }, statusCode: 503);

            var readers = await service.GetReadersAsync();
            return Results.Ok(new { readers });
        });

        group.MapPost("/read", async (ReadRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IIdCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "IdCard service not available" }, statusCode: 503);

            var info = await service.ReadCardAsync(req.ReaderName);
            if (info == null)
                return Results.Json(new { error = "CARD_NOT_FOUND", message = "No ID card detected" }, statusCode: 404);

            return Results.Ok(info);
        });

        group.MapPost("/read-photo", async (ReadRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IIdCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "IdCard service not available" }, statusCode: 503);

            var photo = await service.ReadPhotoAsync(req.ReaderName);
            if (photo == null)
                return Results.Json(new { error = "PHOTO_NOT_FOUND", message = "No photo data" }, statusCode: 404);

            return Results.File(photo, "image/jpeg");
        });
    }

    internal record ReadRequest(string? ReaderName);
}
