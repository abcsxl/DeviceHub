namespace DeviceHub.Devices.Contracts;

/// <summary>余额信息。</summary>
public record BalanceInfo(
    int Balance,
    string Currency = "CNY"
);
