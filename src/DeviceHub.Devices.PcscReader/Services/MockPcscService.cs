using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions;
using DeviceHub.Devices.Contracts.Abstractions.Services;

using DeviceHub.Devices.PcscReader.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Devices.PcscReader.Services;

public class MockPcscService : IPcscService, IHardwareEndpointRegistrar, IDisposable
{
    private readonly ILogger<MockPcscService> _logger;
    private HardwareStatus _status = HardwareStatus.Stopped;
    private readonly Dictionary<string, bool> _readerStates = new();
    private CancellationTokenSource? _monitorCts;
    private Thread? _monitorThread;
    private bool _cardPresent = true;
    private readonly object _stateLock = new();

    private const string MockReaderCl = "Mock Reader CL";
    private const string MockReaderSam = "Mock Reader SAM";
    private const string MockAtr = "3B8F8001804F0CA0000003060300030000000068";

    public string Name => "Pcsc";
    public HardwareStatus Status => _status;
    public void MapEndpoints(IEndpointRouteBuilder app) => PcscEndpoint.MapEndpoints(app);

    public event EventHandler<CardStatusEventArgs>? CardStatusChanged;

    public MockPcscService(ILogger<MockPcscService> logger)
    {
        _logger = logger;
    }

    public Task InitAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_status == HardwareStatus.Running)
                return Task.CompletedTask;

            _readerStates[MockReaderCl] = _cardPresent;
            _readerStates[MockReaderSam] = false;

            _status = HardwareStatus.Running;
            _logger.LogInformation("[Mock] PCSC mock service started, readers: {Reader1}, {Reader2}", MockReaderCl, MockReaderSam);

            _monitorCts = new CancellationTokenSource();
            _monitorThread = new Thread(() => MonitorCardState(_monitorCts.Token))
            {
                IsBackground = true,
                Name = "MockPcscMonitor"
            };
            _monitorThread.Start();
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            _monitorCts?.Cancel();
            _monitorThread?.Join(3000);
            _monitorCts?.Dispose();
            _monitorCts = null;
            _monitorThread = null;

            _readerStates.Clear();
            _status = HardwareStatus.Stopped;
            _logger.LogInformation("[Mock] PCSC mock service stopped");
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReaderInfo>> ListReadersAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_status != HardwareStatus.Running)
                return Task.FromResult<IReadOnlyList<ReaderInfo>>([]);

            var infos = _readerStates
                .Select(kv => new ReaderInfo(kv.Key, kv.Value, kv.Value ? MockAtr : null))
                .ToList();

            return Task.FromResult<IReadOnlyList<ReaderInfo>>(infos);
        }
    }

    public Task<ReaderInfo> GetReaderInfoAsync(string readerName, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_status != HardwareStatus.Running || !_readerStates.ContainsKey(readerName))
                return Task.FromResult(new ReaderInfo(readerName, false));

            var isPresent = _readerStates[readerName];
            return Task.FromResult(new ReaderInfo(readerName, isPresent, isPresent ? MockAtr : null));
        }
    }

    public Task<string?> GetAtrAsync(string readerName, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_status != HardwareStatus.Running || !_readerStates.TryGetValue(readerName, out var isPresent) || !isPresent)
                return Task.FromResult<string?>(null);

            return Task.FromResult<string?>(MockAtr);
        }
    }

    public Task<string?> ResetCardAsync(string readerName, CancellationToken ct = default)
    {
        return GetAtrAsync(readerName, ct);
    }

    public Task<TransmitResult> TransmitAsync(string readerName, string apdu, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_status != HardwareStatus.Running)
                return Task.FromResult(new TransmitResult(false, ErrorMessage: "[Mock] PCSC mock service not running", ErrorCode: "HARDWARE_ERROR"));

            if (!_readerStates.ContainsKey(readerName))
                return Task.FromResult(new TransmitResult(false, ErrorMessage: $"[Mock] Reader not found: {readerName}", ErrorCode: "READER_NOT_FOUND"));

            if (!_readerStates[readerName])
                return Task.FromResult(new TransmitResult(false, ErrorMessage: "[Mock] No card present in reader", ErrorCode: "CARD_NOT_PRESENT"));

            if (string.IsNullOrEmpty(apdu) || apdu.Length % 2 != 0)
                return Task.FromResult(new TransmitResult(false, ErrorMessage: "[Mock] Invalid APDU format", ErrorCode: "INVALID_PARAMETERS"));

            var responseData = apdu.ToUpperInvariant() switch
            {
                var s when s.StartsWith("00A4040007A000000003869807") => new TransmitResult(true, Sw1: "90", Sw2: "00", ResponseData: "0102030405"),
                var s when s.StartsWith("805C000204") => new TransmitResult(true, Sw1: "90", Sw2: "00", ResponseData: "00000960"),
                var s when s.StartsWith("00B00000") => new TransmitResult(true, Sw1: "90", Sw2: "00", ResponseData: "123456789012345678"),
                var s when s.StartsWith("00B2010C") => new TransmitResult(true, Sw1: "90", Sw2: "00", ResponseData: "0102030405060708090A"),
                _ => new TransmitResult(true, Sw1: "90", Sw2: "00", ResponseData: "MOCK_RESPONSE_DATA")
            };

            _logger.LogDebug("[Mock] Transmit: reader={Reader}, apdu={Apdu} -> SW1={Sw1} SW2={Sw2}", readerName, apdu, responseData.Sw1, responseData.Sw2);
            return Task.FromResult(responseData);
        }
    }

    public void Dispose()
    {
        ShutdownAsync().GetAwaiter().GetResult();
    }

    private void MonitorCardState(CancellationToken ct)
    {
        var cycle = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Thread.Sleep(10000);
                if (ct.IsCancellationRequested)
                    break;

                lock (_stateLock)
                {
                    if (_status != HardwareStatus.Running || !_readerStates.ContainsKey(MockReaderCl))
                        break;

                    cycle++;
                    var newCardState = cycle % 2 == 0;
                    var oldState = _readerStates[MockReaderCl];

                    if (oldState != newCardState)
                    {
                        _readerStates[MockReaderCl] = newCardState;
                        var oldStatus = oldState ? "card_present" : "empty";
                        var newStatus = newCardState ? "card_present" : "empty";

                        _logger.LogInformation("[Mock] Card state changed: {Reader} {Old} -> {New}", MockReaderCl, oldStatus, newStatus);
                        CardStatusChanged?.Invoke(this, new CardStatusEventArgs(MockReaderCl, oldStatus, newStatus));
                    }
                }
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[Mock] Monitor exception");
            }
        }
    }
}
