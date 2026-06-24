namespace DeviceHub.Devices.Contracts;

/// <summary>打印机信息。</summary>
public record PrinterInfo(
    string Name,
    string Status,
    bool IsDefault,
    string? Description = null,
    string? Location = null
);
