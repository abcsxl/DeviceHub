using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions;
using DeviceHub.Devices.Contracts.Abstractions.Services;
using DeviceHub.Devices.IdCard.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Devices.IdCard.Services;

public class MockIdCardService : IIdCardService, IHardwareEndpointRegistrar, IDisposable
{
    private readonly ILogger<MockIdCardService> _logger;
    private HardwareStatus _status = HardwareStatus.Stopped;
    private CancellationTokenSource? _monitorCts;
    private Thread? _monitorThread;
    private bool _cardPresent;

    private const string MockIdCardReader = "Mock IdCard Reader 1";

    public string Name => "IdCard";
    public HardwareStatus Status => _status;

    public event EventHandler<CardStatusEventArgs>? CardInserted;
    public event EventHandler<CardStatusEventArgs>? CardRemoved;

    public MockIdCardService(ILogger<MockIdCardService> logger) => _logger = logger;

    public Task InitAsync(CancellationToken ct = default)
    {
        if (_status == HardwareStatus.Running)
            return Task.CompletedTask;

        _cardPresent = true;
        _status = HardwareStatus.Running;
        _logger.LogInformation("[Mock] IdCard mock service started, reader: {Reader}", MockIdCardReader);

        _monitorCts = new CancellationTokenSource();
        _monitorThread = new Thread(() => MonitorCardState(_monitorCts.Token))
        {
            IsBackground = true,
            Name = "MockIdCardMonitor"
        };
        _monitorThread.Start();

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _monitorCts?.Cancel();
        _monitorThread?.Join(3000);
        _monitorCts?.Dispose();
        _monitorCts = null;
        _monitorThread = null;

        _status = HardwareStatus.Stopped;
        _logger.LogInformation("[Mock] IdCard mock service stopped");
        return Task.CompletedTask;
    }

    public void Dispose() => ShutdownAsync().GetAwaiter().GetResult();

    public void MapEndpoints(IEndpointRouteBuilder app) => IdCardEndpoint.MapEndpoints(app);

    public Task<List<ReaderInfo>> GetReadersAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<ReaderInfo>
        {
            new(MockIdCardReader, _cardPresent),
            new("Mock IdCard Reader 2", false)
        });
    }

    public Task<IdCardInfo?> ReadCardAsync(string? readerName = null, CancellationToken ct = default)
    {
        if (!_cardPresent)
            return Task.FromResult<IdCardInfo?>(null);

        _logger.LogInformation("[Mock] Mock read ID card from {Reader}", readerName ?? "default");

        return Task.FromResult<IdCardInfo?>(new IdCardInfo(
            Name: "张三",
            Gender: "男",
            Ethnicity: "汉",
            BirthDate: "19900101",
            Address: "北京市朝阳区建国路100号",
            IdNumber: "110101199001010031",
            IssuingAuthority: "北京市公安局朝阳分局",
            ValidFrom: "20200101",
            ValidTo: "20400101"
        ));
    }

    public Task<byte[]?> ReadPhotoAsync(string? readerName = null, CancellationToken ct = default)
    {
        var placeholder = Convert.FromHexString("FFD8FFE000104A46494600010101004800480000FFDB004300FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFC00011080001000103012200021101031101FFF900040001FFDA000C0301000211031100000000FFD9");
        return Task.FromResult<byte[]?>(placeholder);
    }

    private void MonitorCardState(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Thread.Sleep(15000);
                if (ct.IsCancellationRequested)
                    break;

                _cardPresent = !_cardPresent;
                var status = _cardPresent ? "card_present" : "empty";
                _logger.LogInformation("[Mock] IdCard state changed: {Reader} -> {Status}", MockIdCardReader, status);

                if (_cardPresent)
                    CardInserted?.Invoke(this, new CardStatusEventArgs(MockIdCardReader, "empty", "card_present"));
                else
                    CardRemoved?.Invoke(this, new CardStatusEventArgs(MockIdCardReader, "card_present", "empty"));
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "[Mock] IdCard monitor exception");
            }
        }
    }
}
