using System.Collections.Concurrent;
using DeviceHub.Devices.Contracts;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Cards.TransitCard.Services;

public class TransitCardService : ITransitCardService
{
    private const string SelectAid = "00A4040008A00000000386980701";
    private const string GetBalance = "805C000204";
    private const string GetCardNumber = "00B0000008";
    private const string GetTransactionLog = "00B2010C14";

    private readonly IPcscService _pcsc;
    private readonly ILogger<TransitCardService> _logger;
    private readonly ConcurrentDictionary<string, RechargeSession> _sessions = new();

    public TransitCardService(IPcscService pcsc, ILogger<TransitCardService> logger)
    {
        _pcsc = pcsc;
        _logger = logger;
    }

    public async Task<string[]> GetAvailableReadersAsync(CancellationToken ct = default)
    {
        var readers = await _pcsc.ListReadersAsync(ct);
        return readers.Where(r => r.IsCardPresent).Select(r => r.Name).ToArray();
    }

    public async Task<CardInfo> ReadCardInfoAsync(string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);
        var cardNumBytes = await TransmitHex(name, GetCardNumber, ct);
        var cardNumber = ParseCardNumber(cardNumBytes.ResponseData);
        return new CardInfo(cardNumber, IssuerCode: null, CardType: null, ExpiryDate: null, OtherData: null);
    }

    public async Task<BalanceInfo> ReadBalanceAsync(string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);
        var result = await TransmitHex(name, GetBalance, ct);
        var balance = ParseBalance(result.ResponseData);
        return new BalanceInfo(balance);
    }

    public async Task<List<TransactionRecord>> ReadTransactionsAsync(int count = 10, string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);
        var result = await TransmitHex(name, GetTransactionLog, ct);
        return ParseTransactions(result.ResponseData, count);
    }

    public async Task<RechargeInitResult> RechargeInitAsync(decimal amount, string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);

        var sessionId = Guid.NewGuid().ToString("N");
        var amountHex = ((int)(amount * 100)).ToString("X8");
        var unsignedApdu = $"8054000008{amountHex}0000000000000000";
        var signData = $"{sessionId}{amountHex}";

        var hostChallenge = "A0A1A2A3A4A5A6A7";
        var initCmd = $"8050000008{hostChallenge}";
        var initResult = await TransmitHex(name, initCmd, ct);

        _sessions[sessionId] = new RechargeSession
        {
            ReaderName = name,
            Amount = amount,
            HostChallenge = hostChallenge,
            CardChallenge = initResult.ResponseData,
            UnsignedApdu = unsignedApdu,
            Timestamp = DateTime.UtcNow
        };

        return new RechargeInitResult(sessionId, unsignedApdu, signData);
    }

    public async Task<RechargeResult> RechargeExecuteAsync(string sessionId, string macSignature, CancellationToken ct = default)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
            return new RechargeResult(false, null, null, "Session not found or expired");

        var macHex = macSignature.Length % 2 == 0 ? macSignature : macSignature;
        var rechargeApdu = session.UnsignedApdu[..^16] + macHex;
        var result = await TransmitHex(session.ReaderName, rechargeApdu, ct);

        return new RechargeResult(result.Success, result.Sw1, result.Sw2);
    }

    private async Task<string> ResolveReaderName(string? readerName, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(readerName))
            return readerName;

        var available = await GetAvailableReadersAsync(ct);
        if (available.Length == 0)
            throw new InvalidOperationException("No card present in any reader");

        return available[0];
    }

    private async Task SelectTransitApplet(string readerName, CancellationToken ct)
    {
        var result = await _pcsc.TransmitAsync(readerName, SelectAid, ct);
        if (!result.Success || result.Sw1 != "90")
        {
            _logger.LogWarning("SELECT AID failed for {Reader}: SW={Sw1}{Sw2}", readerName, result.Sw1, result.Sw2);
            throw new InvalidOperationException("Transit card not found on this reader");
        }
    }

    private async Task<TransmitResult> TransmitHex(string readerName, string apdu, CancellationToken ct)
    {
        return await _pcsc.TransmitAsync(readerName, apdu, ct);
    }

    private static string ParseCardNumber(string? responseData)
    {
        if (string.IsNullOrEmpty(responseData)) return "Unknown";
        return responseData.Length >= 16 ? responseData[..16] : responseData;
    }

    private static int ParseBalance(string? responseData)
    {
        if (string.IsNullOrEmpty(responseData) || responseData.Length < 4) return 0;
        if (int.TryParse(responseData[..4], System.Globalization.NumberStyles.HexNumber, null, out var bal))
            return bal;
        return 0;
    }

    private static List<TransactionRecord> ParseTransactions(string? responseData, int count)
    {
        var records = new List<TransactionRecord>();
        if (string.IsNullOrEmpty(responseData)) return records;

        for (var i = 0; i < responseData.Length && records.Count < count; i += 8)
        {
            if (i + 8 > responseData.Length) break;
            var type = responseData[i..(i + 2)];
            var amountStr = responseData[(i + 2)..(i + 8)];
            if (int.TryParse(amountStr, System.Globalization.NumberStyles.HexNumber, null, out var amount))
            {
                records.Add(new TransactionRecord(
                    type == "01" ? "DEBIT" : type == "02" ? "CREDIT" : "UNKNOWN",
                    amount,
                    DateTime.UtcNow,
                    null
                ));
            }
        }

        return records;
    }

    private sealed class RechargeSession
    {
        public string ReaderName { get; set; } = "";
        public decimal Amount { get; set; }
        public string HostChallenge { get; set; } = "";
        public string? CardChallenge { get; set; }
        public string UnsignedApdu { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
