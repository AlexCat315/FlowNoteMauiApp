using System.Globalization;
using FlowNoteMauiApp.Resources;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private static string T(string key, string fallback)
    {
        return AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? fallback;
    }

    private static string TF(string key, string fallback, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, T(key, fallback), args);
    }

    private void ApplyLocalizedUiText()
    {
        StatusLabel.Text = T("UiReady", "Ready");
        UrlEntry.Placeholder = T("UiPdfUrl", "PDF URL");
        HomeSearchEntry.Placeholder = T("UiKeyword", "Keyword");

        FilterAllButton.Text = T("HomeFilterAll", "All");
        FilterPdfButton.Text = T("HomeFilterPdf", "PDF");
        FilterNoteButton.Text = T("HomeFilterNote", "Notes");
        FilterFolderButton.Text = T("HomeFilterFolder", "Folders");
        HomeSortButton.Text = T("HomeSort", "Sort");

        FindInDrawer<Label>("DrawerAuthLabel").Text = T("DrawerAuth", "Sign in / Register");
        FindInDrawer<Label>("DrawerSyncHintLabel").Text = T("DrawerSyncHint", "Sync your learning progress");
        FindInDrawer<Button>("DrawerAllDocsButton").Text = T("DrawerAllDocs", "All Documents");
        FindInDrawer<Button>("DrawerRecentButton").Text = T("DrawerRecent", "Recent");
        FindInDrawer<Button>("DrawerFavoriteButton").Text = T("DrawerFavorites", "Favorites");
        FindInDrawer<Button>("DrawerTrashButton").Text = T("DrawerTrash", "Trash");
        FindInDrawer<Label>("DrawerTagsTitleLabel").Text = T("DrawerTags", "My Tags");
        FindInDrawer<Button>("DrawerEditTagsButton").Text = T("DrawerEditTags", "Edit Tags");
        FindInDrawer<Button>("DrawerCreateTagButton").Text = T("DrawerCreateTag", "Create Tag");
        FindInDrawer<Label>("DrawerAppsTitleLabel").Text = T("DrawerApps", "My Apps");
        FindInDrawer<Button>("DrawerHelpButton").Text = T("DrawerHelp", "Help Center");
        FindInDrawer<Button>("DrawerAboutButton").Text = T("DrawerAbout", "About");
        FindInDrawer<Button>("DrawerDiscountButton").Text = T("DrawerDiscount", "Get Discount");
        FindInDrawer<Label>("DrawerPromoLabel").Text = T("DrawerPromo", "Register for free trial\nUnlock full note workflow");

        FindInEditor<Label>("InputModeTitleLabel").Text = T("InputModeTitle", "Input Mode");
        FindInEditor<Label>("InputModeHintLabel").Text = T("InputModeHint", "In finger/capacitive mode: one finger writes, two fingers move/turn pages");
        FindInEditor<Button>("InputModePenButton").Text = T("InputModePen", "Stylus");
        FindInEditor<Button>("InputModeFingerButton").Text = T("InputModeFinger", "Finger/Capacitive (2-finger move)");
        FindInEditor<Button>("InputModeReadButton").Text = T("InputModeRead", "Read Mode");
        FindInEditor<Label>("DrawingPenWidthLabel").Text = T("DrawingPenWidth", "Stroke");
        FindInEditor<Label>("ToolColorTitleLabel").Text = T("Color", "Color");
        FindInEditor<Label>("ThumbnailTitleLabel").Text = T("Thumbnail", "Thumbnails");
        FindInEditor<Label>("ThumbnailHintLabel").Text = T("ThumbnailHint", "Tap an item to jump to page");
        FindInEditor<Label>("LayerTitleLabel").Text = T("LayerTitle", "Layers");

        SettingsHomeHintLabel.Text = T("SettingsHomeHint", "Page, display and language settings");
        FindInSettings<Label>("SettingsPageTitleLabel").Text = T("SettingsPageTitle", "Page");
        FindInSettings<Label>("SettingsDisplayTitleLabel").Text = T("SettingsDisplayTitle", "Display");
        FindInSettings<Label>("SettingsLanguageTitleLabel").Text = T("SettingsLanguageTitle", "Language & Date");
        FindInSettings<Label>("PageDirectionTitleLabel").Text = T("PageDirection", "Page Direction");
        PageDirectionVerticalButton.Text = T("DirectionVertical", "Vertical");
        PageDirectionHorizontalButton.Text = T("DirectionHorizontal", "Horizontal");
        FindInSettings<Label>("PageModeTitleLabel").Text = T("PageTurnMode", "Page Turn Mode");
        FindInSettings<Label>("FitModeTitleLabel").Text = T("FitMode", "Fit Mode");
        FindInSettings<Label>("PageNumberPositionTitleLabel").Text = T("PageNumberPosition", "Page Number Position");
        FindInSettings<Label>("TextSelectionTitleLabel").Text = T("TextSelection", "Text Selection");
        FindInSettings<Label>("ZoomFollowTitleLabel").Text = T("ZoomFollow", "Zoom Follow");
        FindInSettings<Label>("ZoomFollowHintLabel").Text = T("ZoomFollowHint", "When enabled, stroke width scales with zoom; otherwise remains screen-pixel width.");
        FindInSettings<Label>("DisplayModeTitleLabel").Text = T("DisplayModeTitle", "Display Mode");
        ThemeLightButton.Text = T("ThemeLight", "Light");
        ThemeDarkButton.Text = T("ThemeDark", "Dark");
        ThemeSystemButton.Text = T("ThemeSystem", "System");
        FindInSettings<Label>("DateFormatTitleLabel").Text = T("DateFormatTitle", "Date Format");
        FindInSettings<Label>("KeepScreenOnTitleLabel").Text = T("KeepScreenOn", "Keep Screen On");
        FindInSettings<Label>("DarkInvertTitleLabel").Text = T("DarkInvert", "Invert Colors in Dark Mode");
        FindInSettings<Label>("LanguageTitleLabel").Text = T("Language", "Language");
        LanguageChineseButton.Text = T("LangZhHans", "Simplified Chinese");
        LanguageEnglishButton.Text = T("LangEnUs", "English");
        FindInSettings<Label>("LanguageDateFormatTitleLabel").Text = T("DateFormatTitle", "Date Format");
        FindInSettings<Label>("LanguageDateFormatHintLabel").Text = T("DateFormatInDisplay", "Set in Display");
        ResetSettingsButton.Text = T("ResetDefaults", "Reset to Defaults");

        LoadUrlButton.Text = T("LoadAction", "Load");
        LoadSampleButton.Text = T("SampleAction", "Sample");
        LocalFileButton.Text = T("LocalOpenAction", "Open Local");
        FindInSettings<Button>("WorkspaceRootButton").Text = T("WorkspaceRoot", "Root");
        FindInSettings<Button>("WorkspaceUpButton").Text = T("WorkspaceUp", "Up");
        FindInSettings<Button>("WorkspaceOpenFolderButton").Text = T("WorkspaceOpen", "Open");
        WorkspaceNewFolderEntry.Placeholder = T("WorkspaceNewFolder", "New Folder");
        FindInSettings<Button>("WorkspaceCreateFolderButton").Text = T("CreateAction", "Create");
        FindInSettings<Button>("WorkspaceRefreshButton").Text = T("RefreshAction", "Refresh");
        SearchEntry.Placeholder = T("UiKeyword", "Keyword");
        DisplayModePicker.Title = T("PickerMode", "Mode");
        OrientationPicker.Title = T("PickerDirection", "Direction");
        FitPolicyPicker.Title = T("PickerFit", "Fit");
        FindInSettings<Label>("DebugZoomLabel").Text = T("Zoom", "Zoom");
        FindInSettings<Label>("DebugSwipeLabel").Text = T("PageTurn", "Page Turn");
        FindInSettings<Label>("DebugLinkLabel").Text = T("Link", "Link");
        FindInSettings<Label>("DebugFingerDrawLabel").Text = T("FingerDraw", "Finger Draw");
        EventInfoLabel.Text = T("UiReady", "Ready");
    }
}
