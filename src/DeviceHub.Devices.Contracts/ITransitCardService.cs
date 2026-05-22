namespace DeviceHub.Devices.Contracts;

public record CardInfo(
    string CardNumber,
    string? IssuerCode,
    string? CardType,
    string? ExpiryDate,
    string[]? OtherData
);

public record BalanceInfo(
    int Balance,
    string Currency = "CNY"
);

public record TransactionRecord(
    string Type,
    int Amount,
    DateTime Timestamp,
    string? Location
);

public record RechargeInitResult(
    string SessionId,
    string UnsignedApdu,
    string SignData
);

public record RechargeResult(
    bool Success,
    string? Sw1,
    string? Sw2,
    string? ErrorMessage = null
);

public interface ITransitCardService
{
    Task<string[]> GetAvailableReadersAsync(CancellationToken ct = default);

    Task<CardInfo> ReadCardInfoAsync(string? readerName = null, CancellationToken ct = default);

    Task<BalanceInfo> ReadBalanceAsync(string? readerName = null, CancellationToken ct = default);

    Task<List<TransactionRecord>> ReadTransactionsAsync(int count = 10, string? readerName = null, CancellationToken ct = default);

    Task<RechargeInitResult> RechargeInitAsync(decimal amount, string? readerName = null, CancellationToken ct = default);

    Task<RechargeResult> RechargeExecuteAsync(string sessionId, string macSignature, CancellationToken ct = default);
}
