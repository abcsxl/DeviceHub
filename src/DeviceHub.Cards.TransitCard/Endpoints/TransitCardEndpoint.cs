using DeviceHub.Devices.Contracts;
using DeviceHub.Cards.TransitCard.Models.Requests;
using DeviceHub.Cards.TransitCard.Models.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceHub.Cards.TransitCard.Endpoints;

internal static class TransitCardEndpoint
{
    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hardware/transitcard");

        group.MapGet("/readers", async (HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            var readers = await service.GetAvailableReadersAsync();
            return Results.Ok(new { readers });
        });

        group.MapPost("/reset", async (string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            try
            {
                var atr = await service.ResetCardAsync(readerName);
                if (atr == null)
                    return Results.Json(new { error = "CARD_NOT_PRESENT", message = "Reset failed, card not found" }, statusCode: 404);
                return Results.Ok(new { atr });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "HARDWARE_ERROR", message = ex.Message }, statusCode: 500);
            }
        });

        group.MapGet("/info", async (string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            try
            {
                var info = await service.ReadCardInfoAsync(readerName);
                return Results.Ok(info);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "HARDWARE_ERROR", message = ex.Message }, statusCode: 500);
            }
        });

        group.MapGet("/balance", async (string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            try
            {
                var balance = await service.ReadBalanceAsync(readerName);
                return Results.Ok(balance);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "HARDWARE_ERROR", message = ex.Message }, statusCode: 500);
            }
        });

        group.MapGet("/transactions", async (int? count, string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            try
            {
                var records = await service.ReadTransactionsAsync(count ?? 10, readerName);
                return Results.Ok(new { records });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "HARDWARE_ERROR", message = ex.Message }, statusCode: 500);
            }
        });

        group.MapPost("/recharge/init", async (RechargeInitRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            if (req.Amount <= 0)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "Recharge amount must be greater than 0" }, statusCode: 400);

            try
            {
                var result = await service.RechargeInitAsync(req.Amount, req.ReaderName);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "HARDWARE_ERROR", message = ex.Message }, statusCode: 500);
            }
        });

        group.MapPost("/recharge/execute", async (RechargeExecuteRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            if (string.IsNullOrEmpty(req.SessionId) || string.IsNullOrEmpty(req.MacSignature))
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "sessionId and macSignature are required" }, statusCode: 400);

            var result = await service.RechargeExecuteAsync(req.SessionId, req.MacSignature);
            if (!result.Success)
                return Results.Json(new { error = "HARDWARE_ERROR", message = result.ErrorMessage ?? "Recharge execution failed" }, statusCode: 500);

            return Results.Ok(new { sw1 = result.Sw1, sw2 = result.Sw2, success = true });
        });

        group.MapPost("/consume/init", async (ConsumeInitRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            if (req.Dealflag < 1 || req.Dealflag > 2)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "dealflag must be 1 or 2" }, statusCode: 400);
            if (req.Keyindex < 0 || req.Keyindex > 255)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "keyindex must be 0-255" }, statusCode: 400);
            if (req.Amount <= 0)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "Amount must be greater than 0" }, statusCode: 400);
            if (string.IsNullOrEmpty(req.Termainno) || req.Termainno.Length != 12)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "termainno must be 12 hex chars (6 bytes)" }, statusCode: 400);

            try
            {
                var result = await service.ConsumeInitAsync(req.Dealflag, req.Keyindex, req.Amount, req.Termainno, req.ReaderName);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "HARDWARE_ERROR", message = ex.Message }, statusCode: 500);
            }
        });

        group.MapPost("/consume/capp-init", async (ConsumeInitRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            if (req.Dealflag < 1 || req.Dealflag > 2)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "dealflag must be 1 or 2" }, statusCode: 400);
            if (req.Keyindex < 0 || req.Keyindex > 255)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "keyindex must be 0-255" }, statusCode: 400);
            if (req.Amount <= 0)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "Amount must be greater than 0" }, statusCode: 400);
            if (string.IsNullOrEmpty(req.Termainno) || req.Termainno.Length != 12)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "termainno must be 12 hex chars (6 bytes)" }, statusCode: 400);

            try
            {
                var result = await service.ConsumeCappInitAsync(req.Dealflag, req.Keyindex, req.Amount, req.Termainno, req.ReaderName);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
            catch (Exception ex)
            {
                return Results.Json(new { error = "HARDWARE_ERROR", message = ex.Message }, statusCode: 500);
            }
        });

        group.MapPost("/consume/execute", async (ConsumeExecuteRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "TransitCard not registered" }, statusCode: 503);

            if (string.IsNullOrEmpty(req.SessionId))
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "sessionId is required" }, statusCode: 400);
            if (string.IsNullOrEmpty(req.Dealtime) || req.Dealtime.Length != 14)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "dealtime must be 14 hex chars (yyyymmddHHmiss BCD)" }, statusCode: 400);
            if (string.IsNullOrEmpty(req.Mac1) || req.Mac1.Length != 8)
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "mac1 must be 8 hex chars (4 bytes)" }, statusCode: 400);

            var result = await service.ConsumeExecuteAsync(req.SessionId, req.Termdealno, req.Dealtime, req.Mac1);
            if (!result.Success)
            {
                var msg = result.ErrorMessage ?? "Consume execution failed";
                if (result.Sw1 != null)
                    msg += $" (SW={result.Sw1}{result.Sw2})";
                return Results.Json(new { error = "HARDWARE_ERROR", message = msg, sw1 = result.Sw1, sw2 = result.Sw2 }, statusCode: 500);
            }

            return Results.Ok(new { sw1 = result.Sw1, sw2 = result.Sw2, success = true });
        });
    }

}
