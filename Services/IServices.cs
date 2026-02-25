using FlowNoteMauiApp.Models;

namespace FlowNoteMauiApp.Services;

public interface IDocumentService
{
    Task<bool> LoadDocumentAsync(string path);
    Task<bool> SaveDocumentAsync(string path);
    Task<byte[]?> ExportAsync();
}

public interface ISyncService
{
    Task<bool> SyncAsync();
    Task<bool> PullAsync();
    Task<bool> PushAsync();
    event EventHandler<SyncEventArgs>? SyncStatusChanged;
}

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceNote>> GetRecentNotesAsync(int limit = 20);
    Task<WorkspaceBrowseResult> BrowseAsync(string? folderPath);
    Task<bool> CreateFolderAsync(string? parentFolderPath, string folderName);
    Task<WorkspaceNote> ImportPdfAsync(string sourceName, byte[] pdfBytes, string? folderPath = null);
    Task<byte[]?> GetPdfBytesAsync(string noteId);
    Task<WorkspaceNote?> GetNoteAsync(string noteId);
    Task MarkOpenedAsync(string noteId);
}

public interface IDrawingPersistenceService
{
    Task SaveAsync(string noteId, DrawingDocumentState state);
    Task<DrawingDocumentState?> LoadAsync(string noteId);
}

public class SyncEventArgs : EventArgs
{
    public SyncStatus Status { get; set; }
    public string? Message { get; set; }
    public double Progress { get; set; }
}

public enum SyncStatus
{
    Idle,
    Syncing,
    Success,
    Error,
    Conflict
}
