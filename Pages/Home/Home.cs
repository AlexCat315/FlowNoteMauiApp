using System.Globalization;
using FlowNoteMauiApp.Models;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private enum HomeFilterType
    {
        All,
        Pdf,
        Note,
        Folder
    }

    private enum HomeSortType
    {
        TimeAscending,
        TimeDescending,
        NameAscending,
        NameDescending
    }

    private HomeFilterType _homeFilter = HomeFilterType.All;
    private HomeSortType _homeSort = HomeSortType.TimeDescending;
    private string _homeSearchKeyword = string.Empty;
    private IReadOnlyList<WorkspaceNote> _cachedHomeNotes = Array.Empty<WorkspaceNote>();
    private IReadOnlyList<string> _cachedHomeFolders = Array.Empty<string>();
    private IReadOnlyList<WorkspaceNote> _cachedTrashedNotes = Array.Empty<WorkspaceNote>();
    private bool _isTrashView;

    private void SetDrawerVisible(bool visible)
    {
        if (visible)
        {
            SetHomeSortPanelVisible(false);
        }

        if (visible)
        {
            EnsureUiBootstrapped();
        }

        DrawerOverlayView.IsVisible = visible;
        DrawerOverlayView.InputTransparent = !visible;
        if (visible || _isUiBootstrapped)
        {
            HomeDrawerOverlay.IsVisible = visible;
        }
    }

    private void SetSettingsVisible(bool visible)
    {
        if (visible)
        {
            SetHomeSortPanelVisible(false);
        }

        if (visible)
        {
            EnsureUiBootstrapped();
        }

        SettingsOverlayView.IsVisible = visible;
        SettingsOverlayView.InputTransparent = !visible;
        if (visible || _isUiBootstrapped)
        {
            SettingsOverlay.IsVisible = visible;
            SettingsPanel.IsVisible = visible;
        }
        if (visible && _isUiBootstrapped)
        {
            SetSettingsSection(SettingsSection.Home);
            RefreshSettingsUiState();
        }
    }

    private void RefreshHomeFeed()
    {
        if (_isTrashView)
        {
            var trashNotes = _cachedTrashedNotes;
            if (!string.IsNullOrWhiteSpace(_homeSearchKeyword))
            {
                trashNotes = trashNotes.Where(note =>
                        note.Name.Contains(_homeSearchKeyword, StringComparison.OrdinalIgnoreCase)
                        || note.FolderPath.Contains(_homeSearchKeyword, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            RenderHomeNotes(trashNotes);
            return;
        }

        UpdateHomeSortLabel();
        UpdateHomeFilterButtons();

        if (_homeFilter == HomeFilterType.Folder)
        {
            IEnumerable<string> folderQuery = _cachedHomeFolders;

            if (!string.IsNullOrWhiteSpace(_homeSearchKeyword))
            {
                folderQuery = folderQuery.Where(f =>
                    f.Contains(_homeSearchKeyword, StringComparison.OrdinalIgnoreCase));
            }

            folderQuery = _homeSort switch
            {
                HomeSortType.NameDescending => folderQuery.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase),
                HomeSortType.NameAscending => folderQuery.OrderBy(f => f, StringComparer.OrdinalIgnoreCase),
                HomeSortType.TimeAscending => folderQuery.OrderBy(f => f, StringComparer.OrdinalIgnoreCase),
                _ => folderQuery.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            };

            var folders = folderQuery.ToList();
            if (folders.Count == 0 && (_cachedHomeFolders.Count > 0 || !string.IsNullOrWhiteSpace(_homeSearchKeyword)))
            {
                HomeNotesList.Children.Clear();
                HomeCountLabel.Text = TF("HomeFolderCountFormat", "{0} folders", 0);
                RenderHomeEmptyState(
                    T("HomeNoMatchingFolders", "No matching folders"),
                    T("HomeNoMatchingFoldersHint", "Try a different keyword or switch back to all"));
                return;
            }

            RenderHomeFolders(folders);
            return;
        }

        IEnumerable<WorkspaceNote> query = _cachedHomeNotes;

        if (_homeFilter == HomeFilterType.Note)
        {
            query = query.Where(IsLikelyHandwrittenNote);
        }

        if (!string.IsNullOrWhiteSpace(_homeSearchKeyword))
        {
            query = query.Where(note =>
                note.Name.Contains(_homeSearchKeyword, StringComparison.OrdinalIgnoreCase)
                || note.FolderPath.Contains(_homeSearchKeyword, StringComparison.OrdinalIgnoreCase));
        }

        query = _homeSort switch
        {
            HomeSortType.TimeAscending => query.OrderBy(n => n.ModifiedAtUtc).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase),
            HomeSortType.NameAscending => query.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase),
            HomeSortType.NameDescending => query.OrderByDescending(n => n.Name, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderByDescending(n => n.ModifiedAtUtc).ThenByDescending(n => n.Name, StringComparer.OrdinalIgnoreCase)
        };

        var notes = query.ToList();
        if (notes.Count == 0 && (_cachedHomeNotes.Count > 0 || !string.IsNullOrWhiteSpace(_homeSearchKeyword)))
        {
            HomeNotesList.Children.Clear();
            HomeCountLabel.Text = TF("HomeDocCountFormat", "{0} docs", 0);
            RenderHomeEmptyState(
                T("HomeNoMatchingDocs", "No matching documents"),
                T("HomeNoMatchingDocsHint", "Try changing filter, sort, or keyword"));
            return;
        }

        RenderHomeNotes(notes);
    }

    private static bool IsLikelyHandwrittenNote(WorkspaceNote note)
    {
        var keyword1 = T("HomeNoteKeyword1", "note");
        var keyword2 = T("HomeNoteKeyword2", "handwriting");
        var keyword3 = T("HomeNoteKeyword3", "class");
        return note.Name.Contains(keyword1, StringComparison.OrdinalIgnoreCase)
            || note.Name.Contains(keyword2, StringComparison.OrdinalIgnoreCase)
            || note.Name.Contains(keyword3, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateHomeFilterButtons()
    {
        UpdateHomeFilterVisual(FilterAllButton, FilterAllIndicator, _homeFilter == HomeFilterType.All);
        UpdateHomeFilterVisual(FilterPdfButton, FilterPdfIndicator, _homeFilter == HomeFilterType.Pdf);
        UpdateHomeFilterVisual(FilterNoteButton, FilterNoteIndicator, _homeFilter == HomeFilterType.Note);
        UpdateHomeFilterVisual(FilterFolderButton, FilterFolderIndicator, _homeFilter == HomeFilterType.Folder);
    }

    private void UpdateHomeFilterVisual(Button button, BoxView indicator, bool selected)
    {
        button.TextColor = selected
            ? Color.FromArgb("#4A90E2")
            : (IsDarkTheme ? Color.FromArgb("#F2F2F7") : Color.FromArgb("#1C1C1E"));
        indicator.IsVisible = selected;
    }

    private void SetHomeFilter(HomeFilterType filter)
    {
        if (_homeFilter == filter)
            return;

        _homeFilter = filter;
        SetHomeSortPanelVisible(false);
        RefreshHomeFeed();
    }

    private string GetHomeSortDescription()
    {
        return _homeSort switch
        {
            HomeSortType.TimeAscending => T("HomeSortTimeAsc", "Time Asc"),
            HomeSortType.TimeDescending => T("HomeSortTimeDesc", "Time Desc"),
            HomeSortType.NameAscending => T("HomeSortNameAsc", "Name Asc"),
            _ => T("HomeSortNameDesc", "Name Desc")
        };
    }

    private void UpdateHomeSortLabel()
    {
    }

    private void SetHomeSort(HomeSortType sortType)
    {
        _homeSort = sortType;
        RefreshHomeFeed();
        SetHomeSortPanelVisible(false);
        ShowStatus(TF("CurrentSortFormat", "Current sort: {0}", GetHomeSortDescription()));
    }

    private void SetHomeSortPanelVisible(bool visible)
    {
        if (!visible)
        {
            AnimatePopupOut(HomeSortPanel, () => HomeSortPanel.IsVisible = false);
            return;
        }

        HomeSortPanel.IsVisible = true;
        PositionHomeSortPanelUnderSortButton();
        AnimatePopupIn(HomeSortPanel);
    }

    private void PositionHomeSortPanelUnderSortButton()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            void ApplyPosition(int attempt)
            {
                var panelWidth = HomeSortPanel.Width > 1
                    ? HomeSortPanel.Width
                    : (HomeSortPanel.WidthRequest > 1 ? HomeSortPanel.WidthRequest : 176d);
                var anchorX = GetVisualOffsetX(HomeSortMenuButton, HomePanelView);
                var anchorY = GetVisualOffsetY(HomeSortMenuButton, HomePanelView);
                var anchorWidth = HomeSortMenuButton.Width > 1 ? HomeSortMenuButton.Width : HomeSortMenuButton.WidthRequest;
                var anchorHeight = HomeSortMenuButton.Height > 1 ? HomeSortMenuButton.Height : HomeSortMenuButton.HeightRequest;

                var hasValidLayout = panelWidth > 0
                    && anchorWidth > 1
                    && anchorHeight > 1
                    && HomePanelView.Width > 1;
                if (!hasValidLayout)
                {
                    if (attempt < 12)
                    {
                        HomeSortPanel.Dispatcher.DispatchDelayed(
                            TimeSpan.FromMilliseconds(16),
                            () => ApplyPosition(attempt + 1));
                    }
                    return;
                }

                var targetX = anchorX + anchorWidth - panelWidth;
                targetX = Math.Clamp(targetX, 10d, Math.Max(10d, HomePanelView.Width - panelWidth - 10d));
                var targetY = anchorY + anchorHeight + 5d;
                HomeSortPanel.TranslationX = 0;
                HomeSortPanel.TranslationY = 0;
                HomeSortPanel.Margin = new Thickness(targetX, targetY, 0, 0);
            }

            ApplyPosition(0);
        });
    }

    private void OnHomeLayoutChanged(object? sender, EventArgs e)
    {
        if (!HomeSortPanel.IsVisible)
            return;

        PositionHomeSortPanelUnderSortButton();
    }

    private void OnHomeFilterAllClicked(object? sender, EventArgs e) => SetHomeFilter(HomeFilterType.All);
    private void OnHomeFilterPdfClicked(object? sender, EventArgs e) => SetHomeFilter(HomeFilterType.Pdf);
    private void OnHomeFilterNoteClicked(object? sender, EventArgs e) => SetHomeFilter(HomeFilterType.Note);
    private void OnHomeFilterFolderClicked(object? sender, EventArgs e) => SetHomeFilter(HomeFilterType.Folder);

    private void OnHomeSortClicked(object? sender, EventArgs e)
    {
        if (_isHomeSelectionMode)
        {
            _ = ShowHomeBatchActionsAsync();
            return;
        }

        SetHomeSortPanelVisible(!HomeSortPanel.IsVisible);
    }

    private void OnHomeSortTimeAscClicked(object? sender, EventArgs e)
    {
        SetHomeSort(HomeSortType.TimeAscending);
    }

    private void OnHomeSortTimeDescClicked(object? sender, EventArgs e)
    {
        SetHomeSort(HomeSortType.TimeDescending);
    }

    private void OnHomeSortNameAscClicked(object? sender, EventArgs e)
    {
        SetHomeSort(HomeSortType.NameAscending);
    }

    private void OnHomeSortNameDescClicked(object? sender, EventArgs e)
    {
        SetHomeSort(HomeSortType.NameDescending);
    }

    private void OnHomeSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        SetHomeSortPanelVisible(false);
        _homeSearchKeyword = e.NewTextValue?.Trim() ?? string.Empty;
        RefreshHomeFeed();
    }

    private async void OnHomeQuickPenClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        var target = _cachedHomeNotes.FirstOrDefault(n => n.Id == _currentNoteId)
            ?? _cachedHomeNotes.FirstOrDefault();

        if (target is null)
        {
            ShowStatus(T("ImportPdfFirst", "Please import a PDF first."));
            return;
        }

        try
        {
            await OpenWorkspaceNoteAsync(target);

            if (!IsEditorInitialized)
                return;

            ApplyInputMode(DrawingInputMode.PenStylus);
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
    }

    private void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        EnsureUiBootstrapped();
        SetHomeSortPanelVisible(false);
        SetDrawerVisible(false);
        SetInputModePanelVisible(false);
        DrawingToolbarPanel.IsVisible = false;
        ThumbnailPanel.IsVisible = false;
        LayerPanel.IsVisible = false;
        SetSettingsVisible(true);
        _ = RefreshWorkspaceViewsAsync();
    }

    private void OnSettingsOverlayTapped(object? sender, TappedEventArgs e)
    {
        SetHomeSortPanelVisible(false);
        SetInputModePanelVisible(false);
        SetSettingsVisible(false);
    }

    private void OnDrawerBackdropTapped(object? sender, TappedEventArgs e)
    {
        SetHomeSortPanelVisible(false);
        SetInputModePanelVisible(false);
        SetDrawerVisible(false);
    }

    private void OnDrawerAllDocsClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        _isTrashView = false;
        _homeFilter = HomeFilterType.All;
        RefreshHomeFeed();
        SetDrawerVisible(false);
    }

    private void OnDrawerRecentClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        _isTrashView = false;
        _homeFilter = HomeFilterType.All;
        _homeSort = HomeSortType.TimeDescending;
        RefreshHomeFeed();
        SetDrawerVisible(false);
    }

    private void OnDrawerFavoriteClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        ShowStatus(T("FeatureFavoritesPending", "Favorites is under development."));
        SetDrawerVisible(false);
    }

    private void OnDrawerTrashClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        _isTrashView = true;
        _homeFilter = HomeFilterType.All;
        RefreshHomeFeed();
        ShowStatus(T("FeatureTrashOpened", "Trash opened."));
        SetDrawerVisible(false);
    }

    private void OnDrawerEditTagsClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        ShowStatus(T("FeatureEditTagsPending", "Tag editing is coming soon."));
    }

    private void OnDrawerCreateTagClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        ShowStatus(T("FeatureCreateTagPending", "Tag creation is coming soon."));
    }

    private void OnDrawerHelpClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        ShowStatus(T("FeatureHelpPending", "Help center is coming soon."));
        SetDrawerVisible(false);
    }

    private void OnDrawerAboutClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        ShowStatus(T("AboutAppName", "FlowNote MAUI Demo v1.0"));
        SetDrawerVisible(false);
    }

    private void OnDrawerDiscountClicked(object? sender, EventArgs e)
    {
        SetHomeSortPanelVisible(false);
        ShowStatus(T("FeatureDiscountPending", "Discount activity is coming soon."));
        SetDrawerVisible(false);
    }

    private void OnSettingsQuickItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border row)
            return;

        var item = row.AutomationId ?? T("SettingsItem", "Settings Item");

        switch (item)
        {
            case "Language":
                {
                    var isZh = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
                    LanguageManager.SetCulture(new CultureInfo(isZh ? "en-US" : "zh-CN"));
                    ShowStatus(isZh ? T("StatusLanguageEnglish", "Language: English") : T("StatusLanguageChinese", "Language: Simplified Chinese"));
                    break;
                }
            case "Display":
                ShowStatus(T("StatusDisplaySettingsOpened", "Display settings opened (theme integration pending)."));
                break;
            case "LocalBackup":
                ShowStatus(T("StatusLocalBackupTriggered", "Local backup triggered."));
                break;
            default:
                ShowStatus(TF("StatusFeatureRecordedFormat", "{0} feature recorded for future release.", item));
                break;
        }
    }

    private void OnResetSettingsClicked(object? sender, EventArgs e)
    {
        ResetAppSettingValuesToDefault();

        DisplayModePicker.SelectedIndex = (int)_savedDisplayMode;
        OrientationPicker.SelectedIndex = (int)_savedScrollOrientation;
        FitPolicyPicker.SelectedIndex = (int)_savedFitPolicy;

        ZoomSlider.Value = _savedZoom;
        EnableZoomSwitch.IsToggled = _savedEnableZoom;
        EnableSwipeSwitch.IsToggled = _savedEnableSwipe;
        EnableLinkSwitch.IsToggled = _savedEnableLink;
        ApplyInputMode(DrawingInputMode.PenStylus, activateDrawing: false);
        if (IsEditorInitialized)
        {
            DrawingCanvas.ZoomAffectsStrokeWidth = _zoomFollowEnabled;
        }

        ApplyGlobalSettings();
        ApplyDarkModeInversion();
        StrokeWidthSlider.Value = 3;

        _homeFilter = HomeFilterType.All;
        _homeSort = HomeSortType.TimeDescending;
        _homeSearchKeyword = string.Empty;
        HomeSearchEntry.Text = string.Empty;

        RefreshHomeFeed();
        SavePersistedAppSettings();
        RefreshSettingsUiState();
        SetSettingsVisible(false);
        ShowStatus(T("StatusSettingsReset", "Settings reset to defaults."));
    }
}
