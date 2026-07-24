using System.Text;
using DeviceHub.Devices.Contracts;

namespace DeviceHub.Service.Api.Models;

public class FileLogProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly Dictionary<string, LogLevel> _minLevels;
    private readonly object _lock = new();

    public FileLogProvider(IConfiguration configuration)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "data", "logs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"devicehub-{DateTime.Now:yyyyMMdd}.log");
        _writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };
        _minLevels = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase);
        ReloadLevels(configuration);

        if (configuration is IConfigurationRoot root)
            _ = root.GetReloadToken().RegisterChangeCallback(_ => ReloadLevels(root), null);
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
        return new FileLogger(categoryName, _writer, _minLevels, _lock);
    }

    public void Dispose()
    {
        lock (_lock) { _writer.Close(); }
        GC.SuppressFinalize(this);
    }

    private class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly StreamWriter _writer;
        private readonly Dictionary<string, LogLevel> _minLevels;
        private readonly object _lock;

        public FileLogger(
            string categoryName,
            StreamWriter writer,
            Dictionary<string, LogLevel> minLevels,
            object lockObj)
        {
            _categoryName = categoryName;
            _writer = writer;
            _minLevels = minLevels;
            _lock = lockObj;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (_minLevels.TryGetValue(_categoryName, out var level))
                return logLevel >= level;
            if (_minLevels.TryGetValue("Default", out var defaultLevel))
                return logLevel >= defaultLevel;
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var now = DateTime.Now;
            var message = formatter(state, exception);
            lock (_lock)
            {
                _writer.WriteLine($"{now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel,-11}] {_categoryName}: {message}");
                if (exception != null)
                    _writer.WriteLine(exception);
            }
        }
    }
}
