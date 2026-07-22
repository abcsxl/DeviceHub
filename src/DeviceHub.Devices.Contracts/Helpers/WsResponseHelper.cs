namespace DeviceHub.Devices.Contracts.Helpers;

public static class WsResponseHelper
{
    private static string Timestamp => DateTime.UtcNow.ToString("o");

    public static object Ok<T>(string requestId, T data) => new
    {
        requestId,
        success = true,
        data,
        timestamp = Timestamp
    };

    public static object Error(string requestId, string code, string message) => new
    {
        requestId,
        success = false,
        error = new { code, message },
        timestamp = Timestamp
    };
}
