namespace DeviceHub.Devices.Contracts;

public enum HardwareStatus
{
    Stopped,
    Initializing,
    Running,
    Error
}

public interface IHardwareService
{
    string Name { get; }

    HardwareStatus Status { get; }

    Task InitAsync(CancellationToken cancellationToken = default);

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
