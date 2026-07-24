using DeviceHub.Cards.TransitCard.Models.Requests;
using DeviceHub.Cards.TransitCard.Models.Responses;
using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Cards.TransitCard.Endpoints;

internal static class TransitCardEndpoint
{
    private const string CARD_NOT_PRESENT = "CARD_NOT_PRESENT";
    private const string READER_NOT_FOUND = "READER_NOT_FOUND";
    private const string SERVICE_NOT_RUNNING = "SERVICE_NOT_RUNNING";
    private const string HARDWARE_ERROR = "HARDWARE_ERROR";

    private static IResult HandleError(Exception ex)
    {
        if (ex is InvalidOperationException ioe)
        {
            var (code, msg) = ErrorCodeHelper.ParseTransmitError(ioe.Message);
            return ApiResponseHelper.Error(code, msg, ErrorCodeHelper.GetHttpStatus(code));
        }

        var (code2, status2) = SwCodeHelper.ClassifySwFromMessage(ex.Message);
        return ApiResponseHelper.Error(code2, ex.Message, status2);
    }

    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hardware/transitcard");

        group.MapGet("/readers", async (HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            var readers = await service.GetAvailableReadersAsync();
            return ApiResponseHelper.Ok(new { readers });
        });

        group.MapPost("/reset", async (string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            try
            {
                var atr = await service.ResetCardAsync(readerName);
                if (atr == null)
                    return ApiResponseHelper.Error(SERVICE_NOT_RUNNING, "Reset failed: PCSC service not running", 503);
                return ApiResponseHelper.Ok(new { atr });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        });

        group.MapGet("/info", async (string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            try
            {
                var info = await service.ReadCardInfoAsync(readerName);
                return ApiResponseHelper.Ok(info);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        });

        group.MapGet("/balance", async (string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            try
            {
                var balance = await service.ReadBalanceAsync(readerName);
                return ApiResponseHelper.Ok(balance);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        });

        group.MapGet("/transactions", async (int? count, string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            try
            {
                var records = await service.ReadTransactionsAsync(count ?? 10, readerName);
                return ApiResponseHelper.Ok(new { records });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        });

        group.MapPost("/recharge/init", async (RechargeInitRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            if (req.Amount <= 0)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "Recharge amount must be greater than 0", 400);

            try
            {
                var result = await service.RechargeInitAsync(req.Amount, req.ReaderName);
                return ApiResponseHelper.Ok(result);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        });

        group.MapPost("/recharge/execute", async (RechargeExecuteRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            if (string.IsNullOrEmpty(req.SessionId) || string.IsNullOrEmpty(req.MacSignature))
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "sessionId and macSignature are required", 400);

            var result = await service.RechargeExecuteAsync(req.SessionId, req.MacSignature);
            if (!result.Success)
            {
                var (code, status) = SwCodeHelper.ClassifySw(result.Sw1 ?? "FF", result.Sw2 ?? "FF");
                return ApiResponseHelper.Error(code, result.ErrorMessage ?? "Recharge execution failed", status);
            }

            return ApiResponseHelper.Ok();
        });

        group.MapPost("/consume/init", async (ConsumeInitRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            if (req.Dealflag < 1 || req.Dealflag > 2)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "dealflag must be 1 or 2", 400);
            if (req.Keyindex < 0 || req.Keyindex > 255)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "keyindex must be 0-255", 400);
            if (req.Amount <= 0)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "Amount must be greater than 0", 400);
            if (string.IsNullOrEmpty(req.Termainno) || req.Termainno.Length != 12)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "termainno must be 12 hex chars (6 bytes)", 400);

            try
            {
                var result = await service.ConsumeInitAsync(req.Dealflag, req.Keyindex, req.Amount, req.Termainno, req.ReaderName);
                return ApiResponseHelper.Ok(result);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        });

        group.MapPost("/consume/capp-init", async (ConsumeInitRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            if (req.Dealflag < 1 || req.Dealflag > 2)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "dealflag must be 1 or 2", 400);
            if (req.Keyindex < 0 || req.Keyindex > 255)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "keyindex must be 0-255", 400);
            if (req.Amount <= 0)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "Amount must be greater than 0", 400);
            if (string.IsNullOrEmpty(req.Termainno) || req.Termainno.Length != 12)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "termainno must be 12 hex chars (6 bytes)", 400);

            try
            {
                var result = await service.ConsumeCappInitAsync(req.Dealflag, req.Keyindex, req.Amount, req.Termainno, req.ReaderName);
                return ApiResponseHelper.Ok(result);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        });

        group.MapPost("/consume/execute", async (ConsumeExecuteRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return ApiResponseHelper.Error("DRIVER_NOT_FOUND", "TransitCard not registered", 503);

            if (string.IsNullOrEmpty(req.SessionId))
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "sessionId is required", 400);
            if (string.IsNullOrEmpty(req.Dealtime) || req.Dealtime.Length != 14)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "dealtime must be 14 hex chars (yyyymmddHHmiss BCD)", 400);
            if (string.IsNullOrEmpty(req.Mac1) || req.Mac1.Length != 8)
                return ApiResponseHelper.Error("INVALID_PARAMETERS", "mac1 must be 8 hex chars (4 bytes)", 400);

            var result = await service.ConsumeExecuteAsync(req.SessionId, req.Termdealno, req.Dealtime, req.Mac1);
            if (!result.Success)
            {
                var msg = result.ErrorMessage ?? "Consume execution failed";
                var (code, status) = SwCodeHelper.ClassifySw(result.Sw1 ?? "FF", result.Sw2 ?? "FF");
                if (result.Sw1 != null)
                    msg += $" (SW={result.Sw1}{result.Sw2})";
                return ApiResponseHelper.Error(code, msg, status);
            }

            return ApiResponseHelper.Ok();
        });
    }

}
