namespace DeviceHub.Cards.TransitCard.Models.Requests;

/// <summary>充值执行请求。</summary>
public record RechargeExecuteRequest(string? SessionId, string? MacSignature);
