using System.Text;
using DeviceHub.Devices.Contracts;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Devices.PcscReader;

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

    public string Name => "Pcsc";
    public HardwareStatus Status => _status;

    public event EventHandler<CardStatusEventArgs>? CardStatusChanged;

    public PcscService(ILogger<PcscService> logger)
    {
        _logger = logger;
    }

    public Task InitAsync(CancellationToken ct = default)
    {
        lock (this)
        {
            if (_status == HardwareStatus.Running)
                return Task.CompletedTask;

            var rc = NativeMethods.EstablishContext(NativeMethods.SCardScopeSystem, out _context);
            if (rc != Success)
            {
                _status = HardwareStatus.Error;
                _logger.LogError("SCardEstablishContext 失败: 0x{Code:X8}", rc);
                throw new InvalidOperationException($"PCSC 上下文初始化失败: 0x{rc:X8}");
            }

            _status = HardwareStatus.Running;
            _logger.LogInformation("PCSC 服务已启动");

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
        lock (this)
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
            _logger.LogInformation("PCSC 服务已关闭");
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
                return Task.FromResult(new TransmitResult(false, ErrorMessage: "PCSC 服务未启动"));
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
                ErrorMessage: $"连接读卡器失败: 0x{rc:X8}"));

        try
        {
            var apduBytes = HexToBytes(apdu);
            if (apduBytes == null)
                return Task.FromResult(new TransmitResult(false, ErrorMessage: "APDU 格式错误"));

            var sendPci = new SCardIORequest { Protocol = protocol, Length = 8 };
            var recvPci = new SCardIORequest { Protocol = protocol, Length = 8 };
            var recvBuf = new byte[256];
            var recvLen = (uint)recvBuf.Length;

            rc = NativeMethods.Transmit(hCard, ref sendPci, apduBytes, (uint)apduBytes.Length,
                ref recvPci, recvBuf, ref recvLen);

            if (rc != Success)
            {
                return Task.FromResult(new TransmitResult(false,
                    ErrorMessage: $"发送失败: 0x{rc:X8}"));
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
        GC.SuppressFinalize(this);
    }

    private List<string> GetReaderNames()
    {
        var bufLen = 0u;
        var rc = NativeMethods.ListReaders(_context, null, ref bufLen);

        if (rc != Success || bufLen == 0)
            return [];

        var buf = new byte[bufLen];
        rc = NativeMethods.ListReaders(_context, buf, ref bufLen);

        if (rc != Success || buf.Length == 0)
            return [];

        var names = new List<string>();
        var offset = 0;

        while (offset < buf.Length)
        {
            var end = Array.IndexOf(buf, (byte)0, offset);
            if (end < 0 || end == offset)
                break;

            var name = Encoding.ASCII.GetString(buf, offset, end - offset);
            if (!string.IsNullOrEmpty(name))
                names.Add(name);

            offset = end + 1;
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
                var currentReaderNames = GetReaderNames();

                foreach (var name in currentReaderNames)
                {
                    if (!_lastReaderStates.ContainsKey(name))
                    {
                        _lastReaderStates[name] = false;
                        _logger.LogInformation("新读卡器接入: {Reader}", name);
                    }
                }

                foreach (var name in _lastReaderStates.Keys.ToList())
                {
                    if (!currentReaderNames.Contains(name))
                    {
                        _lastReaderStates.Remove(name);
                        _logger.LogInformation("读卡器移除: {Reader}", name);
                    }
                }

                foreach (var name in currentReaderNames)
                {
                    var info = GetReaderInfoInternal(name);
                    var wasPresent = _lastReaderStates.TryGetValue(name, out var prev) && prev;

                    if (info.IsCardPresent != wasPresent)
                    {
                        _lastReaderStates[name] = info.IsCardPresent;
                        CardStatusChanged?.Invoke(this,
                            new CardStatusEventArgs(name,
                                wasPresent ? "card_present" : "empty",
                                info.IsCardPresent ? "card_present" : "empty"));
                    }
                }

                Thread.Sleep(1000);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读卡器监控异常");
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
