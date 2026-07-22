using DeviceHub.Devices.Contracts;

namespace DeviceHub.Devices.Contracts.Abstractions.Services;

/// <summary>打印机服务接口。</summary>
public interface IPrinterService : IHardwareService
{
    Task<List<PrinterInfo>> GetPrintersAsync(CancellationToken ct = default);

    Task<bool> PrintTextAsync(string text, string? printerName = null, CancellationToken ct = default);

    Task<bool> PrintRawAsync(byte[] data, string? printerName = null, CancellationToken ct = default);
}
