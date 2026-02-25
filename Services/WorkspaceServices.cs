using System.Text.Json;
using FlowNoteMauiApp.Models;

namespace FlowNoteMauiApp.Services;

public sealed class WorkspaceService : IWorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _workspaceRoot;
    private readonly string _notesRoot;
    private readonly string _indexPath;

    public WorkspaceService()
    {
        _workspaceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlowNote",
            "workspace");
        _notesRoot = Path.Combine(_workspaceRoot, "notes");
        _indexPath = Path.Combine(_workspaceRoot, "index.json");
        Directory.CreateDirectory(_workspaceRoot);
        Directory.CreateDirectory(_notesRoot);
    }

    public async Task<IReadOnlyList<WorkspaceNote>> GetRecentNotesAsync(int limit = 20)
    {
        await _gate.WaitAsync();
        try
        {
            var index = await LoadIndexLockedAsync();
            return index.Notes
                .OrderByDescending(n => n.LastOpenedAtUtc)
                .ThenByDescending(n => n.ModifiedAtUtc)
                .Take(Math.Max(1, limit))
                .Select(CloneNote)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorkspaceBrowseResult> BrowseAsync(string? folderPath)
    {
        await _gate.WaitAsync();
        try
        {
            var index = await LoadIndexLockedAsync();
            var folder = NormalizeFolderPath(folderPath);
            EnsureFolderExistsLocked(index, folder);

            var folders = GetDirectChildFolders(index.Folders, folder)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var notes = index.Notes
                .Where(n => string.Equals(n.FolderPath, folder, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.ModifiedAtUtc)
                .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
                .Select(CloneNote)
                .ToList();

            return new WorkspaceBrowseResult
            {
                FolderPath = folder,
                SubFolders = folders,
                Notes = notes
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> CreateFolderAsync(string? parentFolderPath, string folderName)
    {
        var segment = SanitizeFolderSegment(folderName);
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        await _gate.WaitAsync();
        try
        {
            var index = await LoadIndexLockedAsync();
            var parent = NormalizeFolderPath(parentFolderPath);
            var path = parent == "/"
                ? "/" + segment
                : parent + "/" + segment;
            EnsureFolderExistsLocked(index, path);
            await SaveIndexLockedAsync(index);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorkspaceNote> ImportPdfAsync(string sourceName, byte[] pdfBytes, string? folderPath = null)
    {
        if (pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes are empty.", nameof(pdfBytes));

        await _gate.WaitAsync();
        try
        {
            var index = await LoadIndexLockedAsync();
            var folder = NormalizeFolderPath(folderPath);
            EnsureFolderExistsLocked(index, folder);

            var baseName = SanitizeNoteName(Path.GetFileNameWithoutExtension(sourceName));
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "Untitled";
            var uniqueName = BuildUniqueNoteName(index.Notes, folder, baseName);

            var now = DateTime.UtcNow;
            var note = new WorkspaceNote
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = uniqueName,
                FolderPath = folder,
                CreatedAtUtc = now,
                ModifiedAtUtc = now,
                LastOpenedAtUtc = now
            };
            note.RelativePdfPath = Path.Combine("notes", $"{note.Id}.pdf").Replace('\\', '/');

            var absolutePdfPath = GetAbsolutePath(note.RelativePdfPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePdfPath)!);
            await File.WriteAllBytesAsync(absolutePdfPath, pdfBytes);

            index.Notes.Add(note);
            await SaveIndexLockedAsync(index);
            return CloneNote(note);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<byte[]?> GetPdfBytesAsync(string noteId)
    {
        await _gate.WaitAsync();
        try
        {
            var index = await LoadIndexLockedAsync();
            var note = index.Notes.FirstOrDefault(n => n.Id == noteId);
            if (note is null)
                return null;

            var absolutePath = GetAbsolutePath(note.RelativePdfPath);
            if (!File.Exists(absolutePath))
                return null;

            return await File.ReadAllBytesAsync(absolutePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<WorkspaceNote?> GetNoteAsync(string noteId)
    {
        await _gate.WaitAsync();
        try
        {
            var index = await LoadIndexLockedAsync();
            var note = index.Notes.FirstOrDefault(n => n.Id == noteId);
            return note is null ? null : CloneNote(note);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkOpenedAsync(string noteId)
    {
        await _gate.WaitAsync();
        try
        {
            var index = await LoadIndexLockedAsync();
            var note = index.Notes.FirstOrDefault(n => n.Id == noteId);
            if (note is null)
                return;

            note.LastOpenedAtUtc = DateTime.UtcNow;
            await SaveIndexLockedAsync(index);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string GetAbsolutePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_workspaceRoot, normalized);
    }

    private static WorkspaceNote CloneNote(WorkspaceNote note)
    {
        return new WorkspaceNote
        {
            Id = note.Id,
            Name = note.Name,
            FolderPath = note.FolderPath,
            RelativePdfPath = note.RelativePdfPath,
            CreatedAtUtc = note.CreatedAtUtc,
            ModifiedAtUtc = note.ModifiedAtUtc,
            LastOpenedAtUtc = note.LastOpenedAtUtc
        };
    }

    private static IEnumerable<string> GetDirectChildFolders(IEnumerable<string> folders, string parentFolder)
    {
        var prefix = parentFolder == "/" ? "/" : parentFolder + "/";
        var children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            if (string.Equals(folder, parentFolder, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!folder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var remainder = folder[prefix.Length..];
            if (string.IsNullOrWhiteSpace(remainder))
                continue;

            var nextSegment = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(nextSegment))
                continue;

            var childPath = parentFolder == "/"
                ? "/" + nextSegment
                : parentFolder + "/" + nextSegment;
            children.Add(childPath);
        }

        return children;
    }

    private static string BuildUniqueNoteName(IReadOnlyCollection<WorkspaceNote> notes, string folderPath, string baseName)
    {
        var existing = new HashSet<string>(
            notes.Where(n => string.Equals(n.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase))
                 .Select(n => n.Name),
            StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(baseName))
            return baseName;

        var suffix = 2;
        while (existing.Contains($"{baseName} ({suffix})"))
        {
            suffix++;
        }
        return $"{baseName} ({suffix})";
    }

    private static string NormalizeFolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        var normalized = path.Replace('\\', '/').Trim();
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (normalized.Length > 1 && normalized.EndsWith('/'))
            normalized = normalized[..^1];

        return normalized;
    }

    private static string SanitizeFolderSegment(string input)
    {
        var segment = input.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            segment = segment.Replace(invalid.ToString(), string.Empty, StringComparison.Ordinal);
        }
        return segment.Trim().Trim('.');
    }

    private static string SanitizeNoteName(string input)
    {
        var name = input.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid.ToString(), string.Empty, StringComparison.Ordinal);
        }
        return name.Trim();
    }

    private static void EnsureFolderExistsLocked(WorkspaceIndex index, string folderPath)
    {
        var normalized = NormalizeFolderPath(folderPath);
        if (normalized == "/")
        {
            if (!index.Folders.Contains("/"))
                index.Folders.Insert(0, "/");
            return;
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "/";
        if (!index.Folders.Contains("/"))
            index.Folders.Insert(0, "/");

        foreach (var part in parts)
        {
            current = current == "/" ? "/" + part : current + "/" + part;
            if (!index.Folders.Any(f => string.Equals(f, current, StringComparison.OrdinalIgnoreCase)))
            {
                index.Folders.Add(current);
            }
        }
    }

    private async Task<WorkspaceIndex> LoadIndexLockedAsync()
    {
        if (!File.Exists(_indexPath))
        {
            return new WorkspaceIndex();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_indexPath);
            var index = JsonSerializer.Deserialize<WorkspaceIndex>(json, JsonOptions) ?? new WorkspaceIndex();
            if (!index.Folders.Any(f => string.Equals(f, "/", StringComparison.OrdinalIgnoreCase)))
                index.Folders.Insert(0, "/");
            index.Folders = index.Folders
                .Select(NormalizeFolderPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f.Count(c => c == '/'))
                .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return index;
        }
        catch
        {
            return new WorkspaceIndex();
        }
    }

    private Task SaveIndexLockedAsync(WorkspaceIndex index)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_indexPath)!);
        var json = JsonSerializer.Serialize(index, JsonOptions);
        return File.WriteAllTextAsync(_indexPath, json);
    }
}
