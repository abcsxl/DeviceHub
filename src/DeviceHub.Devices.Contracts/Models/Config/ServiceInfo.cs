namespace DeviceHub.Devices.Contracts;

public record ServiceInfo(
    string Version,
    string Platform,
    int HttpPort,
    DateTime StartTime,
    TimeSpan Uptime,
    int WebSocketConnections,
    IReadOnlyList<DriverInfo> Drivers
);
