namespace DeviceHub.Devices.Contracts;

public record TransmitResult(
    bool Success,
    string? Sw1 = null,
    string? Sw2 = null,
    string? ResponseData = null,
    string? ErrorMessage = null
);

public record CardStatusEventArgs(
    string ReaderName,
    string OldStatus,
    string NewStatus
);

public interface IPcscService : IHardwareService
{
    Task<IReadOnlyList<ReaderInfo>> ListReadersAsync(CancellationToken ct = default);

    Task<ReaderInfo> GetReaderInfoAsync(string readerName, CancellationToken ct = default);

    Task<TransmitResult> TransmitAsync(
        string readerName, string apdu,
        CancellationToken ct = default);

    Task<string?> GetAtrAsync(string readerName, CancellationToken ct = default);

    event EventHandler<CardStatusEventArgs>? CardStatusChanged;
}
