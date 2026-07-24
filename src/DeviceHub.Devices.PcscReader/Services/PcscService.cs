using System.Text;
using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions;
using DeviceHub.Devices.Contracts.Abstractions.Services;

using DeviceHub.Devices.PcscReader.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Devices.PcscReader.Services;

public class PcscService : IPcscService, IHardwareEndpointRegistrar, IDisposable
{
    private const uint Success = 0;
    private const uint ScardEUnknownReader = 0x80100009;
    private const uint ScardETimeout = 0x8010000A;
    private const uint ScardESharingViolation = 0x8010000B;
    private const uint ScardENoSmartcard = 0x8010000C;
    private const uint ScardEReaderUnavailable = 0x8010000F;
    private const uint ScardENoService = 0x8010001D;
    private const uint ScardEReaderUnsupported = 0x8010001A;
    private const uint ScardENoReaderAvailable = 0x8010002E;
    private const uint ScardECommAborted = 0x80100030;
    private const uint ScardWRemovedCard = 0x80100069;

    private static (string code, string message) MapPcscStatus(uint rc) => rc switch
    {
        0x80100001 => ("HARDWARE_ERROR", "PCSC internal error"),
        0x80100002 => ("HARDWARE_ERROR", "Operation cancelled"),
        0x80100003 => ("HARDWARE_ERROR", "Invalid handle"),
        0x80100004 => ("INVALID_PARAMETERS", "Invalid parameter"),
        0x80100008 => ("HARDWARE_ERROR", "Insufficient buffer"),
        0x80100009 => ("READER_NOT_FOUND", "Specified reader is not recognized by the system"),
        0x8010000A => ("TIMEOUT", "Operation timed out while communicating with reader or card"),
        0x8010000B => ("READER_NOT_FOUND", "Reader is in use by another application"),
        0x8010000C => ("CARD_NOT_PRESENT", "No card detected in reader"),
        0x8010000F => ("READER_NOT_FOUND", "Reader is not currently available"),
        0x8010001A => ("READER_NOT_FOUND", "Reader type is not supported"),
        0x8010001D => ("SERVICE_NOT_RUNNING", "Smart card service is not running"),
        0x8010001E => ("READER_NOT_FOUND", "Reader communication failed or was interrupted"),
        0x8010002E => ("READER_NOT_FOUND", "No reader available"),
        0x80100030 => ("READER_NOT_FOUND", "Communication with reader was aborted"),
        0x80100069 => ("CARD_NOT_PRESENT", "Card was removed during operation"),
        _ => ("HARDWARE_ERROR", $"Unhandled PCSC error: 0x{rc:X8}")
    };

    private readonly ILogger<PcscService> _logger;
    private readonly object _syncLock = new();
    private nint _context;
    private HardwareStatus _status = HardwareStatus.Stopped;
    private readonly Dictionary<string, bool> _lastReaderStates = [];
    private CancellationTokenSource? _monitorCts;
    private Thread? _monitorThread;
    private int _monitorConsecutiveFailures;

    public string Name => "Pcsc";
    public HardwareStatus Status => _status;
    public void MapEndpoints(IEndpointRouteBuilder app) => PcscEndpoint.MapEndpoints(app);

    public event EventHandler<CardStatusEventArgs>? CardInserted;
    public event EventHandler<CardStatusEventArgs>? CardRemoved;
    public event EventHandler<ReaderStatusEventArgs>? ReaderArrival;
    public event EventHandler<ReaderStatusEventArgs>? ReaderRemoval;

    public PcscService(ILogger<PcscService> logger)
    {
        _logger = logger;
    }

    public Task InitAsync(CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            if (_status == HardwareStatus.Running)
                return Task.CompletedTask;

            var rc = NativeMethods.EstablishContext(NativeMethods.SCardScopeSystem, out _context);
            if (rc != Success)
            {
                _status = HardwareStatus.Error;
                _logger.LogError("SCardEstablishContext failed: 0x{Code:X8}", rc);
                throw new InvalidOperationException($"PCSC context initialization failed: 0x{rc:X8}");
            }

            _status = HardwareStatus.Running;
            _logger.LogInformation("PCSC service started");

            _monitorCts = new CancellationTokenSource();
            _monitorThread = new Thread(() => MonitorReaders(_monitorCts.Token))
            {
                IsBackground = true,
                Name = "PcscMonitor"
            };
            _monitorThread.Start();
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        CancellationTokenSource? cts;
        Thread? monitorThread;

        lock (_syncLock)
        {
            cts = _monitorCts;
            monitorThread = _monitorThread;
            _monitorCts = null;
            _monitorThread = null;
        }

        cts?.Cancel();
        monitorThread?.Join(5000);
        cts?.Dispose();

        lock (_syncLock)
        {
            if (_context != nint.Zero)
            {
                NativeMethods.ReleaseContext(_context);
                _context = nint.Zero;
            }

            _status = HardwareStatus.Stopped;
            _logger.LogInformation("PCSC service stopped");
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReaderInfo>> ListReadersAsync(CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            if (_context == nint.Zero)
                return Task.FromResult<IReadOnlyList<ReaderInfo>>([]);
            var readers = GetReaderNames();
            var infos = readers.Select(r => GetReaderInfoInternal(r)).ToList();
            return Task.FromResult<IReadOnlyList<ReaderInfo>>(infos);
        }
    }

    public Task<ReaderInfo> GetReaderInfoAsync(string readerName, CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            if (_context == nint.Zero)
                return Task.FromResult(new ReaderInfo(readerName, false));
            return Task.FromResult(GetReaderInfoInternal(readerName));
        }
    }

    public Task<string?> GetAtrAsync(string readerName, CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            if (_context == nint.Zero)
                return Task.FromResult<string?>(null);

            var rc = NativeMethods.Connect(_context, readerName,
                NativeMethods.SCardShareShared, NativeMethods.SCardProtocolTx,
                out var hCard, out _);

            if (rc != Success)
                return Task.FromResult<string?>(null);

            try
            {
                return Task.FromResult(GetAtrFromHandle(hCard));
            }
            finally
            {
                NativeMethods.Disconnect(hCard, NativeMethods.SCardLeaveCard);
            }
        }
    }

    public Task<string?> ResetCardAsync(string readerName, CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            if (_context == nint.Zero)
                return Task.FromResult<string?>(null);

            var rc = NativeMethods.Connect(_context, readerName,
                NativeMethods.SCardShareShared, NativeMethods.SCardProtocolTx,
                out var hCard, out var protocol);

            if (rc != Success)
                return Task.FromResult<string?>(null);

            try
            {
                rc = NativeMethods.Reconnect(hCard, NativeMethods.SCardShareShared,
                    protocol, NativeMethods.SCardResetCard, out _);
                if (rc != Success)
                    return Task.FromResult<string?>(null);

                return Task.FromResult(GetAtrFromHandle(hCard));
            }
            finally
            {
                NativeMethods.Disconnect(hCard, NativeMethods.SCardLeaveCard);
            }
        }
    }

    public Task<TransmitResult> TransmitAsync(string readerName, string apdu, CancellationToken ct = default)
    {
        lock (_syncLock)
        {
            if (_context == nint.Zero)
                return Task.FromResult(new TransmitResult(false, ErrorMessage: "PCSC service not running", ErrorCode: "SERVICE_NOT_RUNNING"));
            return TransmitInternal(readerName, apdu);
        }
    }

    private Task<TransmitResult> TransmitInternal(string readerName, string apdu)
    {
        var rc = NativeMethods.Connect(_context, readerName,
            NativeMethods.SCardShareShared, NativeMethods.SCardProtocolTx,
            out var hCard, out var protocol);

        if (rc != Success)
        {
            var (errorCode, errorMessage) = MapPcscStatus((uint)rc);
            return Task.FromResult(new TransmitResult(false,
                ErrorMessage: errorMessage,
                ErrorCode: errorCode));
        }

        try
        {
            byte[] apduBytes;
            try { apduBytes = Convert.FromHexString(apdu); }
            catch (Exception)
            {
                return Task.FromResult(new TransmitResult(false, ErrorMessage: "Invalid APDU format", ErrorCode: "INVALID_PARAMETERS"));
            }

            var sendPci = new SCardIORequest { Protocol = protocol, Length = 8 };
            var recvPci = new SCardIORequest { Protocol = protocol, Length = 8 };
            var recvBuf = new byte[256];
            var recvLen = (uint)recvBuf.Length;

            rc = NativeMethods.Transmit(hCard, ref sendPci, apduBytes, (uint)apduBytes.Length,
                ref recvPci, recvBuf, ref recvLen);

            if (rc != Success)
            {
                var (errorCode, errorMessage) = MapPcscStatus((uint)rc);
                return Task.FromResult(new TransmitResult(false,
                    ErrorMessage: errorMessage,
                    ErrorCode: errorCode));
            }

            Array.Resize(ref recvBuf, (int)recvLen);

            var sw1 = recvLen >= 2 ? recvBuf[^2].ToString("X2") : null;
            var sw2 = recvLen >= 2 ? recvBuf[^1].ToString("X2") : null;
            var data = recvLen > 2
                ? BytesToHex(recvBuf[..^2])
                : null;

            return Task.FromResult(new TransmitResult(true, Sw1: sw1, Sw2: sw2, ResponseData: data));
        }
        finally
        {
            NativeMethods.Disconnect(hCard, NativeMethods.SCardLeaveCard);
        }
    }

    public void Dispose()
    {
        ShutdownAsync().GetAwaiter().GetResult();
    }

    private List<string> GetReaderNames()
    {
        var charsLen = 0u;
        var rc = NativeMethods.ListReaders(_context, null, ref charsLen);

        if (rc != Success || charsLen == 0)
            return [];

        if (OperatingSystem.IsWindows())
        {
            // SCardListReadersW — pcchReaders is in WCHARs (UTF-16)
            var buf = new byte[charsLen * 2];
            rc = NativeMethods.ListReaders(_context, buf, ref charsLen);
            if (rc != Success)
                return [];
            var str = Encoding.Unicode.GetString(buf, 0, (int)charsLen * 2);
            return str.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        else
        {
            // SCardListReaders (ANSI/UTF-8) — pcchReaders is in bytes
            var buf = new byte[charsLen];
            rc = NativeMethods.ListReaders(_context, buf, ref charsLen);
            if (rc != Success)
                return [];
            var str = Encoding.UTF8.GetString(buf, 0, (int)charsLen);
            return str.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }

    private ReaderInfo GetReaderInfoInternal(string readerName)
    {
        var rc = NativeMethods.Connect(_context, readerName,
            NativeMethods.SCardShareShared, NativeMethods.SCardProtocolTx,
            out var hCard, out _);

        if (rc != Success)
            return new ReaderInfo(readerName, false);

        try
        {
            var atr = GetAtrFromHandle(hCard);
            return new ReaderInfo(readerName, true, atr);
        }
        finally
        {
            NativeMethods.Disconnect(hCard, NativeMethods.SCardLeaveCard);
        }
    }

    private void MonitorReaders(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                List<(string Name, bool WasPresent, bool IsPresent)> changes;

                lock (_syncLock)
                {
                    if (_context == nint.Zero)
                        break;

                    var currentReaderNames = GetReaderNames();

                    foreach (var name in currentReaderNames)
                    {
                        if (!_lastReaderStates.ContainsKey(name))
                        {
                            _lastReaderStates[name] = false;
                            _logger.LogInformation("New reader detected: {Reader}", name);
                            ReaderArrival?.Invoke(this, new ReaderStatusEventArgs(name, "arrived"));
                        }
                    }

                    foreach (var name in _lastReaderStates.Keys.ToList())
                    {
                        if (!currentReaderNames.Contains(name))
                        {
                            _lastReaderStates.Remove(name);
                            _logger.LogInformation("Reader removed: {Reader}", name);
                            ReaderRemoval?.Invoke(this, new ReaderStatusEventArgs(name, "removed"));
                        }
                    }

                    changes = new List<(string, bool, bool)>();
                    foreach (var name in currentReaderNames)
                    {
                        var info = GetReaderInfoInternal(name);
                        var wasPresent = _lastReaderStates.TryGetValue(name, out var prev) && prev;

                        if (info.IsCardPresent != wasPresent)
                        {
                            _lastReaderStates[name] = info.IsCardPresent;
                            changes.Add((name, wasPresent, info.IsCardPresent));
                        }
                    }

                    _monitorConsecutiveFailures = 0;
                }

                foreach (var (name, wasPresent, isPresent) in changes)
                {
                    var args = new CardStatusEventArgs(name,
                        wasPresent ? "card_present" : "empty",
                        isPresent ? "card_present" : "empty");

                    if (isPresent && !wasPresent)
                        CardInserted?.Invoke(this, args);
                    else if (wasPresent && !isPresent)
                        CardRemoved?.Invoke(this, args);
                }

                Thread.Sleep(1000);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _monitorConsecutiveFailures++;
                if (_monitorConsecutiveFailures >= 5)
                {
                    lock (_syncLock)
                        _status = HardwareStatus.Error;
                    _logger.LogError(ex, "Reader monitor failed {Count} consecutive times, entering error state", _monitorConsecutiveFailures);
                    break;
                }
                _logger.LogWarning(ex, "Reader monitor exception ({Count} consecutive failures)", _monitorConsecutiveFailures);
                Thread.Sleep(5000);
            }
        }
    }

    private static string BytesToHex(byte[] bytes)
        => BitConverter.ToString(bytes).Replace("-", "");

    private static string? GetAtrFromHandle(nint hCard)
    {
        var atrLen = 33u;
        var atrBuf = new byte[atrLen];
        var dummyLen = 0u;
        var rc = NativeMethods.Status(hCard, null, ref dummyLen, out _, out _, atrBuf, ref atrLen);
        if (rc != Success || atrLen == 0)
            return null;
        Array.Resize(ref atrBuf, (int)atrLen);
        return BytesToHex(atrBuf);
    }
}
