using Microsoft.Extensions.DependencyInjection;

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
