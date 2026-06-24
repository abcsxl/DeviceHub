namespace DeviceHub.Devices.Contracts;

public record ReaderInfo(
    string Name,
    bool IsCardPresent,
    string? Atr = null
);
