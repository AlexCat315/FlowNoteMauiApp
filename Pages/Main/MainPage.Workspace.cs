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
        EditorDocumentTitleLabel.Text = note.Name;
        PdfViewer.Source = new BytesPdfSource(bytes);
        ShowEditorScreen();
        await LoadDrawingForCurrentNoteAsync();
        await RefreshWorkspaceViewsAsync();
        ShowStatus($"Opened: {note.Name}");
    }

    private async Task RefreshWorkspaceViewsAsync()
    {
        WorkspaceFolderEntry.Text = _workspaceFolder;
        var homeNotes = await _workspaceService.GetRecentNotesAsync(120);
        var recent = homeNotes.Take(6).ToList();
        var browse = await _workspaceService.BrowseAsync(_workspaceFolder);

        RenderRecentNotes(recent);
        RenderFolderItems(browse);
        RenderNoteItems(browse);

        _cachedHomeNotes = homeNotes;
        _cachedHomeFolders = browse.SubFolders;
        RefreshHomeFeed();
    }

    private void ShowHomeScreen()
    {
        HomePanel.IsVisible = true;
        TopBarPanel.IsVisible = false;
        SetDrawerVisible(false);
        SetSettingsVisible(false);
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
        SetDrawerVisible(false);
        SetSettingsVisible(false);
        PdfViewer.IsVisible = true;
    }

    private void RenderHomeNotes(IReadOnlyList<WorkspaceNote> notes)
    {
        HomeNotesList.Children.Clear();
        HomeCountLabel.Text = $"{notes.Count} 个文档";

        if (notes.Count == 0)
        {
            RenderHomeEmptyState("还没有导入文档", "点击右下角 + 导入 PDF");
            return;
        }

        foreach (var note in notes)
        {
            HomeNotesList.Children.Add(CreateHomeNoteCard(note));
        }
    }

    private void RenderHomeFolders(IReadOnlyList<string> folders)
    {
        HomeNotesList.Children.Clear();
        HomeCountLabel.Text = $"{folders.Count} 个文件夹";

        if (folders.Count == 0)
        {
            RenderHomeEmptyState("当前目录没有子文件夹", "使用设置中的工作区管理创建新文件夹");
            return;
        }

        foreach (var folderPath in folders)
        {
            HomeNotesList.Children.Add(CreateHomeFolderCard(folderPath));
        }
    }

    private void RenderHomeEmptyState(string title, string description)
    {
        HomeNotesList.Children.Add(new Border
        {
            WidthRequest = 360,
            Margin = new Thickness(0, 0, 12, 12),
            BackgroundColor = IsDarkTheme ? Color.FromArgb("#1E2E45") : Color.FromArgb("#FFFFFF"),
            Stroke = IsDarkTheme ? Color.FromArgb("#445C7D") : Color.FromArgb("#E3EAF5"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontFamily = "OpenSansSemibold",
                        FontSize = 18,
                        TextColor = ThemePrimaryText
                    },
                    new Label
                    {
                        Text = description,
                        FontFamily = "OpenSansRegular",
                        FontSize = 13,
                        TextColor = ThemeSecondaryText
                    },
                    new Button
                    {
                        Text = "导入 PDF",
                        FontSize = 14,
                        Padding = new Thickness(14, 8),
                        CornerRadius = 14,
                        BackgroundColor = Color.FromArgb("#1E63FF"),
                        TextColor = Colors.White,
                        Command = new Command(async () => await PickAndImportPdfAsync(openAfterImport: false))
                    }
                }
            }
        });
    }

    private View CreateHomeFolderCard(string folderPath)
    {
        var folderName = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "/";

        var card = new Border
        {
            WidthRequest = 246,
            Margin = new Thickness(0, 0, 12, 12),
            BackgroundColor = IsDarkTheme ? Color.FromArgb("#1E2E45") : Colors.White,
            Stroke = IsDarkTheme ? Color.FromArgb("#445C7D") : Color.FromArgb("#E3EAF5"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(14),
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(120) },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                },
                RowSpacing = 8,
                Children =
                {
                    new Border
                    {
                        BackgroundColor = IsDarkTheme ? Color.FromArgb("#2A3B56") : Color.FromArgb("#EEF5FF"),
                        Stroke = IsDarkTheme ? Color.FromArgb("#5B7090") : Color.FromArgb("#DCE8F8"),
                        StrokeThickness = 1,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                        Content = new Image
                        {
                            Source = "icon_folder.svg",
                            WidthRequest = 54,
                            HeightRequest = 54,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center,
                            Opacity = 0.82
                        }
                    },
                    new Label
                    {
                        Text = folderName,
                        FontFamily = "OpenSansSemibold",
                        FontSize = 18,
                        MaxLines = 1,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        TextColor = ThemePrimaryText
                    },
                    new Label
                    {
                        Text = folderPath,
                        FontFamily = "OpenSansRegular",
                        FontSize = 12,
                        MaxLines = 1,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        TextColor = ThemeSecondaryText
                    }
                }
            }
        };

        if (card.Content is Grid content)
        {
            content.SetRow(content.Children[0], 0);
            content.SetRow(content.Children[1], 1);
            content.SetRow(content.Children[2], 2);
        }

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                _workspaceFolder = folderPath;
                _homeFilter = HomeFilterType.All;
                UpdateHomeFilterButtons();
                await RefreshWorkspaceViewsAsync();
                ShowStatus($"已进入文件夹: {folderName}");
            })
        });

        return card;
    }

    private View CreateHomeNoteCard(WorkspaceNote note)
    {
        var selected = note.Id == _currentNoteId;
        var card = new Border
        {
            WidthRequest = 246,
            Margin = new Thickness(0, 0, 12, 12),
            BackgroundColor = IsDarkTheme ? Color.FromArgb("#1E2E45") : Colors.White,
            Stroke = selected
                ? Color.FromArgb("#1E63FF")
                : (IsDarkTheme ? Color.FromArgb("#445C7D") : Color.FromArgb("#E3EAF5")),
            StrokeThickness = selected ? 2 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(14)
        };

        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(148) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 8
        };

        var preview = new Border
        {
            BackgroundColor = IsDarkTheme ? Color.FromArgb("#2A3B56") : Color.FromArgb("#EEF5FF"),
            Stroke = IsDarkTheme ? Color.FromArgb("#5B7090") : Color.FromArgb("#DCE8F8"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(10)
        };

        var previewGrid = new Grid();
        previewGrid.Children.Add(new Border
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 160,
            HeightRequest = 118,
            BackgroundColor = IsDarkTheme ? Color.FromArgb("#314763") : Color.FromArgb("#FDFEFF"),
            Stroke = IsDarkTheme ? Color.FromArgb("#60779A") : Color.FromArgb("#DFEAF8"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = new Image
            {
                Source = "icon_file.svg",
                WidthRequest = 56,
                HeightRequest = 56,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Opacity = 0.68
            }
        });
        preview.Content = previewGrid;
        content.Add(preview);
        Grid.SetRow(preview, 0);

        var titleGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        titleGrid.Children.Add(new Label
        {
            Text = note.Name,
            FontFamily = "OpenSansSemibold",
            FontSize = 18,
            MaxLines = 2,
            LineBreakMode = LineBreakMode.WordWrap,
            TextColor = ThemePrimaryText
        });
        var chevron = new Label
        {
            Text = "›",
            FontSize = 28,
            Margin = new Thickness(10, -2, 0, 0),
            TextColor = ThemeSecondaryText,
            VerticalOptions = LayoutOptions.Start
        };
        titleGrid.Children.Add(chevron);
        Grid.SetColumn(chevron, 1);
        content.Add(titleGrid);
        Grid.SetRow(titleGrid, 1);

        var metaLabel = new Label
        {
            Text = $"{EstimatePages(note)}页 · PDF笔记 · {FormatRelativeTime(note.LastOpenedAtUtc)}",
            FontFamily = "OpenSansRegular",
            FontSize = 12,
            TextColor = ThemeSecondaryText
        };
        content.Add(metaLabel);
        Grid.SetRow(metaLabel, 2);

        card.Content = content;
        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                try
                {
                    await OpenWorkspaceNoteAsync(note);
                }
                catch (Exception ex)
                {
                    ShowStatus(ex.Message);
                }
            })
        });

        return card;
    }

    private static int EstimatePages(WorkspaceNote note)
    {
        var seed = StringComparer.Ordinal.GetHashCode(note.Id);
        if (seed == int.MinValue)
            seed = 0;
        seed = Math.Abs(seed);
        return 8 + (seed % 38);
    }

    private static string FormatRelativeTime(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta.TotalMinutes < 1)
            return "刚刚";
        if (delta.TotalHours < 1)
            return $"{Math.Max(1, (int)delta.TotalMinutes)}分钟前";
        if (delta.TotalDays < 1)
            return $"{Math.Max(1, (int)delta.TotalHours)}小时前";
        if (delta.TotalDays < 7)
            return $"{Math.Max(1, (int)delta.TotalDays)}天前";
        return utc.ToLocalTime().ToString("MM-dd");
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
