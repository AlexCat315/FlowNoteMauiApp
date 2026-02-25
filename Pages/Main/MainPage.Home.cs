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
        HomeDrawerOverlay.IsVisible = visible;
    }

    private void SetSettingsVisible(bool visible)
    {
        SettingsOverlay.IsVisible = visible;
        SettingsPanel.IsVisible = visible;
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
                HomeCountLabel.Text = "0 个文件夹";
                RenderHomeEmptyState("没有匹配的文件夹", "试试更换搜索词或返回全部分类");
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
            HomeCountLabel.Text = "0 个文档";
            RenderHomeEmptyState("没有匹配的文档", "可切换分类、排序或调整搜索关键词");
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
        SetFilterButtonVisual(HomeFilterAllButton, _homeFilter == HomeFilterType.All);
        SetFilterButtonVisual(HomeFilterPdfButton, _homeFilter == HomeFilterType.Pdf);
        SetFilterButtonVisual(HomeFilterNoteButton, _homeFilter == HomeFilterType.Note);
        SetFilterButtonVisual(HomeFilterFolderButton, _homeFilter == HomeFilterType.Folder);
    }

    private void SetFilterButtonVisual(Button button, bool selected)
    {
        if (selected)
        {
            button.BackgroundColor = IsDarkTheme ? Color.FromArgb("#234B9B") : Color.FromArgb("#E7EEFF");
            button.TextColor = IsDarkTheme ? Color.FromArgb("#CFE0FF") : Color.FromArgb("#1F56F5");
            button.FontFamily = "OpenSansSemibold";
        }
        else
        {
            button.BackgroundColor = Colors.Transparent;
            button.TextColor = ThemePrimaryText;
            button.FontFamily = "OpenSansRegular";
        }
    }

    private void UpdateHomeSortLabel()
    {
        var mode = _homeSort switch
        {
            HomeSortType.Name => "名称",
            HomeSortType.Created => "创建时间",
            _ => "最近"
        };

        HomeSortButton.Text = $"{mode} {(_isHomeSortDescending ? "↓" : "↑")}";
    }

    private void OnHomeFilterAllClicked(object? sender, EventArgs e)
    {
        _homeFilter = HomeFilterType.All;
        RefreshHomeFeed();
    }

    private void OnHomeFilterPdfClicked(object? sender, EventArgs e)
    {
        _homeFilter = HomeFilterType.Pdf;
        RefreshHomeFeed();
    }

    private void OnHomeFilterNoteClicked(object? sender, EventArgs e)
    {
        _homeFilter = HomeFilterType.Note;
        RefreshHomeFeed();
    }

    private void OnHomeFilterFolderClicked(object? sender, EventArgs e)
    {
        _homeFilter = HomeFilterType.Folder;
        RefreshHomeFeed();
    }

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
        ShowStatus($"当前排序: {HomeSortButton.Text}");
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
            ShowStatus("请先导入 PDF 文档");
            return;
        }

        try
        {
            await OpenWorkspaceNoteAsync(target);

            if (!IsEditorInitialized)
                return;

            DrawingCanvas.EnableDrawing = true;
            DrawingCanvas.IsVisible = true;
            DrawingToolbarPanel.IsVisible = true;
            UpdateToolSelection("Pen");
            DrawingToggleButton.BackgroundColor = IsDarkTheme
                ? Color.FromArgb("#324A6B")
                : Color.FromArgb("#DBE7FF");
        }
        catch (Exception ex)
        {
            ShowStatus(ex.Message);
        }
    }

    private void OnOpenSettingsClicked(object? sender, EventArgs e)
    {
        SetDrawerVisible(false);
        SetSettingsVisible(true);
        _ = RefreshWorkspaceViewsAsync();
    }

    private void OnSettingsOverlayTapped(object? sender, TappedEventArgs e)
    {
        SetSettingsVisible(false);
    }

    private void OnDrawerBackdropTapped(object? sender, TappedEventArgs e)
    {
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
        ShowStatus("收藏功能正在开发中");
        SetDrawerVisible(false);
    }

    private void OnDrawerTrashClicked(object? sender, EventArgs e)
    {
        ShowStatus("回收站功能正在开发中");
        SetDrawerVisible(false);
    }

    private void OnDrawerEditTagsClicked(object? sender, EventArgs e)
    {
        ShowStatus("标签编辑功能即将支持");
    }

    private void OnDrawerCreateTagClicked(object? sender, EventArgs e)
    {
        ShowStatus("创建标签功能即将支持");
    }

    private void OnDrawerHelpClicked(object? sender, EventArgs e)
    {
        ShowStatus("帮助中心即将上线");
        SetDrawerVisible(false);
    }

    private void OnDrawerAboutClicked(object? sender, EventArgs e)
    {
        ShowStatus("FlowNote MAUI Demo v1.0");
        SetDrawerVisible(false);
    }

    private void OnDrawerDiscountClicked(object? sender, EventArgs e)
    {
        ShowStatus("折扣活动功能即将支持");
        SetDrawerVisible(false);
    }

    private void OnSettingsQuickItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border row)
            return;

        var item = row.AutomationId ?? "设置项";

        switch (item)
        {
            case "语言":
            {
                var isZh = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
                LanguageManager.SetCulture(new CultureInfo(isZh ? "en-US" : "zh-CN"));
                ShowStatus(isZh ? "Language: English" : "语言: 简体中文");
                break;
            }
            case "显示":
                ShowStatus("显示设置入口已打开（主题切换待接入）");
                break;
            case "本地备份":
                ShowStatus("本地备份已触发");
                break;
            default:
                ShowStatus($"{item} 功能已记录，后续版本完善");
                break;
        }
    }

    private void OnResetSettingsClicked(object? sender, EventArgs e)
    {
        DisplayModePicker.SelectedIndex = (int)Flow.PDFView.Abstractions.PdfDisplayMode.SinglePageContinuous;
        OrientationPicker.SelectedIndex = (int)Flow.PDFView.Abstractions.PdfScrollOrientation.Vertical;
        FitPolicyPicker.SelectedIndex = (int)Flow.PDFView.Abstractions.FitPolicy.Width;

        ZoomSlider.Value = 1;
        EnableZoomSwitch.IsToggled = true;
        EnableSwipeSwitch.IsToggled = true;
        EnableLinkSwitch.IsToggled = true;
        EnableFingerDrawSwitch.IsToggled = false;
        StrokeWidthSlider.Value = 3;

        _homeFilter = HomeFilterType.All;
        _homeSort = HomeSortType.Recent;
        _isHomeSortDescending = true;
        _homeSearchKeyword = string.Empty;
        HomeSearchEntry.Text = string.Empty;

        RefreshHomeFeed();
        SetSettingsVisible(false);
        ShowStatus("已还原初始设置");
    }
}
