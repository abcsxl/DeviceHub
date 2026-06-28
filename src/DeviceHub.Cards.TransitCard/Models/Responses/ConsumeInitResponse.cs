namespace DeviceHub.Cards.TransitCard.Models.Responses;

/// <summary>消费初始化响应。</summary>
public record ConsumeInitResponse(
    string SessionId,
    string? CardResponse
);
