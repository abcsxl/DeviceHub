namespace DeviceHub.Devices.Contracts;

/// <summary>APDU 传输结果。</summary>
public record TransmitResult(
    bool Success,
    string? Sw1 = null,
    string? Sw2 = null,
    string? ResponseData = null,
    string? ErrorMessage = null,
    string? ErrorCode = null
);
