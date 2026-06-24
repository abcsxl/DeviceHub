namespace DeviceHub.Devices.Contracts;

public interface IIdCardService : IHardwareService
{
    Task<List<ReaderInfo>> GetReadersAsync(CancellationToken ct = default);

    Task<IdCardInfo?> ReadCardAsync(string? readerName = null, CancellationToken ct = default);

    Task<byte[]?> ReadPhotoAsync(string? readerName = null, CancellationToken ct = default);
}
