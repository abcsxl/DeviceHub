using System.Collections.Concurrent;
using System.Text.Json;

namespace DeviceHub.Service.Api.Services;

public class ConfigStoreService : IDisposable
{
    private readonly string _filePath;
    private readonly ConcurrentDictionary<string, string> _store = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConfigStoreService()
    {
        var dbDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dbDir);
        _filePath = Path.Combine(dbDir, "config.json");
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null)
            {
                foreach (var kv in dict)
                    _store[kv.Key] = kv.Value;
            }
        }
        catch
        {
            // 文件损坏时忽略，使用空存储
        }
    }

    private async Task SaveCoreAsync()
    {
        var dict = _store.ToDictionary(kv => kv.Key, kv => kv.Value);
        var json = JsonSerializer.Serialize(dict, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public Task<string?> GetAsync(string key)
    {
        _store.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task<List<KeyValuePair<string, string>>> GetAllAsync()
    {
        var items = _store.OrderBy(kv => kv.Key)
            .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value))
            .ToList();
        return Task.FromResult(items);
    }

    public async Task SetAsync(string key, string value)
    {
        await _writeLock.WaitAsync();
        try
        {
            _store[key] = value;
            await SaveCoreAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        await _writeLock.WaitAsync();
        try
        {
            if (!_store.TryRemove(key, out _)) return false;
            await SaveCoreAsync();
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            _store.Clear();
            await SaveCoreAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }
}
