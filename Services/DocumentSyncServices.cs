using FlowNoteMauiApp.Services;

namespace FlowNoteMauiApp.Services;

public class DocumentService : IDocumentService
{
    private string? _currentDocumentPath;
    private byte[]? _documentData;

    public Task<bool> LoadDocumentAsync(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                _documentData = File.ReadAllBytes(path);
                _currentDocumentPath = path;
                return Task.FromResult(true);
            }
        }
        catch
        {
        }
        return Task.FromResult(false);
    }

    public Task<bool> SaveDocumentAsync(string path)
    {
        try
        {
            if (_documentData != null)
            {
                File.WriteAllBytes(path, _documentData);
                _currentDocumentPath = path;
                return Task.FromResult(true);
            }
        }
        catch
        {
        }
        return Task.FromResult(false);
    }

    public Task<byte[]?> ExportAsync()
    {
        return Task.FromResult(_documentData);
    }

    public string? CurrentDocumentPath => _currentDocumentPath;
}

public class SyncService : ISyncService
{
    public event EventHandler<SyncEventArgs>? SyncStatusChanged;

    public Task<bool> SyncAsync()
    {
        OnStatusChanged(SyncStatus.Syncing, "Syncing...", 0);
        Task.Delay(1000).Wait();
        OnStatusChanged(SyncStatus.Success, "Sync complete", 100);
        return Task.FromResult(true);
    }

    public Task<bool> PullAsync()
    {
        OnStatusChanged(SyncStatus.Syncing, "Pulling...", 0);
        Task.Delay(1000).Wait();
        OnStatusChanged(SyncStatus.Success, "Pull complete", 100);
        return Task.FromResult(true);
    }

    public Task<bool> PushAsync()
    {
        OnStatusChanged(SyncStatus.Syncing, "Pushing...", 0);
        Task.Delay(1000).Wait();
        OnStatusChanged(SyncStatus.Success, "Push complete", 100);
        return Task.FromResult(true);
    }

    private void OnStatusChanged(SyncStatus status, string message, double progress)
    {
        SyncStatusChanged?.Invoke(this, new SyncEventArgs
        {
            Status = status,
            Message = message,
            Progress = progress
        });
    }
}
