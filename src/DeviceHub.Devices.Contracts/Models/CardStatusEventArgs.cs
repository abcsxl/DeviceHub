namespace DeviceHub.Devices.Contracts;

/// <summary>卡片状态变更事件参数。</summary>
public record CardStatusEventArgs(
    string ReaderName,
    string OldStatus,
    string NewStatus
);
