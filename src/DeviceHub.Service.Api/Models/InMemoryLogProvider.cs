using System.Collections.Concurrent;
using DeviceHub.Devices.Contracts;

namespace DeviceHub.Service.Api.Models;

public class InMemoryLogProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly int _capacity;

    public InMemoryLogProvider(IConfiguration configuration)
    {
        _capacity = configuration.GetValue<int>("Logging:RingBufferSize", 1000);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new InMemoryLogger(categoryName, _entries, _capacity);
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
        GC.SuppressFinalize(this);
    }

    private class InMemoryLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ConcurrentQueue<LogEntry> _entries;
        private readonly int _capacity;

        public InMemoryLogger(
            string categoryName,
            ConcurrentQueue<LogEntry> entries,
            int capacity)
        {
            _categoryName = categoryName;
            _entries = entries;
            _capacity = capacity;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

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
