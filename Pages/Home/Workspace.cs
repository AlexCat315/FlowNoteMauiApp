using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Models;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

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
    private const double EditorTopInsetFallback = 74d;
    private const double AndroidEditorTopInsetFallback = 86d;
    private const int HomeFeedRenderBatchSize = 12;
    private bool _isTopBarInsetWired;
    private CancellationTokenSource? _homeFeedRenderCts;
    private int _openWorkspaceNoteRequestVersion;
    private bool _isHomeSelectionMode;
    private readonly HashSet<string> _selectedHomeNoteIds = new(StringComparer.Ordinal);

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
        var text = string.IsNullOrWhiteSpace(title) ? T("UntitledDocument", "Untitled Document") : title.Trim();
        return text.Length <= 20 ? text : text[..20] + "...";
    }

    private void RefreshEditorTabsVisual()
    {
        EditorTabsHost.Children.Clear();
        _editorTabDropIndicators.Clear();
        _tabDropTargetNoteId = null;
        if (_editorTabs.Count == 0)
        {
            return;
        }

        foreach (var tab in _editorTabs)
        {
            var isActive = string.Equals(tab.NoteId, _currentNoteId, StringComparison.Ordinal);
            var palette = Palette;

            var tabBorder = new Border
            {
                Padding = new Thickness(9, 3),
                Margin = new Thickness(0, 2, 2, 0),
                BackgroundColor = isActive
                    ? palette.TabActiveBackground
                    : palette.TabInactiveBackground,
                Stroke = isActive
                    ? palette.TabActiveBorder
                    : palette.TabInactiveBorder,
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10, 10, 4, 4) },
                MinimumWidthRequest = 108,
                HeightRequest = 30
            };

            var tabGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 5
            };

            tabGrid.Children.Add(new Image
            {
                Source = "icon_file.png",
                WidthRequest = 11,
                HeightRequest = 11,
                VerticalOptions = LayoutOptions.Center,
                Opacity = isActive ? 0.96 : 0.72
            });

            var titleLabel = new Label
            {
                Text = NormalizeEditorTabTitle(tab.Name),
                VerticalOptions = LayoutOptions.Center,
                FontFamily = isActive ? "OpenSansSemibold" : "OpenSansRegular",
                FontSize = 11.5,
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation,
                TextColor = isActive ? palette.TabActiveText : palette.TabInactiveText
            };
            tabGrid.Children.Add(titleLabel);
            Grid.SetColumn(titleLabel, 1);

            var closeButton = new ImageButton
            {
                Source = isActive ? "icon_x_white.png" : "icon_x.png",
                WidthRequest = 10,
                HeightRequest = 10,
                MinimumWidthRequest = 12,
                MinimumHeightRequest = 12,
                Padding = 0,
                CornerRadius = 6,
                BackgroundColor = isActive ? Color.FromArgb("#38FFFFFF") : Color.FromArgb("#EEF3FA"),
                BorderColor = isActive ? Color.FromArgb("#77FFFFFF") : palette.TabInactiveBorder,
                BorderWidth = 1,
                Opacity = isActive ? 0.94 : 0.80,
                CommandParameter = tab.NoteId
            };
            RegisterMicroInteraction(closeButton);
            closeButton.Clicked += OnEditorTabCloseClicked;
            tabGrid.Children.Add(closeButton);
            Grid.SetColumn(closeButton, 2);

            var dropIndicator = new BoxView
            {
                WidthRequest = 2,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Fill,
                Color = palette.ModeButtonExpandedBorder,
                IsVisible = false
            };
            tabGrid.Children.Add(dropIndicator);
            Grid.SetColumnSpan(dropIndicator, 3);

            tabBorder.Content = tabGrid;
            var openTab = new TapGestureRecognizer();
            openTab.Tapped += async (_, _) => await ActivateEditorTabAsync(tab.NoteId);
            tabBorder.GestureRecognizers.Add(openTab);
            _editorTabDropIndicators[tab.NoteId] = dropIndicator;
            AttachEditorTabDragReorder(tabBorder, tab.NoteId);

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
            ShowStatus(T("StatusTabDocMissingRemoved", "Document in tab not found and removed."));
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
        EnsureUiBootstrapped();
        var requestVersion = Interlocked.Increment(ref _openWorkspaceNoteRequestVersion);
        await Task.Yield();

        await SaveCurrentDrawingStateAsync();
        if (requestVersion != _openWorkspaceNoteRequestVersion)
            return;

        var bytes = await _workspaceService.GetPdfBytesAsync(note.Id);
        if (requestVersion != _openWorkspaceNoteRequestVersion)
            return;

        if (bytes is null || bytes.Length == 0)
        {
            ShowStatus(T("OpenNoteContentFailed", "Cannot open note content."));
            return;
        }

        _currentNoteId = note.Id;
        _workspaceFolder = note.FolderPath;
        UpsertEditorTab(note);
        WorkspaceFolderEntry.Text = _workspaceFolder;
        await _workspaceService.MarkOpenedAsync(note.Id);
        if (requestVersion != _openWorkspaceNoteRequestVersion)
            return;

        EnsureEditorInitialized();
        InvalidateThumbnailCache();
        ResetPdfPageBoundsCache();
        ClearArmedInkTool(hideDrawingToolbar: true);
        ShowEditorScreen();
        RefreshEditorTabsVisual();

        await Task.Yield();
        if (requestVersion != _openWorkspaceNoteRequestVersion)
            return;

        PdfViewer.Source = new BytesPdfSource(bytes);
        await LoadDrawingForCurrentNoteAsync();
        if (requestVersion != _openWorkspaceNoteRequestVersion)
            return;

        ShowStatus(TF("OpenedNoteFormat", "Opened: {0}", note.Name));
    }

    private async Task RefreshWorkspaceViewsAsync()
    {
        var homeNotes = await _workspaceService.GetRecentNotesAsync(120);
        var trashedNotes = await _workspaceService.GetTrashedNotesAsync(200);
        var recent = homeNotes.Take(6).ToList();
        var browse = await _workspaceService.BrowseAsync(_workspaceFolder);

        if (_isUiBootstrapped)
        {
            WorkspaceFolderEntry.Text = _workspaceFolder;
            RenderRecentNotes(recent);
            RenderFolderItems(browse);
            RenderNoteItems(browse);
        }

        _cachedHomeNotes = homeNotes;
        _cachedTrashedNotes = trashedNotes;
        _cachedHomeFolders = browse.SubFolders;
        var visibleIds = (_isTrashView ? _cachedTrashedNotes : _cachedHomeNotes)
            .Select(n => n.Id)
            .ToHashSet(StringComparer.Ordinal);
        _selectedHomeNoteIds.RemoveWhere(id => !visibleIds.Contains(id));
        if (_selectedHomeNoteIds.Count == 0)
            _isHomeSelectionMode = false;
        RefreshHomeFeed();
    }

    private void ShowHomeScreen()
    {
        var noteIdForCoverRefresh = _currentNoteId;
        HomePanelView.IsVisible = true;
        HomePanelView.InputTransparent = false;
        AnimateScreenEntry(HomePanelView);
        EditorChromeView.IsVisible = false;
        EditorChromeView.InputTransparent = true;

        HomePanel.IsVisible = true;
        if (_isUiBootstrapped)
        {
            TopBarPanel.IsVisible = false;
            PinnedInkToolsOverlay.IsVisible = false;
            UpdateEditorViewportInset();
            SetDrawerVisible(false);
            SetSettingsVisible(false);
            DrawingToolbarPanel.IsVisible = false;
            InputModePanel.IsVisible = false;
            ThumbnailPanel.IsVisible = false;
            LayerPanel.IsVisible = false;
        }
        else
        {
            DrawerOverlayView.IsVisible = false;
            DrawerOverlayView.InputTransparent = true;
            SettingsOverlayView.IsVisible = false;
            SettingsOverlayView.InputTransparent = true;
            EditorHost.Margin = Thickness.Zero;
        }

        if (IsEditorInitialized)
        {
            PdfViewer.IsVisible = false;
            if (_isUiBootstrapped)
            {
                ClearArmedInkTool(hideDrawingToolbar: true);
            }
            DrawingCanvas.IsVisible = false;
        }

        if (!string.IsNullOrWhiteSpace(noteIdForCoverRefresh))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await SaveCurrentDrawingStateAsync().ConfigureAwait(false);
                    var note = await _workspaceService.GetNoteAsync(noteIdForCoverRefresh).ConfigureAwait(false);
                    if (note is null)
                        return;
                    var bytes = await _workspaceService.GetPdfBytesAsync(noteIdForCoverRefresh).ConfigureAwait(false);
                    if (bytes is null || bytes.Length == 0)
                        return;

                    InvalidateHomeCoverCacheForNote(noteIdForCoverRefresh);
                    QueuePrimeHomeCoverCache(note, bytes);
                    await MainThread.InvokeOnMainThreadAsync(RefreshHomeFeed);
                }
                catch
                {
                }
            });
        }
    }

    private void ShowEditorScreen()
    {
        EnsureUiBootstrapped();
        EnsureEditorInitialized();
        EnsureTopBarInsetHooked();
        HomePanelView.IsVisible = false;
        HomePanelView.InputTransparent = true;
        EditorChromeView.IsVisible = true;
        EditorChromeView.InputTransparent = false;
        AnimateScreenEntry(EditorChromeView);

        HomePanel.IsVisible = false;
        TopBarPanel.IsVisible = true;
        PositionPinnedInkToolsOverlay();
        UpdateEditorViewportInset();
        SetDrawerVisible(false);
        SetSettingsVisible(false);
        PdfViewer.IsVisible = true;
        ApplyInputMode(_drawingInputMode, activateDrawing: true);
    }

    private void EnsureTopBarInsetHooked()
    {
        if (_isTopBarInsetWired)
            return;

        TopBarPanel.SizeChanged += OnTopBarPanelSizeChanged;
        _isTopBarInsetWired = true;
    }

    private void OnTopBarPanelSizeChanged(object? sender, EventArgs e)
    {
        UpdateEditorViewportInset();
    }

    private void UpdateEditorViewportInset()
    {
        if (!EditorChromeView.IsVisible || !TopBarPanel.IsVisible)
        {
            if (DeviceInfo.Platform == DevicePlatform.MacCatalyst)
            {
                EditorChromeView.TranslationY = 0;
            }
            EditorHost.Margin = Thickness.Zero;
            return;
        }

        // Use the top bar's absolute offset inside the page so safe-area/title-bar space is counted.
        var chromeTopOffset = GetVisualOffsetY(TopBarPanel, this);
        if (double.IsNaN(chromeTopOffset) || chromeTopOffset < 0d)
        {
            chromeTopOffset = 0d;
        }

        if (DeviceInfo.Platform == DevicePlatform.MacCatalyst)
        {
            // Pull the custom chrome up into the native title-bar gap so the top background stays opaque.
            EditorChromeView.TranslationY = -chromeTopOffset;
            chromeTopOffset = 0d;
        }
        var topInset = chromeTopOffset + TopBarPanel.Height;
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            topInset = Math.Max(topInset, chromeTopOffset + AndroidEditorTopInsetFallback);
        }
        else
        {
            topInset = Math.Max(topInset, chromeTopOffset + EditorTopInsetFallback);
        }

        if (topInset <= 0)
        {
            topInset = DeviceInfo.Platform == DevicePlatform.Android
                ? AndroidEditorTopInsetFallback
                : EditorTopInsetFallback;
        }

        EditorHost.Margin = new Thickness(0, Math.Ceiling(topInset), 0, 0);
    }

    private void RenderHomeNotes(IReadOnlyList<WorkspaceNote> notes)
    {
        CancelHomeFeedRender();
        HomeNotesList.Children.Clear();
        HomeCountLabel.Text = TF("HomeDocCountFormat", "{0} docs", notes.Count);

        if (notes.Count == 0)
        {
            RenderHomeEmptyState(
                T("HomeNoDocumentsYet", "No documents imported yet"),
                T("HomeNoDocumentsYetHint", "Tap + on the bottom-right to import PDF"));
            return;
        }

        RenderHomeFeedInBatches(notes, CreateHomeNoteCard);
    }

    private void RenderHomeFolders(IReadOnlyList<string> folders)
    {
        CancelHomeFeedRender();
        HomeNotesList.Children.Clear();
        HomeCountLabel.Text = TF("HomeFolderCountFormat", "{0} folders", folders.Count);

        if (folders.Count == 0)
        {
            RenderHomeEmptyState(
                T("HomeNoSubfolders", "No subfolders in this directory"),
                T("HomeNoSubfoldersHint", "Create folders in Workspace settings"));
            return;
        }

        RenderHomeFeedInBatches(folders, CreateHomeFolderCard);
    }

    private const double HomeCardWidth = 164d;
    private const double HomeCardPreviewHeight = 206d;
    private const float HomeCardCornerRadius = 16f;
    private const float HomePreviewCornerRadius = 12f;
    private Color HomeCardBackground => IsDarkTheme ? Color.FromArgb("#233242") : Color.FromArgb("#FFFFFF");
    private Color HomeCardStroke => IsDarkTheme ? Color.FromArgb("#3A4A5E") : Color.FromArgb("#E5E7ED");
    private Color HomePreviewBackground => IsDarkTheme ? Color.FromArgb("#2B3A4B") : Color.FromArgb("#F3F4F7");
    private Color HomePreviewStroke => IsDarkTheme ? Color.FromArgb("#44566D") : Color.FromArgb("#E1E4EB");
    private Color HomePreviewPaperBackground => IsDarkTheme ? Color.FromArgb("#34475B") : Color.FromArgb("#FFFFFF");
    private Color HomeMetaChipBackground => IsDarkTheme ? Color.FromArgb("#304154") : Color.FromArgb("#F4F5F8");
    private Color HomeMetaChipStroke => IsDarkTheme ? Color.FromArgb("#43566F") : Color.FromArgb("#E4E7EE");

    private void RenderHomeEmptyState(string title, string description)
    {
        CancelHomeFeedRender();
        HomeNotesList.Children.Add(new Border
        {
            WidthRequest = 300,
            Margin = new Thickness(0, 8, 16, 18),
            BackgroundColor = HomeCardBackground,
            Stroke = HomeCardStroke,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(18, 16),
            Content = new VerticalStackLayout
            {
                Spacing = 9,
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
                        Text = T("ImportPdf", "Import PDF"),
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

    private void CancelHomeFeedRender()
    {
        _homeFeedRenderCts?.Cancel();
        _homeFeedRenderCts?.Dispose();
        _homeFeedRenderCts = null;
    }

    private void RenderHomeFeedInBatches<T>(IReadOnlyList<T> items, Func<T, View> itemFactory)
    {
        if (items.Count == 0)
            return;

        var cts = new CancellationTokenSource();
        _homeFeedRenderCts = cts;
        var token = cts.Token;
        var index = 0;

        void AppendBatch()
        {
            if (token.IsCancellationRequested)
                return;

            var end = Math.Min(index + HomeFeedRenderBatchSize, items.Count);
            while (index < end)
            {
                HomeNotesList.Children.Add(itemFactory(items[index]));
                index++;
            }

            if (index < items.Count)
            {
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(12), AppendBatch);
            }
        }

        AppendBatch();
    }

    private View CreateHomeFolderCard(string folderPath)
    {
        var folderName = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "/";
        var card = CreateHomeCardContainer();

        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(HomeCardPreviewHeight) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 8
        };

        var preview = new Border
        {
            HeightRequest = HomeCardPreviewHeight,
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = HomePreviewBackground,
            Stroke = HomePreviewStroke,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = HomePreviewCornerRadius },
            Padding = new Thickness(6)
        };
        preview.Content = new Border
        {
            BackgroundColor = HomePreviewPaperBackground,
            Stroke = IsDarkTheme ? Color.FromArgb("#5D779A") : Color.FromArgb("#D8E1EE"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = HomePreviewCornerRadius - 2f },
            Content = new Image
            {
                Source = "icon_folder.png",
                WidthRequest = 22,
                HeightRequest = 22,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Opacity = 0.78
            }
        };
        content.Add(preview);
        Grid.SetRow(preview, 0);

        var folderTitle = new Label
        {
            Text = folderName,
            FontFamily = "OpenSansSemibold",
            FontSize = 13,
            MaxLines = 2,
            LineBreakMode = LineBreakMode.TailTruncation,
            HorizontalTextAlignment = TextAlignment.Start,
            TextColor = ThemePrimaryText
        };
        content.Children.Add(folderTitle);
        Grid.SetRow(folderTitle, 1);

        var metaRow = new HorizontalStackLayout
        {
            Spacing = 6
        };
        metaRow.Children.Add(CreateHomeMetaChip(T("Folder", "Folder")));
        metaRow.Children.Add(CreateHomeMetaChip(FormatDateForHome(DateTime.Now)));
        content.Add(metaRow);
        Grid.SetRow(metaRow, 2);

        card.Content = content;
        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                _workspaceFolder = folderPath;
                _homeFilter = HomeFilterType.All;
                UpdateHomeFilterButtons();
                await RefreshWorkspaceViewsAsync();
                ShowStatus(TF("StatusEnteredFolderFormat", "Entered folder: {0}", folderName));
            })
        });

        return card;
    }

    private View CreateHomeNoteCard(WorkspaceNote note)
    {
        var selected = note.Id == _currentNoteId;
        var batchSelected = _selectedHomeNoteIds.Contains(note.Id);
        var card = CreateHomeCardContainer(selected);

        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(HomeCardPreviewHeight) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 8
        };

        var preview = new Border
        {
            HeightRequest = HomeCardPreviewHeight,
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = HomePreviewBackground,
            Stroke = selected ? Color.FromArgb("#4A90E2") : HomePreviewStroke,
            StrokeThickness = selected ? 1.5 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = HomePreviewCornerRadius },
            Padding = new Thickness(6)
        };

        var previewImage = new Image
        {
            Source = "icon_file.png",
            Aspect = Aspect.AspectFill,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        var placeholderGlyph = new Image
        {
            Source = "icon_file.png",
            WidthRequest = 18,
            HeightRequest = 18,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Opacity = 0.24
        };

        preview.Content = new Border
        {
            BackgroundColor = HomePreviewPaperBackground,
            Stroke = IsDarkTheme ? Color.FromArgb("#5D779A") : Color.FromArgb("#D8E1EE"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = HomePreviewCornerRadius - 2f },
            Content = new Grid
            {
                Children =
                {
                    previewImage,
                    placeholderGlyph
                }
            }
        };
        if (preview.Content is Border previewPaper
            && previewPaper.Content is Grid previewGrid)
        {
            if (_isHomeSelectionMode)
            {
                previewGrid.Children.Add(new Border
                {
                    WidthRequest = 21,
                    HeightRequest = 21,
                    StrokeThickness = 1,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 11 },
                    Stroke = batchSelected ? Color.FromArgb("#4A90E2") : Color.FromArgb("#9AA8BC"),
                    BackgroundColor = batchSelected
                        ? Color.FromArgb("#4A90E2")
                        : (IsDarkTheme ? Color.FromArgb("#324860") : Color.FromArgb("#E8EDF4")),
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(6, 6, 0, 0),
                    Padding = 0,
                    ZIndex = 9,
                    Content = new Label
                    {
                        Text = batchSelected ? "✓" : string.Empty,
                        HorizontalTextAlignment = TextAlignment.Center,
                        VerticalTextAlignment = TextAlignment.Center,
                        FontSize = 12,
                        TextColor = Colors.White
                    }
                });
            }

            var menuButton = new ImageButton
            {
                Source = "home_more.png",
                WidthRequest = 28,
                HeightRequest = 28,
                MinimumWidthRequest = 28,
                MinimumHeightRequest = 28,
                Padding = 6,
                Aspect = Aspect.AspectFit,
                CornerRadius = 14,
                BackgroundColor = IsDarkTheme ? Color.FromArgb("#B62A3F58") : Color.FromArgb("#B8FFFFFF"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 6, 6, 0),
                ZIndex = 8
            };

            CancellationTokenSource? longPressCts = null;
            var handledByLongPress = false;
            menuButton.Pressed += (_, _) =>
            {
                handledByLongPress = false;
                longPressCts?.Cancel();
                longPressCts?.Dispose();
                longPressCts = new CancellationTokenSource();
                var token = longPressCts.Token;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(430, token).ConfigureAwait(false);
                        if (token.IsCancellationRequested)
                            return;

                        handledByLongPress = true;
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await ShowHomeNoteActionsAsync(note);
                        });
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }, token);
            };
            menuButton.Released += (_, _) =>
            {
                longPressCts?.Cancel();
            };
            menuButton.Clicked += async (_, _) =>
            {
                if (handledByLongPress)
                    return;
                await ShowHomeNoteActionsAsync(note);
            };

            previewGrid.Children.Add(menuButton);
        }
        content.Add(preview);
        Grid.SetRow(preview, 0);
        BindHomeCoverPreview(note, previewImage, placeholderGlyph);

        var titleGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 4
        };
        titleGrid.Children.Add(new Label
        {
            Text = note.Name,
            FontFamily = "OpenSansSemibold",
            FontSize = 13,
            MaxLines = 2,
            LineBreakMode = LineBreakMode.TailTruncation,
            HorizontalTextAlignment = TextAlignment.Start,
            TextColor = ThemePrimaryText
        });
        titleGrid.Children.Add(new Label
        {
            Text = string.Empty,
            WidthRequest = 14
        });
        content.Add(titleGrid);
        Grid.SetRow(titleGrid, 1);

        var metaGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 6
        };
        metaGrid.Children.Add(CreateHomeMetaChip(TF("PageCountFormat", "{0} pages", EstimatePages(note))));
        var dateChip = CreateHomeMetaChip(FormatDateForHome(note.LastOpenedAtUtc));
        metaGrid.Children.Add(dateChip);
        Grid.SetColumn(dateChip, 1);
        content.Add(metaGrid);
        Grid.SetRow(metaGrid, 2);

        card.Content = content;
        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                if (_isHomeSelectionMode)
                {
                    ToggleHomeSelection(note.Id);
                    return;
                }

                if (_isTrashView)
                {
                    await ShowHomeNoteActionsAsync(note);
                    return;
                }

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

    private Border CreateHomeCardContainer(bool selected = false)
    {
        return new Border
        {
            WidthRequest = HomeCardWidth,
            Margin = new Thickness(0, 0, 14, 18),
            BackgroundColor = HomeCardBackground,
            Stroke = selected ? Color.FromArgb("#4A90E2") : HomeCardStroke,
            StrokeThickness = selected ? 1.6 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = HomeCardCornerRadius },
            Padding = new Thickness(10, 10, 10, 9)
        };
    }

    private Border CreateHomeMetaChip(string text)
    {
        return new Border
        {
            BackgroundColor = HomeMetaChipBackground,
            Stroke = HomeMetaChipStroke,
            StrokeThickness = 1,
            Padding = new Thickness(7, 3),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Content = new Label
            {
                Text = text,
                FontSize = 10,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                TextColor = ThemeSecondaryText
            }
        };
    }

    private void ToggleHomeSelection(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return;

        if (_selectedHomeNoteIds.Contains(noteId))
        {
            _selectedHomeNoteIds.Remove(noteId);
        }
        else
        {
            _selectedHomeNoteIds.Add(noteId);
        }

        if (_selectedHomeNoteIds.Count == 0)
        {
            _isHomeSelectionMode = false;
        }

        RefreshHomeFeed();
        ShowStatus(TF("SelectionCountFormat", "{0} selected", _selectedHomeNoteIds.Count));
    }

    private async Task ShowHomeBatchActionsAsync()
    {
        if (_selectedHomeNoteIds.Count == 0)
        {
            _isHomeSelectionMode = false;
            RefreshHomeFeed();
            return;
        }

        var action = _isTrashView
            ? await DisplayActionSheet(
                TF("SelectionCountFormat", "{0} selected", _selectedHomeNoteIds.Count),
                T("CancelAction", "Cancel"),
                null,
                T("RestoreFromTrash", "Restore"),
                T("DeletePermanently", "Delete Permanently"),
                T("ExitSelection", "Exit Selection"))
            : await DisplayActionSheet(
                TF("SelectionCountFormat", "{0} selected", _selectedHomeNoteIds.Count),
                T("CancelAction", "Cancel"),
                null,
                T("MoveToTrash", "Move to Trash"),
                T("ExitSelection", "Exit Selection"));

        if (action == T("ExitSelection", "Exit Selection"))
        {
            _isHomeSelectionMode = false;
            _selectedHomeNoteIds.Clear();
            RefreshHomeFeed();
            return;
        }

        if (_isTrashView && action == T("RestoreFromTrash", "Restore"))
        {
            foreach (var noteId in _selectedHomeNoteIds.ToArray())
            {
                await _workspaceService.RestoreFromTrashAsync(noteId);
            }

            _isHomeSelectionMode = false;
            _selectedHomeNoteIds.Clear();
            await RefreshWorkspaceViewsAsync();
            return;
        }

        if (_isTrashView && action == T("DeletePermanently", "Delete Permanently"))
        {
            foreach (var noteId in _selectedHomeNoteIds.ToArray())
            {
                await _workspaceService.DeleteNotePermanentlyAsync(noteId);
                InvalidateHomeCoverCacheForNote(noteId);
            }

            _isHomeSelectionMode = false;
            _selectedHomeNoteIds.Clear();
            await RefreshWorkspaceViewsAsync();
            return;
        }

        if (!_isTrashView && action == T("MoveToTrash", "Move to Trash"))
        {
            foreach (var noteId in _selectedHomeNoteIds.ToArray())
            {
                await _workspaceService.MoveToTrashAsync(noteId);
            }

            _isHomeSelectionMode = false;
            _selectedHomeNoteIds.Clear();
            await RefreshWorkspaceViewsAsync();
        }
    }

    private async Task ShowHomeNoteActionsAsync(WorkspaceNote note)
    {
        if (_isTrashView)
        {
            var trashAction = await DisplayActionSheet(
                note.Name,
                T("CancelAction", "Cancel"),
                null,
                T("RestoreFromTrash", "Restore"),
                T("DeletePermanently", "Delete Permanently"));
            if (trashAction == T("RestoreFromTrash", "Restore"))
            {
                var restored = await _workspaceService.RestoreFromTrashAsync(note.Id);
                if (restored)
                {
                    ShowStatus(T("RestoredFromTrash", "Restored from trash."));
                    await RefreshWorkspaceViewsAsync();
                }
            }
            else if (trashAction == T("DeletePermanently", "Delete Permanently"))
            {
                var deleted = await _workspaceService.DeleteNotePermanentlyAsync(note.Id);
                if (deleted)
                {
                    InvalidateHomeCoverCacheForNote(note.Id);
                    ShowStatus(T("DeletePermanentlyDone", "Deleted permanently."));
                    await RefreshWorkspaceViewsAsync();
                }
            }

            return;
        }

        var action = await DisplayActionSheet(
            note.Name,
            T("CancelAction", "Cancel"),
            null,
            T("RenameAction", "Rename"),
            T("SelectMultiple", "Select Multiple"),
            T("ChangeCoverAction", "Change Cover"),
            T("RefreshCover", "Refresh Cover"),
            T("MoveToTrash", "Move to Trash"));

        if (action == T("RenameAction", "Rename"))
        {
            var updatedName = await DisplayPromptAsync(
                T("RenameAction", "Rename"),
                T("RenamePrompt", "Enter a new name"),
                T("SaveAction", "Save"),
                T("CancelAction", "Cancel"),
                note.Name,
                maxLength: 120);
            if (!string.IsNullOrWhiteSpace(updatedName))
            {
                var renamed = await _workspaceService.RenameNoteAsync(note.Id, updatedName);
                if (renamed)
                {
                    ShowStatus(T("RenameSuccess", "Renamed."));
                    await RefreshWorkspaceViewsAsync();
                }
                else
                {
                    ShowStatus(T("RenameFailed", "Rename failed."));
                }
            }

            return;
        }

        if (action == T("SelectMultiple", "Select Multiple"))
        {
            _isHomeSelectionMode = true;
            _selectedHomeNoteIds.Clear();
            _selectedHomeNoteIds.Add(note.Id);
            RefreshHomeFeed();
            ShowStatus(TF("SelectionCountFormat", "{0} selected", _selectedHomeNoteIds.Count));
            return;
        }

        if (action == T("RefreshCover", "Refresh Cover"))
        {
            InvalidateHomeCoverCacheForNote(note.Id);
            var pdfBytes = await _workspaceService.GetPdfBytesAsync(note.Id);
            if (pdfBytes is { Length: > 0 })
            {
                QueuePrimeHomeCoverCache(note, pdfBytes);
                ShowStatus(T("RefreshCoverQueued", "Cover refresh queued."));
                await RefreshWorkspaceViewsAsync();
            }

            return;
        }

        if (action == T("ChangeCoverAction", "Change Cover"))
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = T("ChangeCoverAction", "Change Cover"),
                    FileTypes = FilePickerFileType.Images
                });
                if (result is null)
                    return;

                var saved = await SetCustomHomeCoverAsync(note.Id, result);
                if (saved)
                {
                    ShowStatus(T("ChangeCoverSuccess", "Cover updated."));
                    await RefreshWorkspaceViewsAsync();
                }
                else
                {
                    ShowStatus(T("ChangeCoverFailed", "Failed to update cover."));
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"{T("ChangeCoverFailed", "Failed to update cover.")} {ex.Message}");
            }

            return;
        }

        if (action == T("MoveToTrash", "Move to Trash"))
        {
            var moved = await _workspaceService.MoveToTrashAsync(note.Id);
            if (moved)
            {
                if (string.Equals(_currentNoteId, note.Id, StringComparison.Ordinal))
                {
                    _currentNoteId = null;
                }
                ShowStatus(T("MovedToTrash", "Moved to trash."));
                await RefreshWorkspaceViewsAsync();
            }
        }
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
            RecentNotesList.Children.Add(new Label { Text = T("NoRecentNotes", "No recent notes"), FontSize = 11, TextColor = ThemeSecondaryText });
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
                TF("FolderLabelFormat", "[Folder] {0}", folderName),
                async () =>
                {
                    _workspaceFolder = folder;
                    await RefreshWorkspaceViewsAsync();
                },
                false));
        }

        if (browse.SubFolders.Count == 0)
        {
            WorkspaceFolderList.Children.Add(new Label { Text = T("NoSubfolders", "No subfolders"), FontSize = 11, TextColor = ThemeSecondaryText });
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
            WorkspaceNoteList.Children.Add(new Label { Text = T("NoNotesInFolder", "No notes in this folder"), FontSize = 11, TextColor = ThemeSecondaryText });
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
            ShowStatus(T("EnterFolderNameFirst", "Enter a folder name first."));
            return;
        }

        var created = await _workspaceService.CreateFolderAsync(_workspaceFolder, name);
        if (!created)
        {
            ShowStatus(T("InvalidFolderName", "Invalid folder name."));
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
