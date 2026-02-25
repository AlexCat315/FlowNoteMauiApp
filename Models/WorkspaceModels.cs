namespace FlowNoteMauiApp.Models;

public sealed class WorkspaceNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled";
    public string FolderPath { get; set; } = "/";
    public string RelativePdfPath { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastOpenedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class WorkspaceBrowseResult
{
    public string FolderPath { get; init; } = "/";
    public IReadOnlyList<string> SubFolders { get; init; } = Array.Empty<string>();
    public IReadOnlyList<WorkspaceNote> Notes { get; init; } = Array.Empty<WorkspaceNote>();
}

public sealed class WorkspaceIndex
{
    public List<string> Folders { get; set; } = new() { "/" };
    public List<WorkspaceNote> Notes { get; set; } = new();
}
