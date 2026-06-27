namespace DeviceHub.Devices.Contracts;

/// <summary>余额信息。Balance 为卡内原始 4 字节数值（不处理单位），上层按业务场景自行解析。</summary>
public record BalanceInfo(int Balance);
