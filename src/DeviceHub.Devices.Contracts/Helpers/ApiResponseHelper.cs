using Microsoft.AspNetCore.Http;

namespace DeviceHub.Devices.Contracts.Helpers;

public static class ApiResponseHelper
{
    public static IResult Ok<T>(T data) =>
        TypedResults.Ok(new ApiResponse<T>(data));

    public static IResult Ok() =>
        TypedResults.Ok(new ApiResponse<object> { Success = true });

    public static IResult BadRequest(string error, string message, Dictionary<string, string[]>? errors = null) =>
        TypedResults.BadRequest(new ApiResponse<object>
        {
            Success = false,
            Error = error,
            Message = message,
            Errors = errors
        });

    public static IResult NotFound(string error, string message) =>
        TypedResults.NotFound(new ApiResponse<object>
        {
            Success = false,
            Error = error,
            Message = message
        });

    public static IResult Error(string error, string message, int statusCode = 500) =>
        Results.Json(new ApiResponse<object>
        {
            Success = false,
            Error = error,
            Message = message
        }, statusCode: statusCode);

    public static IResult ErrorFromException(string error, Exception ex, int statusCode = 500) =>
        Results.Json(new ApiResponse<object>
        {
            Success = false,
            Error = error,
            Message = ex.Message
        }, statusCode: statusCode);

    public static IResult Accepted<T>(T data) =>
        Results.Json(new ApiResponse<T>(data), statusCode: StatusCodes.Status202Accepted);
}
