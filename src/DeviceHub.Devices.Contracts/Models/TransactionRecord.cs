namespace DeviceHub.Devices.Contracts;

/// <summary>交易记录。</summary>
public record TransactionRecord(
    string Type,
    int Amount,
    DateTime Timestamp,
    string? Location
);
