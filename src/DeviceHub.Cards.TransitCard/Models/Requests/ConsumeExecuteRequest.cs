namespace DeviceHub.Cards.TransitCard.Models.Requests;

/// <summary>消费执行请求。</summary>
public record ConsumeExecuteRequest(string? SessionId, int Termdealno, string Dealtime, string Mac1);
