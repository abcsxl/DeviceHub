using DeviceHub.Devices.Contracts;
using DeviceHub.Devices.Contracts.Abstractions.Services;

namespace DeviceHub.Service.Api.Models;

public class DriverRegistry
{
    private readonly Dictionary<string, DriverEntry> _entries = new();
    private readonly object _lock = new();

    public void Register(string name, IHardwareService service)
    {
        lock (_lock)
        {
            _entries[name] = new DriverEntry(name, service, true);
        }
    }

    public void Unregister(string name)
    {
        lock (_lock)
        {
            _entries.Remove(name);
        }
    }

    public DriverEntry? Get(string name)
    {
        lock (_lock)
        {
            return _entries.GetValueOrDefault(name);
        }
    }

    public List<DriverInfo> GetAll()
    {
        lock (_lock)
        {
            return _entries.Values.Select(e => new DriverInfo(
                e.Name,
                e.Service.Status.ToString(),
                e.Enabled,
                e.RegisteredAt
            )).ToList();
        }
    }
}

public class DriverEntry
{
    public string Name { get; }
    public IHardwareService Service { get; }
    public bool Enabled { get; set; }
    public DateTime RegisteredAt { get; }

    public DriverEntry(string name, IHardwareService service, bool enabled)
    {
        Name = name;
        Service = service;
        Enabled = enabled;
        RegisteredAt = DateTime.UtcNow;
    }
}
