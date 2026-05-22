using System.Text;
using DeviceHub.Devices.Contracts;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Devices.PcscReader.Services;

public class PcscService : IPcscService, IDisposable
{
    private const uint Success = 0;

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

    public event EventHandler<CardStatusEventArgs>? CardStatusChanged;

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
        lock (_syncLock)
        {
            _monitorCts?.Cancel();
            _monitorThread?.Join(5000);
            _monitorCts?.Dispose();
            _monitorCts = null;
            _monitorThread = null;

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
                var atrLen = 0u;
                rc = NativeMethods.GetAttrib(hCard, NativeMethods.SCardAttrAtrString, null, ref atrLen);
                if (rc != Success || atrLen == 0)
                    return Task.FromResult<string?>(null);

                var atrBuf = new byte[atrLen];
                rc = NativeMethods.GetAttrib(hCard, NativeMethods.SCardAttrAtrString, atrBuf, ref atrLen);
                return Task.FromResult(rc == Success ? BytesToHex(atrBuf) : null);
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
                return Task.FromResult(new TransmitResult(false, ErrorMessage: "PCSC service not running", ErrorCode: "HARDWARE_ERROR"));
            return TransmitInternal(readerName, apdu);
        }
    }

    private Task<TransmitResult> TransmitInternal(string readerName, string apdu)
    {
        var rc = NativeMethods.Connect(_context, readerName,
            NativeMethods.SCardShareShared, NativeMethods.SCardProtocolTx,
            out var hCard, out var protocol);

        if (rc != Success)
            return Task.FromResult(new TransmitResult(false,
                ErrorMessage: $"Failed to connect to reader: 0x{rc:X8}",
                ErrorCode: "READER_ERROR"));

        try
        {
            var apduBytes = HexToBytes(apdu);
            if (apduBytes == null)
                return Task.FromResult(new TransmitResult(false, ErrorMessage: "Invalid APDU format", ErrorCode: "INVALID_PARAMETERS"));

            var sendPci = new SCardIORequest { Protocol = protocol, Length = 8 };
            var recvPci = new SCardIORequest { Protocol = protocol, Length = 8 };
            var recvBuf = new byte[256];
            var recvLen = (uint)recvBuf.Length;

            rc = NativeMethods.Transmit(hCard, ref sendPci, apduBytes, (uint)apduBytes.Length,
                ref recvPci, recvBuf, ref recvLen);

            if (rc != Success)
            {
                return Task.FromResult(new TransmitResult(false,
                    ErrorMessage: $"Transmit failed: 0x{rc:X8}",
                    ErrorCode: "HARDWARE_ERROR"));
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
        var bufLen = 0u;
        var rc = NativeMethods.ListReaders(_context, null, ref bufLen);

        if (rc != Success || bufLen == 0)
            return [];

        // On Windows, SCardListReadersW returns length in WCHARs (2 bytes each)
        if (OperatingSystem.IsWindows())
            bufLen *= 2;

        var buf = new byte[bufLen];
        rc = NativeMethods.ListReaders(_context, buf, ref bufLen);

        if (rc != Success || buf.Length == 0)
            return [];

        var names = new List<string>();
        var offset = 0;

        var encoding = OperatingSystem.IsWindows() ? Encoding.Unicode : Encoding.UTF8;

        while (offset < buf.Length)
        {
            var end = Array.IndexOf(buf, (byte)0, offset);
            if (end < 0 || end == offset)
                break;

            var name = encoding.GetString(buf, offset, end - offset);
            if (!string.IsNullOrEmpty(name))
                names.Add(name);

            offset = end + (OperatingSystem.IsWindows() ? 2 : 1);
        }

        return names;
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
            var atrLen = 0u;
            rc = NativeMethods.GetAttrib(hCard, NativeMethods.SCardAttrAtrString, null, ref atrLen);
            if (rc != Success || atrLen == 0)
                return new ReaderInfo(readerName, true);

            var atrBuf = new byte[atrLen];
            rc = NativeMethods.GetAttrib(hCard, NativeMethods.SCardAttrAtrString, atrBuf, ref atrLen);
            var atr = rc == Success ? BytesToHex(atrBuf) : null;

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
                        }
                    }

                    foreach (var name in _lastReaderStates.Keys.ToList())
                    {
                        if (!currentReaderNames.Contains(name))
                        {
                            _lastReaderStates.Remove(name);
                            _logger.LogInformation("Reader removed: {Reader}", name);
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
                    CardStatusChanged?.Invoke(this,
                        new CardStatusEventArgs(name,
                            wasPresent ? "card_present" : "empty",
                            isPresent ? "card_present" : "empty"));
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

    private static byte[]? HexToBytes(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
            return null;

        try
        {
            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private static string BytesToHex(byte[] bytes)
        => BitConverter.ToString(bytes).Replace("-", "");
}
