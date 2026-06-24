using DeviceHub.Devices.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Devices.Printer.Helpers;

internal static class PrinterEndpointHelper
{
    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hardware/printer");

        group.MapGet("/printers", async (HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPrinterService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "Printer service not available" }, statusCode: 503);

            var printers = await service.GetPrintersAsync();
            return Results.Ok(new { printers });
        });

        group.MapPost("/print", async (PrintRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPrinterService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "Printer service not available" }, statusCode: 503);

            if (string.IsNullOrEmpty(req.Text))
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "text is required" }, statusCode: 400);

            var ok = await service.PrintTextAsync(req.Text, req.PrinterName);
            if (!ok)
                return Results.Json(new { error = "PRINT_FAILED", message = "Print failed" }, statusCode: 500);

            return Results.Ok(new { success = true });
        });

        group.MapPost("/print-raw", async (PrintRawRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPrinterService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "Printer service not available" }, statusCode: 503);

            if (string.IsNullOrEmpty(req.Data))
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "data (hex) is required" }, statusCode: 400);

            var data = Convert.FromHexString(req.Data);
            var ok = await service.PrintRawAsync(data, req.PrinterName);
            if (!ok)
                return Results.Json(new { error = "PRINT_FAILED", message = "Print failed" }, statusCode: 500);

            return Results.Ok(new { success = true });
        });
    }

    internal record PrintRequest(string Text, string? PrinterName);
    internal record PrintRawRequest(string Data, string? PrinterName);
}
