using System.Text.Json;
using FlowNoteMauiApp.Models;
using FlowNoteMauiApp.Core.Services;

namespace FlowNoteMauiApp.Data.Persistence;

public sealed class DrawingStateStore : IDrawingPersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _inkDirectory;

    public DrawingStateStore()
    {
        _inkDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlowNote",
            "workspace",
            "ink");
        Directory.CreateDirectory(_inkDirectory);
    }

    public async Task SaveAsync(string noteId, DrawingDocumentState state)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return;

        Directory.CreateDirectory(_inkDirectory);
        var path = GetInkPath(noteId);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<DrawingDocumentState?> LoadAsync(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return null;

        var path = GetInkPath(noteId);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<DrawingDocumentState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private string GetInkPath(string noteId)
    {
        var safeId = noteId.Trim().Replace('/', '_').Replace('\\', '_');
        return Path.Combine(_inkDirectory, $"{safeId}.json");
    }
}
