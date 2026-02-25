using FlowNoteMauiApp.Data.Repositories;
using System.Text.Json;

namespace FlowNoteMauiApp.Services;

public class FileRepository : IFileRepository
{
    private readonly List<NoteFileInfo> _files = new();
    private readonly string _basePath;

    public FileRepository()
    {
        _basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlowNote");
        Directory.CreateDirectory(_basePath);
    }

    public Task<NoteFileInfo?> GetFileAsync(string id)
    {
        var file = _files.FirstOrDefault(f => f.Id == id);
        return Task.FromResult(file);
    }

    public Task<List<NoteFileInfo>> GetAllFilesAsync()
    {
        return Task.FromResult(_files.ToList());
    }

    public Task<bool> SaveFileAsync(NoteFileInfo file)
    {
        var existing = _files.FirstOrDefault(f => f.Id == file.Id);
        if (existing != null)
        {
            _files.Remove(existing);
        }
        _files.Add(file);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteFileAsync(string id)
    {
        var file = _files.FirstOrDefault(f => f.Id == id);
        if (file != null)
        {
            _files.Remove(file);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<byte[]?> GetFileContentAsync(string id)
    {
        var file = _files.FirstOrDefault(f => f.Id == id);
        if (file != null && File.Exists(file.Path))
        {
            return Task.FromResult<byte[]?>(File.ReadAllBytes(file.Path));
        }
        return Task.FromResult<byte[]?>(null);
    }
}

public class SettingsRepository : ISettingsRepository
{
    private readonly Dictionary<string, object> _settings = new();
    private readonly string _settingsPath;

    public SettingsRepository()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlowNote", "settings.json");
        Load();
    }

    public T? GetSetting<T>(string key)
    {
        if (_settings.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    public void SetSetting<T>(string key, T value)
    {
        _settings[key] = value!;
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            var json = JsonSerializer.Serialize(_settings);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
        }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (settings != null)
                {
                    foreach (var kvp in settings)
                    {
                        _settings[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch
        {
        }
    }
}
