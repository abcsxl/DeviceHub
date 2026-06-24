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


