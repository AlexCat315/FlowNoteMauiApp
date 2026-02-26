namespace FlowNoteMauiApp;

public partial class MainPage
{
    private readonly Dictionary<string, object> _namedElementCache = new();

    private T FindInView<T>(Element root, string name) where T : class
    {
        var key = $"{root.GetType().Name}:{name}";
        if (_namedElementCache.TryGetValue(key, out var cached))
            return (T)cached;

        var element = root.FindByName<T>(name)
            ?? throw new InvalidOperationException($"Element '{name}' was not found in '{root.GetType().Name}'.");

        _namedElementCache[key] = element;
        return element;
    }

    private T FindInHome<T>(string name) where T : class => FindInView<T>(HomePanelView, name);
    private T FindInEditor<T>(string name) where T : class => FindInView<T>(EditorChromeView, name);
    private T FindInDrawer<T>(string name) where T : class => FindInView<T>(DrawerOverlayView, name);
    private T FindInSettings<T>(string name) where T : class => FindInView<T>(SettingsOverlayView, name);
    private T FindInStatus<T>(string name) where T : class => FindInView<T>(StatusToastView, name);

    private Grid HomePanel => FindInHome<Grid>(nameof(HomePanel));
    private Button FilterAllButton => FindInHome<Button>(nameof(FilterAllButton));
    private BoxView FilterAllIndicator => FindInHome<BoxView>(nameof(FilterAllIndicator));
    private Button FilterPdfButton => FindInHome<Button>(nameof(FilterPdfButton));
    private BoxView FilterPdfIndicator => FindInHome<BoxView>(nameof(FilterPdfIndicator));
    private Button FilterNoteButton => FindInHome<Button>(nameof(FilterNoteButton));
    private BoxView FilterNoteIndicator => FindInHome<BoxView>(nameof(FilterNoteIndicator));
    private Button FilterFolderButton => FindInHome<Button>(nameof(FilterFolderButton));
    private BoxView FilterFolderIndicator => FindInHome<BoxView>(nameof(FilterFolderIndicator));
    private Button HomeSortButton => FindInHome<Button>(nameof(HomeSortButton));
    private FlexLayout HomeNotesList => FindInHome<FlexLayout>(nameof(HomeNotesList));
    private ImageButton QuickPenButton => FindInHome<ImageButton>(nameof(QuickPenButton));
    private Entry HomeSearchEntry => FindInHome<Entry>(nameof(HomeSearchEntry));
    private Label HomeCountLabel => FindInHome<Label>(nameof(HomeCountLabel));
    private Entry HomeUrlEntry => FindInHome<Entry>(nameof(HomeUrlEntry));

    private Border TopBarPanel => FindInEditor<Border>(nameof(TopBarPanel));
    private HorizontalStackLayout EditorTabsHost => FindInEditor<HorizontalStackLayout>(nameof(EditorTabsHost));
    private ImageButton TopModePenButton => FindInEditor<ImageButton>(nameof(TopModePenButton));
    private ImageButton UndoButton => FindInEditor<ImageButton>(nameof(UndoButton));
    private ImageButton RedoButton => FindInEditor<ImageButton>(nameof(RedoButton));
    private ImageButton PenModeButton => FindInEditor<ImageButton>(nameof(PenModeButton));
    private ImageButton HighlighterButton => FindInEditor<ImageButton>(nameof(HighlighterButton));
    private ImageButton PencilButton => FindInEditor<ImageButton>(nameof(PencilButton));
    private ImageButton MarkerButton => FindInEditor<ImageButton>(nameof(MarkerButton));
    private ImageButton EraserButton => FindInEditor<ImageButton>(nameof(EraserButton));
    private ImageButton ClearButton2 => FindInEditor<ImageButton>(nameof(ClearButton2));
    private ImageButton TextToolButton => FindInEditor<ImageButton>(nameof(TextToolButton));
    private ImageButton ImageToolButton => FindInEditor<ImageButton>(nameof(ImageToolButton));
    private ImageButton ShapeToolButton => FindInEditor<ImageButton>(nameof(ShapeToolButton));
    private ImageButton PrevPageButton => FindInEditor<ImageButton>(nameof(PrevPageButton));
    private Label PageInfoLabel => FindInEditor<Label>(nameof(PageInfoLabel));
    private ImageButton NextPageButton => FindInEditor<ImageButton>(nameof(NextPageButton));
    private Button ColorBlack => FindInEditor<Button>(nameof(ColorBlack));
    private Button ColorBlue => FindInEditor<Button>(nameof(ColorBlue));
    private Button ColorRed => FindInEditor<Button>(nameof(ColorRed));
    private Button ColorGreen => FindInEditor<Button>(nameof(ColorGreen));
    private Button ColorOrange => FindInEditor<Button>(nameof(ColorOrange));
    private Button ColorWhite => FindInEditor<Button>(nameof(ColorWhite));
    private Border InputModePanel => FindInEditor<Border>(nameof(InputModePanel));
    private Label InputModePenCheck => FindInEditor<Label>(nameof(InputModePenCheck));
    private Label InputModeFingerCheck => FindInEditor<Label>(nameof(InputModeFingerCheck));
    private Label InputModeReadCheck => FindInEditor<Label>(nameof(InputModeReadCheck));
    private Button InputModePenButton => FindInEditor<Button>(nameof(InputModePenButton));
    private Button InputModeFingerButton => FindInEditor<Button>(nameof(InputModeFingerButton));
    private Button InputModeReadButton => FindInEditor<Button>(nameof(InputModeReadButton));
    private Border DrawingToolbarPanel => FindInEditor<Border>(nameof(DrawingToolbarPanel));
    private Label ToolSettingsTitleLabel => FindInEditor<Label>(nameof(ToolSettingsTitleLabel));
    private VerticalStackLayout EraserModePanel => FindInEditor<VerticalStackLayout>(nameof(EraserModePanel));
    private Button PixelEraserModeButton => FindInEditor<Button>(nameof(PixelEraserModeButton));
    private Button StrokeEraserModeButton => FindInEditor<Button>(nameof(StrokeEraserModeButton));
    private Button LassoEraserModeButton => FindInEditor<Button>(nameof(LassoEraserModeButton));
    private VerticalStackLayout ToolColorPanel => FindInEditor<VerticalStackLayout>(nameof(ToolColorPanel));
    private Slider StrokeWidthSlider => FindInEditor<Slider>(nameof(StrokeWidthSlider));
    private Label DrawingPenWidthLabel => FindInEditor<Label>(nameof(DrawingPenWidthLabel));
    private Label StrokeWidthLabel => FindInEditor<Label>(nameof(StrokeWidthLabel));
    private Border ThumbnailPanel => FindInEditor<Border>(nameof(ThumbnailPanel));
    private VerticalStackLayout ThumbnailList => FindInEditor<VerticalStackLayout>(nameof(ThumbnailList));
    private Border LayerPanel => FindInEditor<Border>(nameof(LayerPanel));
    private VerticalStackLayout LayerList => FindInEditor<VerticalStackLayout>(nameof(LayerList));

    private Grid HomeDrawerOverlay => FindInDrawer<Grid>(nameof(HomeDrawerOverlay));

    private Grid SettingsOverlay => FindInSettings<Grid>(nameof(SettingsOverlay));
    private Border SettingsPanel => FindInSettings<Border>(nameof(SettingsPanel));
    private ImageButton SettingsBackButton => FindInSettings<ImageButton>(nameof(SettingsBackButton));
    private Label SettingsTitleLabel => FindInSettings<Label>(nameof(SettingsTitleLabel));
    private VerticalStackLayout SettingsHomeView => FindInSettings<VerticalStackLayout>(nameof(SettingsHomeView));
    private Label SettingsHomeHintLabel => FindInSettings<Label>(nameof(SettingsHomeHintLabel));
    private Label PageSettingsSummaryLabel => FindInSettings<Label>(nameof(PageSettingsSummaryLabel));
    private Label DisplaySettingsSummaryLabel => FindInSettings<Label>(nameof(DisplaySettingsSummaryLabel));
    private Label LanguageSettingsSummaryLabel => FindInSettings<Label>(nameof(LanguageSettingsSummaryLabel));
    private VerticalStackLayout PageSettingsView => FindInSettings<VerticalStackLayout>(nameof(PageSettingsView));
    private Button PageDirectionVerticalButton => FindInSettings<Button>(nameof(PageDirectionVerticalButton));
    private Button PageDirectionHorizontalButton => FindInSettings<Button>(nameof(PageDirectionHorizontalButton));
    private Label PageModeValueLabel => FindInSettings<Label>(nameof(PageModeValueLabel));
    private Label FitModeValueLabel => FindInSettings<Label>(nameof(FitModeValueLabel));
    private Label PageNumberPositionValueLabel => FindInSettings<Label>(nameof(PageNumberPositionValueLabel));
    private Label TextSelectionValueLabel => FindInSettings<Label>(nameof(TextSelectionValueLabel));
    private Switch ZoomFollowSwitch => FindInSettings<Switch>(nameof(ZoomFollowSwitch));
    private VerticalStackLayout DisplaySettingsView => FindInSettings<VerticalStackLayout>(nameof(DisplaySettingsView));
    private Button ThemeLightButton => FindInSettings<Button>(nameof(ThemeLightButton));
    private Button ThemeDarkButton => FindInSettings<Button>(nameof(ThemeDarkButton));
    private Button ThemeSystemButton => FindInSettings<Button>(nameof(ThemeSystemButton));
    private Label DateFormatValueLabel => FindInSettings<Label>(nameof(DateFormatValueLabel));
    private Switch KeepScreenOnSwitch => FindInSettings<Switch>(nameof(KeepScreenOnSwitch));
    private Switch DarkModeInvertSwitch => FindInSettings<Switch>(nameof(DarkModeInvertSwitch));
    private VerticalStackLayout LanguageSettingsView => FindInSettings<VerticalStackLayout>(nameof(LanguageSettingsView));
    private Button LanguageChineseButton => FindInSettings<Button>(nameof(LanguageChineseButton));
    private Button LanguageEnglishButton => FindInSettings<Button>(nameof(LanguageEnglishButton));
    private Label LanguageDateFormatValueLabel => FindInSettings<Label>(nameof(LanguageDateFormatValueLabel));
    private Button ResetSettingsButton => FindInSettings<Button>(nameof(ResetSettingsButton));
    private Entry UrlEntry => FindInSettings<Entry>(nameof(UrlEntry));
    private Button LoadUrlButton => FindInSettings<Button>(nameof(LoadUrlButton));
    private Button LoadSampleButton => FindInSettings<Button>(nameof(LoadSampleButton));
    private Button LocalFileButton => FindInSettings<Button>(nameof(LocalFileButton));
    private Entry WorkspaceFolderEntry => FindInSettings<Entry>(nameof(WorkspaceFolderEntry));
    private Entry WorkspaceNewFolderEntry => FindInSettings<Entry>(nameof(WorkspaceNewFolderEntry));
    private VerticalStackLayout RecentNotesList => FindInSettings<VerticalStackLayout>(nameof(RecentNotesList));
    private VerticalStackLayout WorkspaceFolderList => FindInSettings<VerticalStackLayout>(nameof(WorkspaceFolderList));
    private VerticalStackLayout WorkspaceNoteList => FindInSettings<VerticalStackLayout>(nameof(WorkspaceNoteList));
    private Entry SearchEntry => FindInSettings<Entry>(nameof(SearchEntry));
    private ImageButton SearchButton => FindInSettings<ImageButton>(nameof(SearchButton));
    private ImageButton SearchPrevButton => FindInSettings<ImageButton>(nameof(SearchPrevButton));
    private ImageButton SearchNextButton => FindInSettings<ImageButton>(nameof(SearchNextButton));
    private Label SearchStatusLabel => FindInSettings<Label>(nameof(SearchStatusLabel));
    private Picker DisplayModePicker => FindInSettings<Picker>(nameof(DisplayModePicker));
    private Picker OrientationPicker => FindInSettings<Picker>(nameof(OrientationPicker));
    private Picker FitPolicyPicker => FindInSettings<Picker>(nameof(FitPolicyPicker));
    private Slider ZoomSlider => FindInSettings<Slider>(nameof(ZoomSlider));
    private Label ZoomValueLabel => FindInSettings<Label>(nameof(ZoomValueLabel));
    private Switch EnableZoomSwitch => FindInSettings<Switch>(nameof(EnableZoomSwitch));
    private Switch EnableSwipeSwitch => FindInSettings<Switch>(nameof(EnableSwipeSwitch));
    private Switch EnableLinkSwitch => FindInSettings<Switch>(nameof(EnableLinkSwitch));
    private Switch EnableFingerDrawSwitch => FindInSettings<Switch>(nameof(EnableFingerDrawSwitch));
    private Label EventInfoLabel => FindInSettings<Label>(nameof(EventInfoLabel));

    private Border StatusToast => FindInStatus<Border>(nameof(StatusToast));
    private Label StatusLabel => FindInStatus<Label>(nameof(StatusLabel));
}
