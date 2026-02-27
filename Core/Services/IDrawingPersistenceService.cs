using FlowNoteMauiApp.Models;

namespace FlowNoteMauiApp.Core.Services;

public interface IDrawingPersistenceService
{
    Task SaveAsync(string noteId, DrawingDocumentState state);
    Task<DrawingDocumentState?> LoadAsync(string noteId);
}
