using DeviceHub.Devices.Contracts;

namespace DeviceHub.Devices.Contracts.Abstractions.Services;

public interface IPcscService : IHardwareService
{
    Task<IReadOnlyList<ReaderInfo>> ListReadersAsync(CancellationToken ct = default);

    Task<ReaderInfo> GetReaderInfoAsync(string readerName, CancellationToken ct = default);

    Task<TransmitResult> TransmitAsync(
        string readerName, string apdu,
        CancellationToken ct = default);

    Task<string?> GetAtrAsync(string readerName, CancellationToken ct = default);

    Task<string?> ResetCardAsync(string readerName, CancellationToken ct = default);

    event EventHandler<CardStatusEventArgs>? CardStatusChanged;
}
