using System.Collections.Concurrent;
using DeviceHub.Cards.TransitCard.Constants;
using DeviceHub.Devices.Contracts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Cards.TransitCard.Services;

public class MockTransitCardService : ITransitCardService, IHardwareEndpointRegistrar
{
    private readonly IPcscService _pcsc;
    private readonly ILogger<MockTransitCardService> _logger;
    private readonly ConcurrentDictionary<string, MockSession> _sessions = new();

    public MockTransitCardService(IPcscService pcsc, ILogger<MockTransitCardService> logger)
    {
        _pcsc = pcsc;
        _logger = logger;
    }

    public async Task<string[]> GetAvailableReadersAsync(CancellationToken ct = default)
    {
        var readers = await _pcsc.ListReadersAsync(ct);
        return readers.Where(r => r.IsCardPresent).Select(r => r.Name).ToArray();
    }

    public Task<CardInfo> ReadCardInfoAsync(string? readerName = null, CancellationToken ct = default)
    {
        return Task.FromResult(new CardInfo(
            CardNumber: "1234567890123456",
            IssuerCode: "001",
            CardType: "01",
            ExpiryDate: "202812",
            OtherData: ["Mock data"]
        ));
    }

    public Task<BalanceInfo> ReadBalanceAsync(string? readerName = null, CancellationToken ct = default)
    {
        return Task.FromResult(new BalanceInfo(Balance: 5000));
    }

    public Task<List<TransactionRecord>> ReadTransactionsAsync(int count = 10, string? readerName = null, CancellationToken ct = default)
    {
        var records = new List<TransactionRecord>();
        for (var i = 0; i < Math.Min(count, 10); i++)
        {
            records.Add(new TransactionRecord(
                i % 2 == 0 ? "DEBIT" : "CREDIT",
                (i + 1) * 100,
                DateTime.UtcNow.AddDays(-i),
                $"Station_{i}"
            ));
        }
        return Task.FromResult(records);
    }

    public Task<RechargeInitResult> RechargeInitAsync(decimal amount, string? readerName = null, CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var amountHex = ((int)(amount * 100)).ToString("X8");
        var unsignedApdu = $"{ApduCommands.CreditForLoad}{amountHex}0000000000000000";
        var signData = $"{sessionId}{amountHex}";

        _sessions[sessionId] = new MockSession { Amount = amount, Timestamp = DateTime.UtcNow };

        return Task.FromResult(new RechargeInitResult(sessionId, unsignedApdu, signData));
    }

    public Task<RechargeResult> RechargeExecuteAsync(string sessionId, string macSignature, CancellationToken ct = default)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
            return Task.FromResult(new RechargeResult(false, null, null, "Session not found or expired"));

        if (string.IsNullOrEmpty(macSignature))
            return Task.FromResult(new RechargeResult(false, null, null, "Invalid MAC signature"));

        _logger.LogInformation("Mock recharge completed: amount={Amount}", session.Amount);
        return Task.FromResult(new RechargeResult(true, SwConstants.SuccessPrefix, "00"));
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => TransitCardEndpointHelper.MapEndpoints(app);

    private sealed class MockSession
    {
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
