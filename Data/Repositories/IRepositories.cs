namespace FlowNoteMauiApp.Data.Repositories;

public interface IFileRepository
{
    Task<NoteFileInfo?> GetFileAsync(string id);
    Task<List<NoteFileInfo>> GetAllFilesAsync();
    Task<bool> SaveFileAsync(NoteFileInfo file);
    Task<bool> DeleteFileAsync(string id);
    Task<byte[]?> GetFileContentAsync(string id);
}

public interface ISettingsRepository
{
    T? GetSetting<T>(string key);
    void SetSetting<T>(string key, T value);
    void Save();
    void Load();
}

public class NoteFileInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public long Size { get; set; }
    public string? ThumbnailPath { get; set; }
    public List<string> Tags { get; set; } = new();
}
