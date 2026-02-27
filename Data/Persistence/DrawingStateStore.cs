using System.IO.Compression;
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
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
        var payload = CompressBytes(jsonBytes);
        await File.WriteAllBytesAsync(path, payload);
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
            var payload = await File.ReadAllBytesAsync(path);
            if (payload.Length == 0)
                return null;

            var jsonBytes = IsGzip(payload)
                ? DecompressBytes(payload)
                : payload;
            if (jsonBytes.Length == 0)
                return null;

            return JsonSerializer.Deserialize<DrawingDocumentState>(jsonBytes, JsonOptions);
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

    private static bool IsGzip(byte[] payload)
    {
        return payload.Length >= 2 && payload[0] == 0x1F && payload[1] == 0x8B;
    }

    private static byte[] CompressBytes(byte[] input)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(input, 0, input.Length);
        }

        return output.ToArray();
    }

    private static byte[] DecompressBytes(byte[] input)
    {
        using var inputStream = new MemoryStream(input);
        using var gzip = new GZipStream(inputStream, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
