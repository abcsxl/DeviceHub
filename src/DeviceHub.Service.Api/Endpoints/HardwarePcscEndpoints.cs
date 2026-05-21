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
                return Results.Problem(statusCode: 503, title: L["PcscDriverNotRegistered"]);

            var readers = await service.ListReadersAsync();
            return Results.Ok(new { readers });
        });

        group.MapGet("/readers/{name}", async (string name, HttpContext context, IStringLocalizer<Program> L) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Problem(statusCode: 503, title: L["PcscDriverNotRegistered"]);

            var info = await service.GetReaderInfoAsync(name);
            return info.Name == name
                ? Results.Ok(info)
                : Results.NotFound(new { error = "CARD_NOT_PRESENT", message = L["ReaderNotFound", name] });
        });

        group.MapGet("/readers/{name}/atr", async (string name, HttpContext context, IStringLocalizer<Program> L) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Problem(statusCode: 503, title: L["PcscDriverNotRegistered"]);

            var atr = await service.GetAtrAsync(name);
            return atr != null
                ? Results.Ok(new { atr })
                : Results.NotFound(new { error = "CARD_NOT_PRESENT", message = L["CardNotPresent"] });
        });

        group.MapPost("/transmit", async (TransmitRequest request, HttpContext context, IStringLocalizer<Program> L) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Problem(statusCode: 503, title: L["PcscDriverNotRegistered"]);

            if (string.IsNullOrEmpty(request.ReaderName) || string.IsNullOrEmpty(request.Apdu))
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = L["ReaderNameAndApduRequired"] });

            if (request.Apdu.Length % 2 != 0)
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = L["ApduMustBeHexString"] });

            var result = await service.TransmitAsync(request.ReaderName, request.Apdu);
            if (!result.Success)
            {
                var (code, status) = result.ErrorMessage?.Contains("卡片", StringComparison.Ordinal) == true
                    ? ("CARD_NOT_PRESENT", 404)
                    : ("HARDWARE_ERROR", 500);
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
