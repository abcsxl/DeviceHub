namespace DeviceHub.Cards.TransitCard.Models.Requests;

/// <summary>充值初始化请求。</summary>
public record RechargeInitRequest(decimal Amount, string? ReaderName);
