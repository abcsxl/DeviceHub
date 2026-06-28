namespace DeviceHub.Cards.TransitCard.Models.Requests;

/// <summary>消费初始化请求。</summary>
public record ConsumeInitRequest(int Dealflag = 2, int Keyindex = 0, int Amount = 0, string Termainno = "000000000000", string? ReaderName = null);
