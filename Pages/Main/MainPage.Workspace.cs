using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Models;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private async Task OpenWorkspaceNoteAsync(WorkspaceNote note)
    {
        await SaveCurrentDrawingStateAsync();
        var bytes = await _workspaceService.GetPdfBytesAsync(note.Id);
        if (bytes is null || bytes.Length == 0)
        {
            ShowStatus("Cannot open note content.");
            return;
        }

        _currentNoteId = note.Id;
        _workspaceFolder = note.FolderPath;
        WorkspaceFolderEntry.Text = _workspaceFolder;
        await _workspaceService.MarkOpenedAsync(note.Id);

        EnsureEditorInitialized();
        PdfViewer.Source = new BytesPdfSource(bytes);
        ShowEditorScreen();
        await LoadDrawingForCurrentNoteAsync();
        await RefreshWorkspaceViewsAsync();
        ShowStatus($"Opened: {note.Name}");
    }

    private async Task RefreshWorkspaceViewsAsync()
    {
        WorkspaceFolderEntry.Text = _workspaceFolder;
        var homeNotes = await _workspaceService.GetRecentNotesAsync(30);
        var recent = homeNotes.Take(6).ToList();
        var browse = await _workspaceService.BrowseAsync(_workspaceFolder);

        RenderRecentNotes(recent);
        RenderFolderItems(browse);
        RenderNoteItems(browse);
        RenderHomeNotes(homeNotes);
    }

    private void ShowHomeScreen()
    {
        HomePanel.IsVisible = true;
        TopBarPanel.IsVisible = false;
        SettingsPanel.IsVisible = false;
        DrawingToolbarPanel.IsVisible = false;
        LayerPanel.IsVisible = false;

        if (IsEditorInitialized)
        {
            PdfViewer.IsVisible = false;
            DrawingCanvas.EnableDrawing = false;
            DrawingCanvas.IsVisible = false;
        }
    }

    private void ShowEditorScreen()
    {
        EnsureEditorInitialized();
        HomePanel.IsVisible = false;
        TopBarPanel.IsVisible = true;
        PdfViewer.IsVisible = true;
        SettingsPanel.IsVisible = false;
    }

    private void RenderHomeNotes(IReadOnlyList<WorkspaceNote> notes)
    {
        HomeNotesList.Children.Clear();
        if (notes.Count == 0)
        {
            HomeNotesList.Children.Add(new Label
            {
                Text = "No imported PDF yet. Click 'Import PDF' to add one.",
                FontSize = 12,
                TextColor = ThemeSecondaryText
            });
            return;
        }

        foreach (var note in notes)
        {
            HomeNotesList.Children.Add(CreateWorkspaceButton(
                $"{note.Name} · {note.LastOpenedAtUtc.ToLocalTime():MM-dd HH:mm}",
                async () => await OpenWorkspaceNoteAsync(note),
                note.Id == _currentNoteId));
        }
    }

    private void RenderRecentNotes(IReadOnlyList<WorkspaceNote> recent)
    {
        RecentNotesList.Children.Clear();
        if (recent.Count == 0)
        {
            RecentNotesList.Children.Add(new Label { Text = "No recent notes", FontSize = 11, TextColor = ThemeSecondaryText });
            return;
        }

        foreach (var note in recent)
        {
            RecentNotesList.Children.Add(CreateWorkspaceButton(
                $"{note.Name} · {note.LastOpenedAtUtc.ToLocalTime():MM-dd HH:mm}",
                async () => await OpenWorkspaceNoteAsync(note),
                note.Id == _currentNoteId));
        }
    }

    private void RenderFolderItems(WorkspaceBrowseResult browse)
    {
        WorkspaceFolderList.Children.Clear();
        foreach (var folder in browse.SubFolders)
        {
            var folderName = folder.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? folder;
            WorkspaceFolderList.Children.Add(CreateWorkspaceButton(
                $"[Folder] {folderName}",
                async () =>
                {
                    _workspaceFolder = folder;
                    await RefreshWorkspaceViewsAsync();
                },
                false));
        }

        if (browse.SubFolders.Count == 0)
        {
            WorkspaceFolderList.Children.Add(new Label { Text = "No subfolders", FontSize = 11, TextColor = ThemeSecondaryText });
        }
    }

    private void RenderNoteItems(WorkspaceBrowseResult browse)
    {
        WorkspaceNoteList.Children.Clear();
        foreach (var note in browse.Notes)
        {
            WorkspaceNoteList.Children.Add(CreateWorkspaceButton(
                note.Name,
                async () => await OpenWorkspaceNoteAsync(note),
                note.Id == _currentNoteId));
        }

        if (browse.Notes.Count == 0)
        {
            WorkspaceNoteList.Children.Add(new Label { Text = "No notes in this folder", FontSize = 11, TextColor = ThemeSecondaryText });
        }
    }

    private Button CreateWorkspaceButton(string text, Func<Task> onClick, bool isSelected)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(10, 6),
            BackgroundColor = isSelected ? ThemeSelectedBackground : ThemeListBackground,
            TextColor = ThemePrimaryText
        };

        button.Clicked += async (_, _) =>
        {
            try
            {
                await onClick();
            }
            catch (Exception ex)
            {
                ShowStatus(ex.Message);
            }
        };
        return button;
    }

    private async void OnWorkspaceRootClicked(object? sender, EventArgs e)
    {
        _workspaceFolder = "/";
        await RefreshWorkspaceViewsAsync();
    }

    private async void OnWorkspaceUpClicked(object? sender, EventArgs e)
    {
        _workspaceFolder = ParentFolderOf(_workspaceFolder);
        await RefreshWorkspaceViewsAsync();
    }

    private async void OnWorkspaceOpenFolderClicked(object? sender, EventArgs e)
    {
        _workspaceFolder = NormalizeFolderPath(WorkspaceFolderEntry.Text);
        await RefreshWorkspaceViewsAsync();
    }

    private async void OnWorkspaceCreateFolderClicked(object? sender, EventArgs e)
    {
        var name = WorkspaceNewFolderEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowStatus("Enter a folder name first.");
            return;
        }

        var created = await _workspaceService.CreateFolderAsync(_workspaceFolder, name);
        if (!created)
        {
            ShowStatus("Invalid folder name.");
            return;
        }

        WorkspaceNewFolderEntry.Text = string.Empty;
        await RefreshWorkspaceViewsAsync();
    }

    private async void OnWorkspaceRefreshClicked(object? sender, EventArgs e)
    {
        await RefreshWorkspaceViewsAsync();
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

    private static string ParentFolderOf(string path)
    {
        var normalized = NormalizeFolderPath(path);
        if (normalized == "/")
            return "/";

        var separatorIndex = normalized.LastIndexOf('/');
        if (separatorIndex <= 0)
            return "/";
        return normalized[..separatorIndex];
    }
}
