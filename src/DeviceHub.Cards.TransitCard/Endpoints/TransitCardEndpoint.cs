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
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "Transit card service not registered" }, statusCode: 503);

            var readers = await service.GetAvailableReadersAsync();
            return Results.Ok(new { readers });
        });

        group.MapGet("/info", async (string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "Transit card service not registered" }, statusCode: 503);

            try
            {
                var info = await service.ReadCardInfoAsync(readerName);
                return Results.Ok(info);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
        });

        group.MapGet("/balance", async (string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "Transit card service not registered" }, statusCode: 503);

            try
            {
                var balance = await service.ReadBalanceAsync(readerName);
                return Results.Ok(balance);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
        });

        group.MapGet("/transactions", async (int? count, string? readerName, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "Transit card service not registered" }, statusCode: 503);

            try
            {
                var records = await service.ReadTransactionsAsync(count ?? 10, readerName);
                return Results.Ok(new { records });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { error = "CARD_NOT_PRESENT", message = ex.Message }, statusCode: 404);
            }
        });

        group.MapPost("/recharge/init", async (RechargeInitRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "Transit card service not registered" }, statusCode: 503);

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
        });

        group.MapPost("/recharge/execute", async (RechargeExecuteRequest req, HttpContext context) =>
        {
            var service = context.RequestServices.GetService<ITransitCardService>();
            if (service == null)
                return Results.Json(new { error = "DRIVER_NOT_FOUND", message = "Transit card service not registered" }, statusCode: 503);

            if (string.IsNullOrEmpty(req.SessionId) || string.IsNullOrEmpty(req.MacSignature))
                return Results.Json(new { error = "INVALID_PARAMETERS", message = "sessionId and macSignature are required" }, statusCode: 400);

            var result = await service.RechargeExecuteAsync(req.SessionId, req.MacSignature);
            if (!result.Success)
                return Results.Json(new { error = "HARDWARE_ERROR", message = result.ErrorMessage ?? "Recharge execution failed" }, statusCode: 500);

            return Results.Ok(new { sw1 = result.Sw1, sw2 = result.Sw2, success = true });
        });
    }

}
