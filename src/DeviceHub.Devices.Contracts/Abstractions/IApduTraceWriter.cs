namespace DeviceHub.Devices.Contracts.Abstractions;

public interface IApduTraceWriter
{
    bool IsEnabled { get; }
    void Write(string message);
    void SetEnabled(bool enabled);
}
