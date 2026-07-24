using System.Text;
using DeviceHub.Devices.Contracts.Abstractions;

namespace DeviceHub.Service.Api.Services;

public class ApduTraceWriter : IApduTraceWriter, IDisposable
{
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private bool _enabled;
    private readonly string _filePath;

    public ApduTraceWriter(IConfiguration configuration)
    {
        _enabled = configuration.GetValue<bool>("Drivers:Pcsc:ApduTraceEnabled");
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "logs");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "apdu.log");
    }

    public bool IsEnabled => _enabled;

    public void Write(string message)
    {
        if (!_enabled) return;
        lock (_lock)
        {
            _writer ??= new StreamWriter(_filePath, append: true, Encoding.UTF8) { AutoFlush = true };
            _writer.WriteLine(message);
        }
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        lock (_lock)
        {
            if (!enabled)
            {
                _writer?.Close();
                _writer = null;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Close();
            _writer = null;
        }
    }
}
