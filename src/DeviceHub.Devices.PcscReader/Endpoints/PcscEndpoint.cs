using DeviceHub.Devices.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.PcscReader.Endpoints;

internal static class PcscEndpoint
{
    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hardware/pcsc");

        group.MapGet("/readers", async (HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "PCSC not registered" }, statusCode: 503);

            var readers = await service.ListReadersAsync();
            return Results.Ok(new { readers });
        });

        group.MapGet("/readers/{name}", async (string name, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "PCSC not registered" }, statusCode: 503);

            var readers = await service.ListReadersAsync();
            if (readers.All(r => r.Name != name))
                return Results.NotFound(new { error = "READER_NOT_FOUND", message = $"Reader not found: {name}" });

            var info = await service.GetReaderInfoAsync(name);
            if (!info.IsCardPresent)
                return Results.NotFound(new { error = "CARD_NOT_PRESENT", message = "No card present in reader" });

            return Results.Ok(info);
        });

        group.MapGet("/readers/{name}/atr", async (string name, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "PCSC not registered" }, statusCode: 503);

            var atr = await service.GetAtrAsync(name);
            return atr != null
                ? Results.Ok(new { atr })
                : Results.NotFound(new { error = "CARD_NOT_PRESENT", message = "No card present in reader" });
        });

        group.MapPost("/transmit", async (TransmitRequest request, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "PCSC not registered" }, statusCode: 503);

            if (string.IsNullOrEmpty(request.ReaderName) || string.IsNullOrEmpty(request.Apdu))
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = "readerName and apdu are required" });

            if (request.Apdu.Length % 2 != 0)
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = "apdu must be a hex string" });

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
                var msg = result.ErrorMessage;
                if (result.Sw1 != null)
                    msg += $" (SW={result.Sw1}{result.Sw2})";
                return Results.Json(new { error = code, message = msg, sw1 = result.Sw1, sw2 = result.Sw2 }, statusCode: status);
            }

            if (result.Sw1 != "90" || result.Sw2 != "00")
                return Results.Json(new { error = "HARDWARE_ERROR", message = $"Card returned error (SW={result.Sw1}{result.Sw2})" }, statusCode: 500);

            return Results.Ok(new
            {
                sw1 = result.Sw1,
                sw2 = result.Sw2,
                responseData = result.ResponseData,
                success = result.Success
            });
        });
    }

    internal record TransmitRequest(string? ReaderName, string? Apdu);
}
