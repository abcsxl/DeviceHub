using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions.Services;
using DeviceHub.Devices.Contracts.Extensions;
using DeviceHub.Devices.Contracts.Helpers;
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
            var service = context.RequestServices.CheckHardwareService<IPcscService>(out var error);
if (service == null) return error;

            var readers = await service.ListReadersAsync();
            return ApiResponseHelper.Ok(new { readers });
        });

        group.MapGet("/readers/{name}", async (string name, HttpContext context) =>
        {
            var service = context.RequestServices.CheckHardwareService<IPcscService>(out var error);
if (service == null) return error;

            var readers = await service.ListReadersAsync();
            if (readers.All(r => r.Name != name))
                return ApiResponseHelper.NotFound("READER_NOT_FOUND", $"Reader not found: {name}");

            var info = await service.GetReaderInfoAsync(name);
            if (!info.IsCardPresent)
                return ApiResponseHelper.NotFound("CARD_NOT_PRESENT", "No card present in reader");

            return ApiResponseHelper.Ok(info);
        });

        group.MapGet("/readers/{name}/atr", async (string name, HttpContext context) =>
        {
            var service = context.RequestServices.CheckHardwareService<IPcscService>(out var error);
if (service == null) return error;

            var atr = await service.GetAtrAsync(name);
            return atr != null
                ? ApiResponseHelper.Ok(new { atr })
                : ApiResponseHelper.NotFound("CARD_NOT_PRESENT", "No card present in reader");
        });

        group.MapPost("/transmit", async (TransmitRequest request, HttpContext context) =>
        {
            var service = context.RequestServices.CheckHardwareService<IPcscService>(out var error);
if (service == null) return error;

            if (string.IsNullOrEmpty(request.ReaderName) || string.IsNullOrEmpty(request.Apdu))
                return ApiResponseHelper.BadRequest("INVALID_PARAMETERS", "readerName and apdu are required");

            if (request.Apdu.Length % 2 != 0)
                return ApiResponseHelper.BadRequest("INVALID_PARAMETERS", "apdu must be a hex string");

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
                var msg = result.ErrorMessage ?? "Unknown error";
                if (result.Sw1 != null)
                    msg += $" (SW={result.Sw1}{result.Sw2})";
                return ApiResponseHelper.Error(code, msg, status);
            }

            if (result.Sw1 != "90" || result.Sw2 != "00")
            {
                var (code, status) = SwCodeHelper.ClassifySw(result.Sw1!, result.Sw2!);
                return ApiResponseHelper.Error(code, $"Card returned error (SW={result.Sw1}{result.Sw2})", status);
            }

            return ApiResponseHelper.Ok(new
            {
                sw1 = result.Sw1,
                sw2 = result.Sw2,
                responseData = result.ResponseData
            });
        });
    }

    internal record TransmitRequest(string? ReaderName, string? Apdu);
}
