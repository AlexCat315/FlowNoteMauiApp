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
        Recent,
        Name,
        Created
    }

    private HomeFilterType _homeFilter = HomeFilterType.All;
    private HomeSortType _homeSort = HomeSortType.Recent;
    private bool _isHomeSortDescending = true;
    private string _homeSearchKeyword = string.Empty;
    private IReadOnlyList<WorkspaceNote> _cachedHomeNotes = Array.Empty<WorkspaceNote>();
    private IReadOnlyList<string> _cachedHomeFolders = Array.Empty<string>();

    private void SetDrawerVisible(bool visible)
    {
        DrawerOverlayView.IsVisible = visible;
        DrawerOverlayView.InputTransparent = !visible;
        HomeDrawerOverlay.IsVisible = visible;
    }

    private void SetSettingsVisible(bool visible)
    {
        SettingsOverlayView.IsVisible = visible;
        SettingsOverlayView.InputTransparent = !visible;
        SettingsOverlay.IsVisible = visible;
        SettingsPanel.IsVisible = visible;
        if (visible)
        {
            SetSettingsSection(SettingsSection.Home);
            RefreshSettingsUiState();
        }
    }

    private void RefreshHomeFeed()
    {
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

            folderQuery = _isHomeSortDescending
                ? folderQuery.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                : folderQuery.OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

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

        query = (_homeSort, _isHomeSortDescending) switch
        {
            (HomeSortType.Name, true) => query.OrderByDescending(n => n.Name, StringComparer.OrdinalIgnoreCase),
            (HomeSortType.Name, false) => query.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase),
            (HomeSortType.Created, true) => query.OrderByDescending(n => n.CreatedAtUtc),
            (HomeSortType.Created, false) => query.OrderBy(n => n.CreatedAtUtc),
            (HomeSortType.Recent, true) => query.OrderByDescending(n => n.LastOpenedAtUtc).ThenByDescending(n => n.ModifiedAtUtc),
            _ => query.OrderBy(n => n.LastOpenedAtUtc).ThenBy(n => n.ModifiedAtUtc)
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
        return note.Name.Contains("笔记", StringComparison.OrdinalIgnoreCase)
            || note.Name.Contains("note", StringComparison.OrdinalIgnoreCase)
            || note.Name.Contains("手写", StringComparison.OrdinalIgnoreCase)
            || note.Name.Contains("课堂", StringComparison.OrdinalIgnoreCase);
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
        RefreshHomeFeed();
    }

    private string GetHomeSortDescription()
    {
        var modeText = _homeSort switch
        {
            HomeSortType.Name => T("SortByName", "Name"),
            HomeSortType.Created => T("SortByCreated", "Created"),
            _ => T("SortByRecent", "Recent")
        };

        return $"{modeText} {(_isHomeSortDescending ? T("SortDescending", "Descending") : T("SortAscending", "Ascending"))}";
    }

    private void UpdateHomeSortLabel()
    {
        HomeSortButton.Text = T("HomeSort", "Sort");
    }

    private void OnHomeFilterAllClicked(object? sender, EventArgs e) => SetHomeFilter(HomeFilterType.All);
    private void OnHomeFilterPdfClicked(object? sender, EventArgs e) => SetHomeFilter(HomeFilterType.Pdf);
    private void OnHomeFilterNoteClicked(object? sender, EventArgs e) => SetHomeFilter(HomeFilterType.Note);
    private void OnHomeFilterFolderClicked(object? sender, EventArgs e) => SetHomeFilter(HomeFilterType.Folder);

    private void OnHomeSortClicked(object? sender, EventArgs e)
    {
        _homeSort = _homeSort switch
        {
            HomeSortType.Recent => HomeSortType.Name,
            HomeSortType.Name => HomeSortType.Created,
            _ => HomeSortType.Recent
        };

        if (_homeSort == HomeSortType.Recent)
            _isHomeSortDescending = !_isHomeSortDescending;

        RefreshHomeFeed();
        ShowStatus(TF("CurrentSortFormat", "Current sort: {0}", GetHomeSortDescription()));
    }

    private void OnHomeSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _homeSearchKeyword = e.NewTextValue?.Trim() ?? string.Empty;
        RefreshHomeFeed();
    }

    private async void OnHomeQuickPenClicked(object? sender, EventArgs e)
    {
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
        SetDrawerVisible(false);
        SetInputModePanelVisible(false);
        SetSettingsVisible(true);
        _ = RefreshWorkspaceViewsAsync();
    }

    private void OnSettingsOverlayTapped(object? sender, TappedEventArgs e)
    {
        SetInputModePanelVisible(false);
        SetSettingsVisible(false);
    }

    private void OnDrawerBackdropTapped(object? sender, TappedEventArgs e)
    {
        SetInputModePanelVisible(false);
        SetDrawerVisible(false);
    }

    private void OnDrawerAllDocsClicked(object? sender, EventArgs e)
    {
        _homeFilter = HomeFilterType.All;
        RefreshHomeFeed();
        SetDrawerVisible(false);
    }

    private void OnDrawerRecentClicked(object? sender, EventArgs e)
    {
        _homeFilter = HomeFilterType.All;
        _homeSort = HomeSortType.Recent;
        _isHomeSortDescending = true;
        RefreshHomeFeed();
        SetDrawerVisible(false);
    }

    private void OnDrawerFavoriteClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("FeatureFavoritesPending", "Favorites is under development."));
        SetDrawerVisible(false);
    }

    private void OnDrawerTrashClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("FeatureTrashPending", "Trash is under development."));
        SetDrawerVisible(false);
    }

    private void OnDrawerEditTagsClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("FeatureEditTagsPending", "Tag editing is coming soon."));
    }

    private void OnDrawerCreateTagClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("FeatureCreateTagPending", "Tag creation is coming soon."));
    }

    private void OnDrawerHelpClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("FeatureHelpPending", "Help center is coming soon."));
        SetDrawerVisible(false);
    }

    private void OnDrawerAboutClicked(object? sender, EventArgs e)
    {
        ShowStatus("FlowNote MAUI Demo v1.0");
        SetDrawerVisible(false);
    }

    private void OnDrawerDiscountClicked(object? sender, EventArgs e)
    {
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
            case "语言":
            {
                var isZh = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
                LanguageManager.SetCulture(new CultureInfo(isZh ? "en-US" : "zh-CN"));
                ShowStatus(isZh ? T("StatusLanguageEnglish", "Language: English") : T("StatusLanguageChinese", "Language: Simplified Chinese"));
                break;
            }
            case "显示":
                ShowStatus(T("StatusDisplaySettingsOpened", "Display settings opened (theme integration pending)."));
                break;
            case "本地备份":
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
        _homeSort = HomeSortType.Recent;
        _isHomeSortDescending = true;
        _homeSearchKeyword = string.Empty;
        HomeSearchEntry.Text = string.Empty;

        RefreshHomeFeed();
        SavePersistedAppSettings();
        RefreshSettingsUiState();
        SetSettingsVisible(false);
        ShowStatus(T("StatusSettingsReset", "Settings reset to defaults."));
    }
}
