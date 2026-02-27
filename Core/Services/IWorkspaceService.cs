using FlowNoteMauiApp.Models;

namespace FlowNoteMauiApp.Core.Services;

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
