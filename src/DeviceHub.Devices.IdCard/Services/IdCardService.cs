using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.IdCard.Helpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Devices.IdCard.Services;

public class IdCardService : IIdCardService, IHardwareEndpointRegistrar
{
    private readonly ILogger<IdCardService> _logger;
    private HardwareStatus _status = HardwareStatus.Stopped;

    public string Name => "IdCard";
    public HardwareStatus Status => _status;

    public IdCardService(ILogger<IdCardService> logger) => _logger = logger;

    public Task InitAsync(CancellationToken ct = default)
    {
        _status = HardwareStatus.Running;
        _logger.LogInformation("IdCard service started");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _status = HardwareStatus.Stopped;
        _logger.LogInformation("IdCard service stopped");
        return Task.CompletedTask;
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => IdCardEndpointHelper.MapEndpoints(app);

    public Task<List<ReaderInfo>> GetReadersAsync(CancellationToken ct = default)
    {
        var readers = new List<ReaderInfo>();

        try
        {
            readers.Add(new ReaderInfo("IdCard Reader (Default)", false));
        }
        catch
        {
            readers.Add(new ReaderInfo("IdCard Reader (Default)", false));
        }

        return Task.FromResult(readers);
    }

    public Task<IdCardInfo?> ReadCardAsync(string? readerName = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Read ID card from {Reader}", readerName ?? "default");
        return Task.FromResult<IdCardInfo?>(null);
    }

    public Task<byte[]?> ReadPhotoAsync(string? readerName = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Read ID card photo from {Reader}", readerName ?? "default");
        return Task.FromResult<byte[]?>(null);
    }
}
