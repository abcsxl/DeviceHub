using DeviceHub.Devices.Contracts;

namespace DeviceHub.Devices.Contracts.Abstractions.Services;

public interface IIdCardService : IHardwareService
{
    Task<List<ReaderInfo>> GetReadersAsync(CancellationToken ct = default);

    Task<IdCardInfo?> ReadCardAsync(string? readerName = null, CancellationToken ct = default);

    Task<byte[]?> ReadPhotoAsync(string? readerName = null, CancellationToken ct = default);

    event EventHandler<CardStatusEventArgs>? CardInserted;

    event EventHandler<CardStatusEventArgs>? CardRemoved;
}
