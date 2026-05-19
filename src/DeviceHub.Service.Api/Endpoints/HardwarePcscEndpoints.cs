using DeviceHub.Devices.Contracts;

namespace DeviceHub.Service.Api.Endpoints;

public static class HardwarePcscEndpoints
{
    public static WebApplication MapHardwarePcscEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/hardware/pcsc");

        group.MapGet("/readers", async (HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Problem(statusCode: 503, title: "PCSC 驱动未注册");

            var readers = await service.ListReadersAsync();
            return Results.Ok(new { readers });
        });

        group.MapGet("/readers/{name}", async (string name, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Problem(statusCode: 503, title: "PCSC 驱动未注册");

            var info = await service.GetReaderInfoAsync(name);
            return info.Name == name
                ? Results.Ok(info)
                : Results.NotFound(new { error = "CARD_NOT_PRESENT", message = $"Reader not found: {name}" });
        });

        group.MapGet("/readers/{name}/atr", async (string name, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Problem(statusCode: 503, title: "PCSC 驱动未注册");

            var atr = await service.GetAtrAsync(name);
            return atr != null
                ? Results.Ok(new { atr })
                : Results.NotFound(new { error = "CARD_NOT_PRESENT", message = "No card present in reader" });
        });

        group.MapPost("/transmit", async (TransmitRequest request, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<IPcscService>();
            if (service == null)
                return Results.Problem(statusCode: 503, title: "PCSC 驱动未注册");

            if (string.IsNullOrEmpty(request.ReaderName) || string.IsNullOrEmpty(request.Apdu))
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = "readerName and apdu are required" });

            if (request.Apdu.Length % 2 != 0)
                return Results.BadRequest(new { error = "INVALID_PARAMETERS", message = "apdu must be a hex string" });

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
