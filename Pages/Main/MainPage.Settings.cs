using System.Globalization;
using Flow.PDFView.Abstractions;
using Microsoft.Maui.Storage;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private enum SettingsSection
    {
        Home,
        Page,
        Display,
        Language
    }

    private enum DateFormatPreference
    {
        YYMMDD,
        YYYYMMDD,
        DDMMYY,
        MMDDYY
    }

    private const string DisplayModeKey = "settings.viewer.display_mode";
    private const string ScrollOrientationKey = "settings.viewer.scroll_orientation";
    private const string FitPolicyKey = "settings.viewer.fit_policy";
    private const string ZoomValueKey = "settings.viewer.zoom";
    private const string EnableZoomKey = "settings.viewer.enable_zoom";
    private const string EnableSwipeKey = "settings.viewer.enable_swipe";
    private const string EnableLinkKey = "settings.viewer.enable_link";

    private const string PageFreeMoveKey = "settings.page.free_move";
    private const string PageMoveResistanceKey = "settings.page.move_resistance";
    private const string ZoomFollowKey = "settings.page.zoom_follow";
    private const string PageNumberPositionKey = "settings.page.page_number_position";
    private const string TextSelectionKey = "settings.page.allow_text_selection";

    private const string ThemePreferenceKey = "settings.display.theme";
    private const string DateFormatKey = "settings.display.date_format";
    private const string KeepScreenOnKey = "settings.display.keep_screen_on";
    private const string DarkModeInvertKey = "settings.display.dark_mode_invert";

    private PdfDisplayMode _savedDisplayMode = PdfDisplayMode.SinglePageContinuous;
    private PdfScrollOrientation _savedScrollOrientation = PdfScrollOrientation.Vertical;
    private FitPolicy _savedFitPolicy = FitPolicy.Width;
    private float _savedZoom = 1f;
    private bool _savedEnableZoom = true;
    private bool _savedEnableSwipe = true;
    private bool _savedEnableLink = true;

    private bool _pageFreeMoveEnabled = true;
    private double _pageMoveResistancePercent = 65d;
    private bool _zoomFollowEnabled = true;
    private int _pageNumberPositionIndex;
    private bool _allowTextSelection = true;

    private AppTheme _themePreference = AppTheme.Unspecified;
    private DateFormatPreference _dateFormatPreference = DateFormatPreference.YYMMDD;
    private bool _keepScreenOnEnabled;
    private bool _darkModeInvertEnabled;

    private SettingsSection _settingsSection = SettingsSection.Home;
    private bool _isUpdatingSettingsControls;

    private void LoadPersistedAppSettings()
    {
        _savedDisplayMode = ParseEnumOrDefault(
            Preferences.Get(DisplayModeKey, PdfDisplayMode.SinglePageContinuous.ToString()),
            PdfDisplayMode.SinglePageContinuous);
        _savedScrollOrientation = ParseEnumOrDefault(
            Preferences.Get(ScrollOrientationKey, PdfScrollOrientation.Vertical.ToString()),
            PdfScrollOrientation.Vertical);
        _savedFitPolicy = ParseEnumOrDefault(
            Preferences.Get(FitPolicyKey, FitPolicy.Width.ToString()),
            FitPolicy.Width);
        _savedZoom = (float)Math.Clamp(Preferences.Get(ZoomValueKey, 1d), EditorMinZoom, EditorMaxZoom);
        _savedEnableZoom = Preferences.Get(EnableZoomKey, true);
        _savedEnableSwipe = Preferences.Get(EnableSwipeKey, true);
        _savedEnableLink = Preferences.Get(EnableLinkKey, true);

        _pageFreeMoveEnabled = Preferences.Get(PageFreeMoveKey, true);
        _pageMoveResistancePercent = Math.Clamp(Preferences.Get(PageMoveResistanceKey, 65d), 0d, 100d);
        _zoomFollowEnabled = Preferences.Get(ZoomFollowKey, true);
        _pageNumberPositionIndex = Math.Clamp(Preferences.Get(PageNumberPositionKey, 0), 0, 1);
        _allowTextSelection = Preferences.Get(TextSelectionKey, true);

        _themePreference = ParseThemePreference(Preferences.Get(ThemePreferenceKey, "system"));
        _dateFormatPreference = ParseEnumOrDefault(
            Preferences.Get(DateFormatKey, DateFormatPreference.YYMMDD.ToString()),
            DateFormatPreference.YYMMDD);
        _keepScreenOnEnabled = Preferences.Get(KeepScreenOnKey, false);
        _darkModeInvertEnabled = Preferences.Get(DarkModeInvertKey, false);
    }

    private void ApplyGlobalSettings()
    {
        if (Application.Current != null)
        {
            Application.Current.UserAppTheme = _themePreference;
        }

        try
        {
            DeviceDisplay.Current.KeepScreenOn = _keepScreenOnEnabled;
        }
        catch
        {
        }
    }

    private void SavePersistedAppSettings()
    {
        CaptureViewerSettingsFromControls();

        Preferences.Set(DisplayModeKey, _savedDisplayMode.ToString());
        Preferences.Set(ScrollOrientationKey, _savedScrollOrientation.ToString());
        Preferences.Set(FitPolicyKey, _savedFitPolicy.ToString());
        Preferences.Set(ZoomValueKey, _savedZoom);
        Preferences.Set(EnableZoomKey, _savedEnableZoom);
        Preferences.Set(EnableSwipeKey, _savedEnableSwipe);
        Preferences.Set(EnableLinkKey, _savedEnableLink);

        Preferences.Set(PageFreeMoveKey, _pageFreeMoveEnabled);
        Preferences.Set(PageMoveResistanceKey, _pageMoveResistancePercent);
        Preferences.Set(ZoomFollowKey, _zoomFollowEnabled);
        Preferences.Set(PageNumberPositionKey, _pageNumberPositionIndex);
        Preferences.Set(TextSelectionKey, _allowTextSelection);

        Preferences.Set(ThemePreferenceKey, ThemePreferenceToString(_themePreference));
        Preferences.Set(DateFormatKey, _dateFormatPreference.ToString());
        Preferences.Set(KeepScreenOnKey, _keepScreenOnEnabled);
        Preferences.Set(DarkModeInvertKey, _darkModeInvertEnabled);
    }

    private void CaptureViewerSettingsFromControls()
    {
        if (DisplayModePicker.SelectedIndex >= 0)
            _savedDisplayMode = (PdfDisplayMode)DisplayModePicker.SelectedIndex;
        if (OrientationPicker.SelectedIndex >= 0)
            _savedScrollOrientation = (PdfScrollOrientation)OrientationPicker.SelectedIndex;
        if (FitPolicyPicker.SelectedIndex >= 0)
            _savedFitPolicy = (FitPolicy)FitPolicyPicker.SelectedIndex;

        _savedZoom = (float)Math.Clamp(ZoomSlider.Value, EditorMinZoom, EditorMaxZoom);
        _savedEnableZoom = EnableZoomSwitch.IsToggled;
        _savedEnableSwipe = EnableSwipeSwitch.IsToggled;
        _savedEnableLink = EnableLinkSwitch.IsToggled;
    }

    private void RefreshSettingsUiState()
    {
        _isUpdatingSettingsControls = true;
        try
        {
            PageModeValueLabel.Text = GetDisplayModeText(_savedDisplayMode);
            FitModeValueLabel.Text = GetFitPolicyText(_savedFitPolicy);
            PageNumberPositionValueLabel.Text = GetPageNumberPositionText();
            TextSelectionValueLabel.Text = _allowTextSelection ? "允许选字" : "仅阅读";
            PageMoveResistanceValueLabel.Text = $"{_pageMoveResistancePercent:0}%";
            DateFormatValueLabel.Text = GetDateFormatText(_dateFormatPreference);
            LanguageDateFormatValueLabel.Text = DateFormatValueLabel.Text;

            PageSettingsSummaryLabel.Text = $"{GetDisplayModeText(_savedDisplayMode)} · {GetOrientationText(_savedScrollOrientation)}";
            DisplaySettingsSummaryLabel.Text = GetThemeText(_themePreference);
            LanguageSettingsSummaryLabel.Text = GetLanguageSummary();

            PageFreeMoveSwitch.IsToggled = _pageFreeMoveEnabled;
            PageMoveResistanceSlider.Value = _pageMoveResistancePercent;
            PageMoveResistanceSlider.IsEnabled = _pageFreeMoveEnabled;
            ZoomFollowSwitch.IsToggled = _zoomFollowEnabled;

            KeepScreenOnSwitch.IsToggled = _keepScreenOnEnabled;
            DarkModeInvertSwitch.IsToggled = _darkModeInvertEnabled;

            ApplySegmentButtonStyles();
            SetSettingsSection(_settingsSection);
        }
        finally
        {
            _isUpdatingSettingsControls = false;
        }
    }

    private void ApplySegmentButtonStyles()
    {
        ApplySegmentButtonStyle(PageDirectionVerticalButton, _savedScrollOrientation == PdfScrollOrientation.Vertical);
        ApplySegmentButtonStyle(PageDirectionHorizontalButton, _savedScrollOrientation == PdfScrollOrientation.Horizontal);

        ApplySegmentButtonStyle(ThemeLightButton, _themePreference == AppTheme.Light);
        ApplySegmentButtonStyle(ThemeDarkButton, _themePreference == AppTheme.Dark);
        ApplySegmentButtonStyle(ThemeSystemButton, _themePreference == AppTheme.Unspecified);

        var isZh = LanguageManager.CurrentCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
        ApplySegmentButtonStyle(LanguageChineseButton, isZh);
        ApplySegmentButtonStyle(LanguageEnglishButton, !isZh);
    }

    private void ApplySegmentButtonStyle(Button button, bool isSelected)
    {
        button.BackgroundColor = isSelected
            ? Color.FromArgb("#1F86E9")
            : (IsDarkTheme ? Color.FromArgb("#2B3D57") : Colors.White);
        button.BorderColor = isSelected
            ? Color.FromArgb("#1F86E9")
            : (IsDarkTheme ? Color.FromArgb("#617A9C") : Color.FromArgb("#CDD7E6"));
        button.BorderWidth = 1;
        button.TextColor = isSelected
            ? Colors.White
            : (IsDarkTheme ? Color.FromArgb("#DDE7F7") : Color.FromArgb("#4B5566"));
    }

    private void SetSettingsSection(SettingsSection section)
    {
        _settingsSection = section;
        SettingsHomeView.IsVisible = section == SettingsSection.Home;
        PageSettingsView.IsVisible = section == SettingsSection.Page;
        DisplaySettingsView.IsVisible = section == SettingsSection.Display;
        LanguageSettingsView.IsVisible = section == SettingsSection.Language;

        SettingsBackButton.IsVisible = section != SettingsSection.Home;
        ResetSettingsButton.IsVisible = section == SettingsSection.Home;

        SettingsTitleLabel.Text = section switch
        {
            SettingsSection.Page => "页面设置",
            SettingsSection.Display => "显示",
            SettingsSection.Language => "语言与日期",
            _ => "设置"
        };
    }

    private void OnSettingsBackClicked(object? sender, EventArgs e)
    {
        SetSettingsSection(SettingsSection.Home);
    }

    private void OnOpenPageSettingsTapped(object? sender, TappedEventArgs e)
    {
        SetSettingsSection(SettingsSection.Page);
    }

    private void OnOpenDisplaySettingsTapped(object? sender, TappedEventArgs e)
    {
        SetSettingsSection(SettingsSection.Display);
    }

    private void OnOpenLanguageSettingsTapped(object? sender, TappedEventArgs e)
    {
        SetSettingsSection(SettingsSection.Language);
    }

    private void OnPageDirectionVerticalClicked(object? sender, EventArgs e)
    {
        OrientationPicker.SelectedIndex = (int)PdfScrollOrientation.Vertical;
        _savedScrollOrientation = PdfScrollOrientation.Vertical;
        if (IsEditorInitialized)
            PdfViewer.ScrollOrientation = PdfScrollOrientation.Vertical;
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnPageDirectionHorizontalClicked(object? sender, EventArgs e)
    {
        OrientationPicker.SelectedIndex = (int)PdfScrollOrientation.Horizontal;
        _savedScrollOrientation = PdfScrollOrientation.Horizontal;
        if (IsEditorInitialized)
            PdfViewer.ScrollOrientation = PdfScrollOrientation.Horizontal;
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnPageModeTapped(object? sender, TappedEventArgs e)
    {
        _savedDisplayMode = _savedDisplayMode == PdfDisplayMode.SinglePageContinuous
            ? PdfDisplayMode.SinglePage
            : PdfDisplayMode.SinglePageContinuous;
        DisplayModePicker.SelectedIndex = (int)_savedDisplayMode;
        if (IsEditorInitialized)
            PdfViewer.DisplayMode = _savedDisplayMode;
        UpdateTwoFingerNavigationPolicy();
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnFitModeTapped(object? sender, TappedEventArgs e)
    {
        _savedFitPolicy = _savedFitPolicy switch
        {
            FitPolicy.Width => FitPolicy.Both,
            FitPolicy.Both => FitPolicy.Height,
            _ => FitPolicy.Width
        };
        FitPolicyPicker.SelectedIndex = (int)_savedFitPolicy;
        if (IsEditorInitialized)
            PdfViewer.FitPolicy = _savedFitPolicy;
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnPageNumberPositionTapped(object? sender, TappedEventArgs e)
    {
        _pageNumberPositionIndex = (_pageNumberPositionIndex + 1) % 2;
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnTextSelectionTapped(object? sender, TappedEventArgs e)
    {
        _allowTextSelection = !_allowTextSelection;
        if (IsEditorInitialized)
            PdfViewer.EnableTapGestures = _allowTextSelection;
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnPageFreeMoveToggled(object? sender, ToggledEventArgs e)
    {
        if (_isUpdatingSettingsControls)
            return;

        _pageFreeMoveEnabled = e.Value;
        if (!_pageFreeMoveEnabled)
        {
            StopTwoFingerInertia();
        }

        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnPageMoveResistanceChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isUpdatingSettingsControls)
            return;

        _pageMoveResistancePercent = Math.Clamp(e.NewValue, 0d, 100d);
        SavePersistedAppSettings();
        PageMoveResistanceValueLabel.Text = $"{_pageMoveResistancePercent:0}%";
    }

    private void OnZoomFollowToggled(object? sender, ToggledEventArgs e)
    {
        if (_isUpdatingSettingsControls)
            return;

        _zoomFollowEnabled = e.Value;
        if (IsEditorInitialized)
            DrawingCanvas.ZoomAffectsStrokeWidth = _zoomFollowEnabled;
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnThemeLightClicked(object? sender, EventArgs e)
    {
        _themePreference = AppTheme.Light;
        ApplyGlobalSettings();
        ApplyDarkModeInversion();
        RefreshEditorTabsVisual();
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnThemeDarkClicked(object? sender, EventArgs e)
    {
        _themePreference = AppTheme.Dark;
        ApplyGlobalSettings();
        ApplyDarkModeInversion();
        RefreshEditorTabsVisual();
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnThemeSystemClicked(object? sender, EventArgs e)
    {
        _themePreference = AppTheme.Unspecified;
        ApplyGlobalSettings();
        ApplyDarkModeInversion();
        RefreshEditorTabsVisual();
        SavePersistedAppSettings();
        RefreshSettingsUiState();
    }

    private void OnDateFormatTapped(object? sender, TappedEventArgs e)
    {
        _dateFormatPreference = _dateFormatPreference switch
        {
            DateFormatPreference.YYMMDD => DateFormatPreference.YYYYMMDD,
            DateFormatPreference.YYYYMMDD => DateFormatPreference.DDMMYY,
            DateFormatPreference.DDMMYY => DateFormatPreference.MMDDYY,
            _ => DateFormatPreference.YYMMDD
        };
        SavePersistedAppSettings();
        RefreshSettingsUiState();
        RefreshHomeFeed();
    }

    private void OnKeepScreenOnToggled(object? sender, ToggledEventArgs e)
    {
        if (_isUpdatingSettingsControls)
            return;

        _keepScreenOnEnabled = e.Value;
        ApplyGlobalSettings();
        SavePersistedAppSettings();
    }

    private void OnDarkInvertToggled(object? sender, ToggledEventArgs e)
    {
        if (_isUpdatingSettingsControls)
            return;

        _darkModeInvertEnabled = e.Value;
        ApplyDarkModeInversion();
        SavePersistedAppSettings();
    }

    private void OnLanguageChineseClicked(object? sender, EventArgs e)
    {
        if (LanguageManager.CurrentCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
            return;

        SavePersistedAppSettings();
        LanguageManager.SetCulture(new CultureInfo("zh-CN"));
    }

    private void OnLanguageEnglishClicked(object? sender, EventArgs e)
    {
        if (!LanguageManager.CurrentCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
            return;

        SavePersistedAppSettings();
        LanguageManager.SetCulture(new CultureInfo("en-US"));
    }

    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        RefreshSettingsUiState();
        RefreshEditorTabsVisual();
        RefreshHomeFeed();
        ApplyDarkModeInversion();
    }

    private void ApplyDarkModeInversion()
    {
        if (!IsEditorInitialized)
            return;

        if (_darkModeInvertEnabled && IsDarkTheme)
        {
            PdfViewer.BackgroundColor = Colors.White;
        }
        else
        {
            PdfViewer.BackgroundColor = null;
        }
    }

    private string FormatDateForHome(DateTime dateTime)
    {
        var local = dateTime.ToLocalTime();
        return _dateFormatPreference switch
        {
            DateFormatPreference.YYYYMMDD => local.ToString("yyyy/MM/dd"),
            DateFormatPreference.DDMMYY => local.ToString("dd/MM/yy"),
            DateFormatPreference.MMDDYY => local.ToString("MM/dd/yy"),
            _ => local.ToString("yy/MM/dd")
        };
    }

    private string FormatDateTimeForRecent(DateTime dateTime)
    {
        var local = dateTime.ToLocalTime();
        return _dateFormatPreference switch
        {
            DateFormatPreference.YYYYMMDD => local.ToString("yyyy/MM/dd HH:mm"),
            DateFormatPreference.DDMMYY => local.ToString("dd/MM/yy HH:mm"),
            DateFormatPreference.MMDDYY => local.ToString("MM/dd/yy HH:mm"),
            _ => local.ToString("yy/MM/dd HH:mm")
        };
    }

    private static T ParseEnumOrDefault<T>(string raw, T fallback) where T : struct, Enum
    {
        return Enum.TryParse<T>(raw, true, out var parsed) ? parsed : fallback;
    }

    private static AppTheme ParseThemePreference(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
    }

    private static string ThemePreferenceToString(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => "light",
            AppTheme.Dark => "dark",
            _ => "system"
        };
    }

    private string GetDisplayModeText(PdfDisplayMode displayMode)
    {
        return displayMode == PdfDisplayMode.SinglePage ? "单页翻页" : "连续滚动";
    }

    private static string GetFitPolicyText(FitPolicy fitPolicy)
    {
        return fitPolicy switch
        {
            FitPolicy.Height => "高度适配",
            FitPolicy.Both => "整页适配",
            _ => "自动加页"
        };
    }

    private static string GetOrientationText(PdfScrollOrientation orientation)
    {
        return orientation == PdfScrollOrientation.Horizontal ? "横向" : "竖向";
    }

    private string GetThemeText(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => "浅色",
            AppTheme.Dark => "深色",
            _ => "跟随系统"
        };
    }

    private string GetDateFormatText(DateFormatPreference preference)
    {
        return preference switch
        {
            DateFormatPreference.YYYYMMDD => "YYYY/MM/DD",
            DateFormatPreference.DDMMYY => "DD/MM/YY",
            DateFormatPreference.MMDDYY => "MM/DD/YY",
            _ => "YY/MM/DD"
        };
    }

    private string GetPageNumberPositionText()
    {
        return _pageNumberPositionIndex == 0 ? "右侧/底部" : "左侧/底部";
    }

    private static string GetLanguageSummary()
    {
        return LanguageManager.CurrentCulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? "简体中文"
            : "English";
    }

    private void ResetAppSettingValuesToDefault()
    {
        _savedDisplayMode = PdfDisplayMode.SinglePageContinuous;
        _savedScrollOrientation = PdfScrollOrientation.Vertical;
        _savedFitPolicy = FitPolicy.Width;
        _savedZoom = 1f;
        _savedEnableZoom = true;
        _savedEnableSwipe = true;
        _savedEnableLink = true;

        _pageFreeMoveEnabled = true;
        _pageMoveResistancePercent = 65d;
        _zoomFollowEnabled = true;
        _pageNumberPositionIndex = 0;
        _allowTextSelection = true;

        _themePreference = AppTheme.Unspecified;
        _dateFormatPreference = DateFormatPreference.YYMMDD;
        _keepScreenOnEnabled = false;
        _darkModeInvertEnabled = false;
    }
}
