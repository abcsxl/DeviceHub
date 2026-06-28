using DeviceHub.Devices.Contracts;
using DeviceHub.Cards.TransitCard.Models.Responses;

namespace DeviceHub.Cards.TransitCard;

public interface ITransitCardService
{
    Task<string[]> GetAvailableReadersAsync(CancellationToken ct = default);

    Task<string?> ResetCardAsync(string? readerName = null, CancellationToken ct = default);

    Task<CardInfo> ReadCardInfoAsync(string? readerName = null, CancellationToken ct = default);

    Task<BalanceInfo> ReadBalanceAsync(string? readerName = null, CancellationToken ct = default);

    Task<List<TransactionRecord>> ReadTransactionsAsync(int count = 10, string? readerName = null, CancellationToken ct = default);

    Task<RechargeInitResult> RechargeInitAsync(int amount, string? readerName = null, CancellationToken ct = default);

    Task<RechargeResult> RechargeExecuteAsync(string sessionId, string macSignature, CancellationToken ct = default);

    Task<ConsumeInitResponse> ConsumeInitAsync(int dealflag, int keyindex, int amount, string termainno, string? readerName = null, CancellationToken ct = default);

    Task<ConsumeInitResponse> ConsumeCappInitAsync(int dealflag, int keyindex, int amount, string termainno, string? readerName = null, CancellationToken ct = default);

    Task<ConsumeResult> ConsumeExecuteAsync(string sessionId, int termdealno, string dealtime, string mac1, CancellationToken ct = default);
}
