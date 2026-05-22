using DeviceHub.Devices.Contracts;
using Microsoft.Extensions.Localization;

namespace DeviceHub.Service.Api.Endpoints;

public static class HardwarePcscEndpoints
{
    public static WebApplication MapHardwarePcscEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/hardware/pcsc");

        group.MapGet("/readers", async (HttpContext context, IStringLocalizer<Program> L) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = L["PcscDriverNotRegistered"].Value }, statusCode: 503);

            var readers = await service.ListReadersAsync();
            return Results.Ok(new { readers });
        });

        group.MapGet("/readers/{name}", async (string name, HttpContext context, IStringLocalizer<Program> L) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = L["PcscDriverNotRegistered"].Value }, statusCode: 503);

            var readers = await service.ListReadersAsync();
            if (readers.All(r => r.Name != name))
                return Results.NotFound(new { error = "READER_NOT_FOUND", message = L["ReaderNotFound", name].Value });

            var info = await service.GetReaderInfoAsync(name);
            if (!info.IsCardPresent)
                return Results.NotFound(new { error = "CARD_NOT_PRESENT", message = L["CardNotPresent"].Value });

            return Results.Ok(info);
        });

        group.MapGet("/readers/{name}/atr", async (string name, HttpContext context, IStringLocalizer<Program> L) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = L["PcscDriverNotRegistered"] }, statusCode: 503);

            var atr = await service.GetAtrAsync(name);
            return atr != null
                ? Results.Ok(new { atr })
                : Results.NotFound(new { error = "CARD_NOT_PRESENT", message = L["CardNotPresent"].Value });
        });

        group.MapPost("/transmit", async (TransmitRequest request, HttpContext context, IStringLocalizer<Program> L) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = L["PcscDriverNotRegistered"].Value }, statusCode: 503);

            if (string.IsNullOrEmpty(request.ReaderName) || string.IsNullOrEmpty(request.Apdu))
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = L["ReaderNameAndApduRequired"].Value });

            if (request.Apdu.Length % 2 != 0)
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = L["ApduMustBeHexString"].Value });

            var result = await service.TransmitAsync(request.ReaderName, request.Apdu);
            if (!result.Success)
            {
                var code = result.ErrorCode ?? "HARDWARE_ERROR";
                var status = code switch
                {
                    "CARD_NOT_PRESENT" => 404,
                    "READER_NOT_FOUND" => 404,
                    "INVALID_PARAMETERS" => 400,
                    _ => 500
                };
                return Results.Json(new { error = code, message = result.ErrorMessage }, statusCode: status);
            }

            return Results.Ok(new
            {
                sw1 = result.Sw1,
                sw2 = result.Sw2,
                responseData = result.ResponseData,
                success = result.Success
            });
        });

        return app;
    }

    private record TransmitRequest(string? ReaderName, string? Apdu);
}
