using System.Collections.Concurrent;
using DeviceHub.Devices.Contracts;

namespace DeviceHub.Service.Api.Models;

public class InMemoryLogProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly int _capacity;
    private readonly Dictionary<string, LogLevel> _minLevels;
    private IDisposable? _reloadToken;

    public InMemoryLogProvider(IConfiguration configuration)
    {
        _capacity = Math.Max(10, configuration.GetValue<int>("Logging:RingBufferSize", 1000));
        _minLevels = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase);
        ReloadLevels(configuration);

        if (configuration is IConfigurationRoot root)
            _reloadToken = root.GetReloadToken().RegisterChangeCallback(_ => OnConfigReload(root), null);
    }

    private void OnConfigReload(IConfigurationRoot root)
    {
        ReloadLevels(root);
        _reloadToken = root.GetReloadToken().RegisterChangeCallback(_ => OnConfigReload(root), null);
    }

    private void ReloadLevels(IConfiguration config)
    {
        _minLevels.Clear();
        var levelSection = config.GetSection("Logging:LogLevel");
        foreach (var kv in levelSection.GetChildren())
        {
            if (Enum.TryParse<LogLevel>(kv.Value, true, out var level))
                _minLevels[kv.Key] = level;
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new InMemoryLogger(categoryName, _entries, _capacity, _minLevels);
    }

    public List<LogEntry> GetLogs(string? level = null, int tail = 100)
    {
        var query = _entries.AsEnumerable();
        if (!string.IsNullOrEmpty(level))
        {
            var upper = level.ToUpperInvariant();
            query = query.Where(e => e.Level == upper);
        }
        return query.TakeLast(tail).ToList();
    }

    public void Dispose()
    {
        _reloadToken?.Dispose();
        GC.SuppressFinalize(this);
    }

    private class InMemoryLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ConcurrentQueue<LogEntry> _entries;
        private readonly int _capacity;
        private readonly Dictionary<string, LogLevel> _minLevels;

        public InMemoryLogger(
            string categoryName,
            ConcurrentQueue<LogEntry> entries,
            int capacity,
            Dictionary<string, LogLevel> minLevels)
        {
            _categoryName = categoryName;
            _entries = entries;
            _capacity = capacity;
            _minLevels = minLevels;
        }

        private LogLevel ResolveMinLevel(string category)
        {
            if (_minLevels.TryGetValue(category, out var level))
                return level;
            if (_minLevels.TryGetValue("Default", out var defaultLevel))
                return defaultLevel;
            return LogLevel.Information;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= ResolveMinLevel(_categoryName);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var entry = new LogEntry(
                DateTime.UtcNow,
                logLevel.ToString().ToUpperInvariant(),
                _categoryName,
                formatter(state, exception),
                exception?.ToString()
            );

            _entries.Enqueue(entry);
            while (_entries.Count > _capacity && _entries.TryDequeue(out _))
            {
            }
        }
    }
}
