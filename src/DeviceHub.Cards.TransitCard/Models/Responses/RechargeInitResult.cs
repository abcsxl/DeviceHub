namespace DeviceHub.Cards.TransitCard.Models.Responses;

/// <summary>充值初始化结果。</summary>
public record RechargeInitResult(
    string SessionId,
    string UnsignedApdu,
    string SignData
);
