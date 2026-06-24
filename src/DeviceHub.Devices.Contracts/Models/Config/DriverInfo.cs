namespace DeviceHub.Devices.Contracts;

public record DriverInfo(
    string Name,
    string Status,
    bool Enabled,
    DateTime RegisteredAt,
    string? Details = null
);
