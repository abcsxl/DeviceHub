namespace DeviceHub.Devices.Contracts;

/// <summary>充值执行结果。</summary>
public record RechargeResult(
    bool Success,
    string? Sw1,
    string? Sw2,
    string? ErrorMessage = null
);
