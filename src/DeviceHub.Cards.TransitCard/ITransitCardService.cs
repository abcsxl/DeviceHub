using DeviceHub.Devices.Contracts;

namespace DeviceHub.Cards.TransitCard;

public interface ITransitCardService
{
    Task<string[]> GetAvailableReadersAsync(CancellationToken ct = default);

    Task<CardInfo> ReadCardInfoAsync(string? readerName = null, CancellationToken ct = default);

    Task<BalanceInfo> ReadBalanceAsync(string? readerName = null, CancellationToken ct = default);

    Task<List<TransactionRecord>> ReadTransactionsAsync(int count = 10, string? readerName = null, CancellationToken ct = default);

    Task<RechargeInitResult> RechargeInitAsync(decimal amount, string? readerName = null, CancellationToken ct = default);

    Task<RechargeResult> RechargeExecuteAsync(string sessionId, string macSignature, CancellationToken ct = default);
}
