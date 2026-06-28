namespace DeviceHub.Cards.TransitCard.Models.Responses;

/// <summary>消费执行结果。</summary>
public record ConsumeResult(
    bool Success,
    string? Sw1,
    string? Sw2,
    string? ErrorMessage = null
);
