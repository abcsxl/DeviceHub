namespace DeviceHub.Devices.Contracts;

/// <summary>交通卡基本信息。</summary>
public record CardInfo(
    string CardNumber,
    string? IssuerCode,
    string? CardType,
    string? ExpiryDate,
    string[]? OtherData
);
