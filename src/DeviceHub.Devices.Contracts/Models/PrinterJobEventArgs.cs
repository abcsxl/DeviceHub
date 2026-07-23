namespace DeviceHub.Devices.Contracts;

/// <summary>打印任务事件参数。</summary>
public record PrinterJobEventArgs(string JobId, string PrinterName, string Status, string? ErrorMessage = null);
