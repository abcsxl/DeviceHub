namespace DeviceHub.Devices.Contracts;

public record LogEntry(
    DateTime Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception = null
);
