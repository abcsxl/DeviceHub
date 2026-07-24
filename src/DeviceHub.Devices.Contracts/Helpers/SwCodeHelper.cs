using System.Text.RegularExpressions;

namespace DeviceHub.Devices.Contracts.Helpers;

public static class SwCodeHelper
{
    public static (string ErrorCode, int StatusCode) ClassifySw(string sw1, string sw2)
    {
        return (sw1, sw2) switch
        {
            ("6A", "82") or ("6A", "83") or ("6A", "88") or ("62", "83") => ("FILE_NOT_FOUND", 404),
            ("6A", "86") or ("67", "00") or ("6B", "00") or ("69", "86") => ("INVALID_PARAMETERS", 400),
            ("6D", "00") or ("6E", "00") => ("UNSUPPORTED_COMMAND", 400),
            ("6A", "81") => ("UNSUPPORTED_COMMAND", 400),
            ("69", "82") or ("69", "85") or ("69", "88") or ("69", "83") or ("69", "87") => ("SECURITY_ERROR", 403),
            ("63", "00") => ("SECURITY_ERROR", 403),
            ("69", "84") or ("6A", "80") or ("69", "81") => ("INVALID_DATA", 400),
            ("6A", "84") => ("CARD_FULL", 507),
            _ when sw1 == "63" && sw2.Length == 2 && sw2[0] == 'C' => ("SECURITY_ERROR", 403),
            _ => ("HARDWARE_ERROR", 500)
        };
    }

    private static readonly Regex SwPattern = new Regex(@"SW=([0-9A-F]{2})([0-9A-F]{2})", RegexOptions.Compiled);

    public static (string ErrorCode, int StatusCode) ClassifySwFromMessage(string message)
    {
        var match = SwPattern.Match(message);
        if (!match.Success) return ("HARDWARE_ERROR", 500);
        return ClassifySw(match.Groups[1].Value, match.Groups[2].Value);
    }
}
