using System.Threading;
using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions;
using DeviceHub.Devices.Contracts.Abstractions.Services;
using DeviceHub.Devices.Printer.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace DeviceHub.Devices.Printer.Services;

public class MockPrinterService : IPrinterService, IHardwareEndpointRegistrar
{
    private readonly ILogger<MockPrinterService> _logger;
    private HardwareStatus _status = HardwareStatus.Stopped;
    private int _jobCounter;

    public string Name => "Printer";
    public HardwareStatus Status => _status;

    public event EventHandler<PrinterJobEventArgs>? JobCompleted;
    public event EventHandler<PrinterJobEventArgs>? JobError;

    public MockPrinterService(ILogger<MockPrinterService> logger) => _logger = logger;

    public Task InitAsync(CancellationToken ct = default)
    {
        _status = HardwareStatus.Running;
        _logger.LogInformation("Mock printer service started");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _status = HardwareStatus.Stopped;
        _logger.LogInformation("Mock printer service stopped");
        return Task.CompletedTask;
    }

    public void MapEndpoints(IEndpointRouteBuilder app) => PrinterEndpoint.MapEndpoints(app);

    public Task<List<PrinterInfo>> GetPrintersAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<PrinterInfo>
        {
            new("Mock Printer 1", "ready", true, "Mock thermal printer", "Counter #1"),
            new("Mock Printer 2", "ready", false, "Mock label printer", "Storage #2")
        });
    }

    public Task<bool> PrintTextAsync(string text, string? printerName = null, CancellationToken ct = default)
    {
        var name = printerName ?? "Mock Printer 1";
        _logger.LogInformation("Mock print text ({Printer}): {Text}", name, text);

        var jobId = $"print-{Interlocked.Increment(ref _jobCounter)}";
        JobCompleted?.Invoke(this, new PrinterJobEventArgs(jobId, name, "completed"));
        return Task.FromResult(true);
    }

    public Task<bool> PrintRawAsync(byte[] data, string? printerName = null, CancellationToken ct = default)
    {
        var name = printerName ?? "Mock Printer 1";
        _logger.LogInformation("Mock print raw ({Printer}): {Length} bytes", name, data.Length);

        var jobId = $"print-{Interlocked.Increment(ref _jobCounter)}";
        JobCompleted?.Invoke(this, new PrinterJobEventArgs(jobId, name, "completed"));
        return Task.FromResult(true);
    }
}
