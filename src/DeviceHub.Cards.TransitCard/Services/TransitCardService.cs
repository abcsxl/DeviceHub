using DeviceHub.Cards.TransitCard.Constants;
using DeviceHub.Cards.TransitCard.Endpoints;
using DeviceHub.Cards.TransitCard.Helpers;
using DeviceHub.Cards.TransitCard.Models.Responses;
using DeviceHub.Devices.Contracts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DeviceHub.Cards.TransitCard.Services;

public class TransitCardService : ITransitCardService, IHardwareEndpointRegistrar
{
    private readonly IPcscService _pcsc;
    private readonly ILogger<TransitCardService> _logger;
    private readonly ConcurrentDictionary<string, RechargeSession> _sessions = new();
    private readonly ConcurrentDictionary<string, ConsumeSession> _consumeSessions = new();

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

    public async Task<string?> ResetCardAsync(string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        return await _pcsc.ResetCardAsync(name, ct);
    }

    public async Task<CardInfo> ReadCardInfoAsync(string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);
        var cardNumBytes = await TransmitHex(name, ApduBuilder.GetCardNumber(), ct);
        if (!cardNumBytes.Success || cardNumBytes.Sw1 != SwConstants.SuccessPrefix)
            throw new Exception($"读取卡号失败 (SW={cardNumBytes.Sw1}{cardNumBytes.Sw2})");
        var cardNumber = ParseCardNumber(cardNumBytes.ResponseData);
        return new CardInfo(cardNumber, IssuerCode: null, CardType: null, ExpiryDate: null, OtherData: null);
    }

    public async Task<BalanceInfo> ReadBalanceAsync(string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);
        var result = await TransmitHex(name, ApduBuilder.GetBalance(), ct);
        if (!result.Success || result.Sw1 != SwConstants.SuccessPrefix)
            throw new Exception($"读取余额失败 (SW={result.Sw1}{result.Sw2})");
        var balance = ParseBalance(result.ResponseData);
        return new BalanceInfo(balance);
    }

    public async Task<List<TransactionRecord>> ReadTransactionsAsync(int count = 10, string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);
        var result = await TransmitHex(name, ApduBuilder.GetTransactionLog(), ct);
        if (!result.Success || result.Sw1 != SwConstants.SuccessPrefix)
            throw new Exception($"读取交易记录失败 (SW={result.Sw1}{result.Sw2})");
        return ParseTransactions(result.ResponseData, count);
    }

    public async Task<RechargeInitResult> RechargeInitAsync(int amount, string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);

        var sessionId = Guid.NewGuid().ToString("N");
        var amountHex = amount.ToString("X8");
        var unsignedApdu = ApduBuilder.CreditForLoad(amountHex);
        var signData = $"{sessionId}{amountHex}";

        var initCmd = ApduBuilder.InitRecharge(ApduBuilder.DefaultHostChallenge);
        var initResult = await TransmitHex(name, initCmd, ct);
        if (!initResult.Success || initResult.Sw1 != SwConstants.SuccessPrefix)
            throw new Exception($"充值初始化失败 (SW={initResult.Sw1}{initResult.Sw2})");

        _sessions[sessionId] = new RechargeSession
        {
            ReaderName = name,
            Amount = amount,
            HostChallenge = ApduBuilder.DefaultHostChallenge,
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

    public async Task<ConsumeInitResponse> ConsumeInitAsync(int dealflag, int keyindex, int amount, string termainno, string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);

        var sessionId = Guid.NewGuid().ToString("N");
        var amountHex = amount.ToString("X8");

        var initApdu = ApduBuilder.InitConsume(dealflag, keyindex, amountHex, termainno);
        var initResult = await TransmitHex(name, initApdu, ct);
        if (!initResult.Success || initResult.Sw1 != SwConstants.SuccessPrefix)
            throw new Exception($"消费初始化失败 (SW={initResult.Sw1}{initResult.Sw2})");

        _consumeSessions[sessionId] = new ConsumeSession { ReaderName = name, Timestamp = DateTime.UtcNow };

        return new ConsumeInitResponse(sessionId, initResult.ResponseData);
    }

    public async Task<ConsumeInitResponse> ConsumeCappInitAsync(int dealflag, int keyindex, int amount, string termainno, string? readerName = null, CancellationToken ct = default)
    {
        var name = await ResolveReaderName(readerName, ct);
        await SelectTransitApplet(name, ct);

        var sessionId = Guid.NewGuid().ToString("N");
        var amountHex = amount.ToString("X8");

        var initApdu = ApduBuilder.InitCappConsume(dealflag, keyindex, amountHex, termainno);
        var initResult = await TransmitHex(name, initApdu, ct);
        if (!initResult.Success || initResult.Sw1 != SwConstants.SuccessPrefix)
            throw new Exception($"复合消费初始化失败 (SW={initResult.Sw1}{initResult.Sw2})");

        _consumeSessions[sessionId] = new ConsumeSession { ReaderName = name, Timestamp = DateTime.UtcNow };

        return new ConsumeInitResponse(sessionId, initResult.ResponseData);
    }

    public async Task<ConsumeResult> ConsumeExecuteAsync(string sessionId, int termdealno, string dealtime, string mac1, CancellationToken ct = default)
    {
        if (!_consumeSessions.TryRemove(sessionId, out var session))
            return new ConsumeResult(false, null, null, "Session not found or expired");

        var consumeApdu = ApduBuilder.DebitForPurchase(termdealno, dealtime, mac1);
        var result = await TransmitHex(session.ReaderName, consumeApdu, ct);

        return new ConsumeResult(result.Success, result.Sw1, result.Sw2);
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
        var result = await _pcsc.TransmitAsync(readerName, ApduBuilder.SelectTransitAid(), ct);
        if (!result.Success || result.Sw1 != SwConstants.SuccessPrefix)
        {
            _logger.LogWarning("SELECT AID failed for {Reader}: SW={Sw1}{Sw2}", readerName, result.Sw1, result.Sw2);
            throw new InvalidOperationException($"SELECT 交通卡应用失败 (SW={result.Sw1}{result.Sw2})");
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

            public void MapEndpoints(IEndpointRouteBuilder app) => TransitCardEndpoint.MapEndpoints(app);

    private sealed class RechargeSession
    {
        public string ReaderName { get; set; } = "";
        public int Amount { get; set; }
        public string HostChallenge { get; set; } = "";
        public string? CardChallenge { get; set; }
        public string UnsignedApdu { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    private sealed class ConsumeSession
    {
        public string ReaderName { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
