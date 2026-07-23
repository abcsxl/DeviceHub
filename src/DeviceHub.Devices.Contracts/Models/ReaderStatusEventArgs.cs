namespace DeviceHub.Devices.Contracts;

/// <summary>读卡器状态变更事件参数。</summary>
public record ReaderStatusEventArgs(string ReaderName, string Status);
