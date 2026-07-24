namespace DeviceHub.Devices.Contracts.Helpers;

public static class ErrorCodeHelper
{
    private static readonly HashSet<string> KnownCodes =
    [
        "CARD_NOT_PRESENT", "READER_NOT_FOUND", "PRINTER_NOT_FOUND", "IDCARD_NOT_FOUND",
        "TIMEOUT", "SERVICE_NOT_RUNNING", "DRIVER_NOT_FOUND",
        "INVALID_PARAMETERS", "INVALID_ACTION",
        "FILE_NOT_FOUND", "UNSUPPORTED_COMMAND", "SECURITY_ERROR", "INVALID_DATA",
        "CARD_FULL", "HARDWARE_ERROR"
    ];

    private static readonly Dictionary<string, string> DefaultMessages = new()
    {
        ["READER_NOT_FOUND"] = "Reader not found or disconnected",
        ["CARD_NOT_PRESENT"] = "No card present in reader",
        ["PRINTER_NOT_FOUND"] = "Printer not found or not connected",
        ["IDCARD_NOT_FOUND"] = "ID card reader not found",
        ["TIMEOUT"] = "Hardware operation timed out",
        ["SERVICE_NOT_RUNNING"] = "Service is not running",
        ["DRIVER_NOT_FOUND"] = "Driver not found or not registered",
        ["INVALID_PARAMETERS"] = "Invalid request parameters",
        ["INVALID_ACTION"] = "Unsupported action",
        ["FILE_NOT_FOUND"] = "Card file not found",
        ["UNSUPPORTED_COMMAND"] = "Card does not support this command",
        ["SECURITY_ERROR"] = "Card security status not satisfied",
        ["INVALID_DATA"] = "Invalid card data",
        ["CARD_FULL"] = "Card storage is full",
        ["HARDWARE_ERROR"] = "Hardware error"
    };

    public static (string code, string message) ParseTransmitError(string message)
    {
        if (message.Contains("Reader name not specified"))
            return ("INVALID_PARAMETERS", message);

        var colonPos = message.IndexOf(':');
        if (colonPos < 0)
        {
            var fallbackCode = KnownCodes.Contains(message) ? message : "HARDWARE_ERROR";
            var fallbackMsg = DefaultMessages.GetValueOrDefault(fallbackCode, message);
            return (fallbackCode, fallbackMsg);
        }

        var code = message[..colonPos];
        var msg = message[(colonPos + 1)..];

        if (!KnownCodes.Contains(code))
            return ("HARDWARE_ERROR", message);

        return (code, string.IsNullOrEmpty(msg) ? DefaultMessages.GetValueOrDefault(code, code) : msg);
    }

    public static int GetHttpStatus(string errorCode) => errorCode switch
    {
        "INVALID_PARAMETERS" or "INVALID_ACTION" or "UNSUPPORTED_COMMAND" or "INVALID_DATA" => 400,
        "SECURITY_ERROR" => 403,
        "CARD_NOT_PRESENT" or "READER_NOT_FOUND" or "PRINTER_NOT_FOUND" or "IDCARD_NOT_FOUND" or "FILE_NOT_FOUND" => 404,
        "TIMEOUT" => 408,
        "SERVICE_NOT_RUNNING" or "DRIVER_NOT_FOUND" => 503,
        "CARD_FULL" => 507,
        _ => 500
    };
}
