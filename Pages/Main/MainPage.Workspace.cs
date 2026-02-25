using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Models;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private sealed class EditorTabInfo
    {
        public required string NoteId { get; init; }
        public required string Name { get; init; }
        public required string FolderPath { get; init; }
    }

    private readonly List<EditorTabInfo> _editorTabs = new();
    private const int MaxEditorTabCount = 12;

    private void UpsertEditorTab(WorkspaceNote note)
    {
        var existingIndex = _editorTabs.FindIndex(t => string.Equals(t.NoteId, note.Id, StringComparison.Ordinal));
        var tab = new EditorTabInfo
        {
            NoteId = note.Id,
            Name = note.Name,
            FolderPath = note.FolderPath
        };

        if (existingIndex >= 0)
        {
            _editorTabs[existingIndex] = tab;
            return;
        }

        _editorTabs.Add(tab);
        if (_editorTabs.Count > MaxEditorTabCount)
        {
            _editorTabs.RemoveAt(0);
        }
    }

    private string NormalizeEditorTabTitle(string title)
    {
        var text = string.IsNullOrWhiteSpace(title) ? "未命名文档" : title.Trim();
        return text.Length <= 20 ? text : text[..20] + "...";
    }

    private void RefreshEditorTabsVisual()
    {
        EditorTabsHost.Children.Clear();
        if (_editorTabs.Count == 0)
        {
            return;
        }

        foreach (var tab in _editorTabs)
        {
            var isActive = string.Equals(tab.NoteId, _currentNoteId, StringComparison.Ordinal);

            var tabBorder = new Border
            {
                Padding = new Thickness(10, 6),
                Margin = new Thickness(0, 0, 2, 0),
                BackgroundColor = isActive
                    ? (IsDarkTheme ? Color.FromArgb("#2D4361") : Color.FromArgb("#EEF4FF"))
                    : (IsDarkTheme ? Color.FromArgb("#1D2B3E") : Color.FromArgb("#F4F6FA")),
                Stroke = isActive
                    ? Color.FromArgb("#4A90E2")
                    : (IsDarkTheme ? Color.FromArgb("#4C6484") : Color.FromArgb("#D7DEE9")),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                MinimumWidthRequest = 180
            };

            var tabGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };

            tabGrid.Children.Add(new Image
            {
                Source = "icon_file.svg",
                WidthRequest = 13,
                HeightRequest = 13,
                VerticalOptions = LayoutOptions.Center,
                Opacity = 0.86
            });

            var titleLabel = new Label
            {
                Text = NormalizeEditorTabTitle(tab.Name),
                VerticalOptions = LayoutOptions.Center,
                FontFamily = "OpenSansSemibold",
                FontSize = 13,
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation,
                TextColor = ThemePrimaryText
            };
            tabGrid.Children.Add(titleLabel);
            Grid.SetColumn(titleLabel, 1);

            var closeButton = new ImageButton
            {
                Source = "icon_x.svg",
                WidthRequest = 20,
                HeightRequest = 20,
                Padding = 4,
                CornerRadius = 10,
                BackgroundColor = Colors.Transparent,
                BorderWidth = 0,
                CommandParameter = tab.NoteId
            };
            closeButton.Clicked += OnEditorTabCloseClicked;
            tabGrid.Children.Add(closeButton);
            Grid.SetColumn(closeButton, 2);

            tabBorder.Content = tabGrid;
            var openTab = new TapGestureRecognizer();
            openTab.Tapped += async (_, _) => await ActivateEditorTabAsync(tab.NoteId);
            tabBorder.GestureRecognizers.Add(openTab);

            EditorTabsHost.Children.Add(tabBorder);
        }
    }

    private async Task ActivateEditorTabAsync(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return;

        if (string.Equals(_currentNoteId, noteId, StringComparison.Ordinal))
        {
            RefreshEditorTabsVisual();
            return;
        }

        var note = _cachedHomeNotes.FirstOrDefault(n => string.Equals(n.Id, noteId, StringComparison.Ordinal))
            ?? await _workspaceService.GetNoteAsync(noteId);
        if (note is null)
        {
            _editorTabs.RemoveAll(t => string.Equals(t.NoteId, noteId, StringComparison.Ordinal));
            RefreshEditorTabsVisual();
            ShowStatus("标签页文档不存在，已自动移除");
            return;
        }

        await OpenWorkspaceNoteAsync(note);
    }

    private async void OnEditorTabCloseClicked(object? sender, EventArgs e)
    {
        if (sender is not ImageButton button || button.CommandParameter is not string noteId)
            return;

        await CloseEditorTabAsync(noteId);
    }

    private async Task CloseEditorTabAsync(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return;

        var removeIndex = _editorTabs.FindIndex(t => string.Equals(t.NoteId, noteId, StringComparison.Ordinal));
        if (removeIndex < 0)
            return;

        var wasActive = string.Equals(_currentNoteId, noteId, StringComparison.Ordinal);
        _editorTabs.RemoveAt(removeIndex);

        if (!wasActive)
        {
            RefreshEditorTabsVisual();
            return;
        }

        if (_editorTabs.Count == 0)
        {
            _currentNoteId = null;
            await RefreshWorkspaceViewsAsync();
            ShowHomeScreen();
            return;
        }

        var nextIndex = Math.Clamp(removeIndex - 1, 0, _editorTabs.Count - 1);
        var nextTab = _editorTabs[nextIndex];
        var nextNote = await _workspaceService.GetNoteAsync(nextTab.NoteId);
        if (nextNote is null)
        {
            _editorTabs.RemoveAt(nextIndex);
            RefreshEditorTabsVisual();
            if (_editorTabs.Count == 0)
            {
                _currentNoteId = null;
                await RefreshWorkspaceViewsAsync();
                ShowHomeScreen();
            }
            return;
        }

        await OpenWorkspaceNoteAsync(nextNote);
    }

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
        UpsertEditorTab(note);
        WorkspaceFolderEntry.Text = _workspaceFolder;
        await _workspaceService.MarkOpenedAsync(note.Id);

        EnsureEditorInitialized();
        PdfViewer.Source = new BytesPdfSource(bytes);
        ShowEditorScreen();
        RefreshEditorTabsVisual();
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
        InputModePanel.IsVisible = false;
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
        ApplyInputMode(
            _drawingInputMode == DrawingInputMode.TapRead ? DrawingInputMode.PenStylus : _drawingInputMode,
            activateDrawing: true);
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

    private Color HomePreviewBackground => IsDarkTheme ? Color.FromArgb("#2A3B55") : Color.FromArgb("#E7EEF7");
    private Color HomePreviewStroke => IsDarkTheme ? Color.FromArgb("#4B6281") : Color.FromArgb("#D3DCE9");
    private Color HomePreviewPaperBackground => IsDarkTheme ? Color.FromArgb("#374D69") : Color.FromArgb("#FFFFFF");

    private void RenderHomeEmptyState(string title, string description)
    {
        HomeNotesList.Children.Add(new Border
        {
            WidthRequest = 280,
            Margin = new Thickness(0, 8, 24, 18),
            BackgroundColor = IsDarkTheme ? Color.FromArgb("#223349") : Color.FromArgb("#FFFFFF"),
            Stroke = HomePreviewStroke,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(16, 14),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontFamily = "OpenSansSemibold",
                        FontSize = 16,
                        TextColor = ThemePrimaryText
                    },
                    new Label
                    {
                        Text = description,
                        FontFamily = "OpenSansRegular",
                        FontSize = 12,
                        TextColor = ThemeSecondaryText
                    },
                    new Button
                    {
                        Text = "导入 PDF",
                        FontSize = 13,
                        Padding = new Thickness(12, 8),
                        CornerRadius = 8,
                        HorizontalOptions = LayoutOptions.Start,
                        BackgroundColor = Color.FromArgb("#4A90E2"),
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
            WidthRequest = 130,
            Margin = new Thickness(0, 0, 22, 20),
            BackgroundColor = Colors.Transparent,
            StrokeThickness = 0,
            Padding = new Thickness(0)
        };

        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(138) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 6
        };

        var preview = new Border
        {
            WidthRequest = 94,
            HeightRequest = 138,
            HorizontalOptions = LayoutOptions.Center,
            BackgroundColor = HomePreviewBackground,
            Stroke = HomePreviewStroke,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 7 },
            Padding = new Thickness(8)
        };
        preview.Content = new Border
        {
            BackgroundColor = HomePreviewPaperBackground,
            Stroke = IsDarkTheme ? Color.FromArgb("#5D779A") : Color.FromArgb("#D8E1EE"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 7 },
            Content = new Image
            {
                Source = "icon_folder.svg",
                WidthRequest = 40,
                HeightRequest = 40,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Opacity = 0.85
            }
        };
        content.Add(preview);
        Grid.SetRow(preview, 0);

        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        titleRow.Children.Add(new Label
        {
            Text = folderName,
            FontFamily = "OpenSansSemibold",
            FontSize = 12,
            MaxLines = 2,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = ThemePrimaryText
        });
        var moreLabel = new Label
        {
            Text = "⋮",
            FontSize = 15,
            TextColor = ThemeSecondaryText
        };
        titleRow.Children.Add(moreLabel);
        Grid.SetColumn(moreLabel, 1);
        content.Add(titleRow);
        Grid.SetRow(titleRow, 1);

        var metaGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        metaGrid.Children.Add(new Label
        {
            Text = "文件夹",
            FontSize = 10,
            TextColor = ThemeSecondaryText
        });
        var dateLabel = new Label
        {
            Text = FormatDateForHome(DateTime.Now),
            HorizontalTextAlignment = TextAlignment.End,
            FontSize = 10,
            TextColor = ThemeSecondaryText
        };
        metaGrid.Children.Add(dateLabel);
        Grid.SetColumn(dateLabel, 1);
        content.Add(metaGrid);
        Grid.SetRow(metaGrid, 2);

        card.Content = content;
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
            WidthRequest = 130,
            Margin = new Thickness(0, 0, 22, 20),
            BackgroundColor = Colors.Transparent,
            StrokeThickness = 0,
            Padding = new Thickness(0)
        };

        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(138) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 6
        };

        var preview = new Border
        {
            WidthRequest = 94,
            HeightRequest = 138,
            HorizontalOptions = LayoutOptions.Center,
            BackgroundColor = HomePreviewBackground,
            Stroke = selected ? Color.FromArgb("#4A90E2") : HomePreviewStroke,
            StrokeThickness = selected ? 1.5 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 7 },
            Padding = new Thickness(8)
        };

        preview.Content = new Border
        {
            BackgroundColor = HomePreviewPaperBackground,
            Stroke = IsDarkTheme ? Color.FromArgb("#5D779A") : Color.FromArgb("#D8E1EE"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 7 },
            Content = new Image
            {
                Source = "icon_file.svg",
                WidthRequest = 38,
                HeightRequest = 38,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Opacity = 0.76
            }
        };
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
            FontSize = 12,
            MaxLines = 2,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = ThemePrimaryText
        });
        var chevron = new Label
        {
            Text = "⋮",
            FontSize = 15,
            TextColor = ThemeSecondaryText,
            VerticalOptions = LayoutOptions.Start
        };
        titleGrid.Children.Add(chevron);
        Grid.SetColumn(chevron, 1);
        content.Add(titleGrid);
        Grid.SetRow(titleGrid, 1);

        var metaGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };
        metaGrid.Children.Add(new Label
        {
            Text = $"{EstimatePages(note)}页",
            FontSize = 10,
            TextColor = ThemeSecondaryText
        });
        var dateLabel = new Label
        {
            Text = FormatDateForHome(note.LastOpenedAtUtc),
            HorizontalTextAlignment = TextAlignment.End,
            FontSize = 10,
            TextColor = ThemeSecondaryText
        };
        metaGrid.Children.Add(dateLabel);
        Grid.SetColumn(dateLabel, 1);
        content.Add(metaGrid);
        Grid.SetRow(metaGrid, 2);

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
                $"{note.Name} · {FormatDateTimeForRecent(note.LastOpenedAtUtc)}",
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
