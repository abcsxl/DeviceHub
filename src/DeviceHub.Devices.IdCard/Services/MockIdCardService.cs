using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.IdCard.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Devices.IdCard.Services;

public class MockIdCardService : IIdCardService, IHardwareEndpointRegistrar
{
    private readonly ILogger<MockIdCardService> _logger;
    private HardwareStatus _status = HardwareStatus.Stopped;

    public string Name => "IdCard";
    public HardwareStatus Status => _status;

    public MockIdCardService(ILogger<MockIdCardService> logger) => _logger = logger;

    public Task InitAsync(CancellationToken ct = default)
    {
        _status = HardwareStatus.Running;
        _logger.LogInformation("Mock IdCard service started");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _status = HardwareStatus.Stopped;
        _logger.LogInformation("Mock IdCard service stopped");
        return Task.CompletedTask;
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => IdCardEndpoint.MapEndpoints(app);

    public Task<List<ReaderInfo>> GetReadersAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<ReaderInfo>
        {
            new("Mock IdCard Reader 1", false),
            new("Mock IdCard Reader 2", false)
        });
    }

    public Task<IdCardInfo?> ReadCardAsync(string? readerName = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Mock read ID card from {Reader}", readerName ?? "default");

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
        // Return a 1x1 pixel JPEG placeholder
        var placeholder = Convert.FromHexString("FFD8FFE000104A46494600010101004800480000FFDB004300FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFC00011080001000103012200021101031101FFF900040001FFDA000C0301000211031100000000FFD9");
        return Task.FromResult<byte[]?>(placeholder);
    }
}
