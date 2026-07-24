using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions.Services;
using DeviceHub.Devices.Contracts.Extensions;
using DeviceHub.Devices.Contracts.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.Printer.Endpoints;

internal static class PrinterEndpoint
{
    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hardware/printer");

        group.MapGet("/printers", async (HttpContext context) =>
        {
            var service = context.RequestServices.CheckHardwareService<IPrinterService>(out var error);
if (service == null) return error;

            var printers = await service.GetPrintersAsync();
            return ApiResponseHelper.Ok(new { printers });
        });

        group.MapPost("/print", async (PrintRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.CheckHardwareService<IPrinterService>(out var error);
if (service == null) return error;

            if (string.IsNullOrEmpty(req.Text))
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "text is required", 400);

            try
            {
                var ok = await service.PrintTextAsync(req.Text, req.PrinterName);
                if (!ok)
                    return ApiResponseHelper.Error("HARDWARE_ERROR", "Print failed", 500);
                return ApiResponseHelper.Ok();
            }
            catch (InvalidOperationException ex) when (ex.Message == "PRINTER_NOT_FOUND")
            {
                return ApiResponseHelper.Error("PRINTER_NOT_FOUND", "No printer available", 404);
            }
        });

        group.MapPost("/print-raw", async (PrintRawRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.CheckHardwareService<IPrinterService>(out var error);
if (service == null) return error;

            if (string.IsNullOrEmpty(req.Data) || req.Data.Length % 2 != 0)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "Data must be a valid hex string", 400);

            var data = Convert.FromHexString(req.Data);
            try
            {
                var ok = await service.PrintRawAsync(data, req.PrinterName);
                if (!ok)
                    return ApiResponseHelper.Error("HARDWARE_ERROR", "Print failed", 500);
                return ApiResponseHelper.Ok();
            }
            catch (InvalidOperationException ex) when (ex.Message == "PRINTER_NOT_FOUND")
            {
                return ApiResponseHelper.Error("PRINTER_NOT_FOUND", "No printer available", 404);
            }
        });
    }

    internal record PrintRequest(string Text, string? PrinterName);
    internal record PrintRawRequest(string Data, string? PrinterName);
}
