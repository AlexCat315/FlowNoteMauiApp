namespace FlowNoteMauiApp;

public partial class MainPage
{
    private bool _homePanelEventsWired;
    private bool _editorChromeEventsWired;
    private bool _drawerEventsWired;
    private bool _settingsEventsWired;

    private void WireComposedViewEvents()
    {
        WireHomePanelEvents();
        WireEditorChromeEvents();
        WireDrawerEvents();
        WireSettingsEvents();
        WireMicroInteractions();
    }

    private void WireHomePanelEvents()
    {
        if (_homePanelEventsWired)
            return;

        _homePanelEventsWired = true;

        FindInHome<ImageButton>("HomeMenuButton").Clicked += OnMenuClicked;
        FindInHome<ImageButton>("HomeDrawerRecentButton").Clicked += OnDrawerRecentClicked;
        FindInHome<ImageButton>("HomeDrawerAllDocsButton").Clicked += OnDrawerAllDocsClicked;
        FindInHome<ImageButton>("HomeDrawerFavoriteButton").Clicked += OnDrawerFavoriteClicked;
        FindInHome<ImageButton>("HomeRefreshButton").Clicked += OnHomeRefreshClicked;
        FindInHome<ImageButton>("HomeOpenSettingsButton").Clicked += OnOpenSettingsClicked;
        HomeSortMenuButton.Clicked += OnHomeSortClicked;
        FilterAllButton.Clicked += OnHomeFilterAllClicked;
        FilterPdfButton.Clicked += OnHomeFilterPdfClicked;
        FilterNoteButton.Clicked += OnHomeFilterNoteClicked;
        FilterFolderButton.Clicked += OnHomeFilterFolderClicked;
        HomeSortTimeAscButton.Clicked += OnHomeSortTimeAscClicked;
        HomeSortTimeDescButton.Clicked += OnHomeSortTimeDescClicked;
        HomeSortNameAscButton.Clicked += OnHomeSortNameAscClicked;
        HomeSortNameDescButton.Clicked += OnHomeSortNameDescClicked;
        QuickPenButton.Clicked += OnHomeQuickPenClicked;
        FindInHome<ImageButton>("HomeImportButton").Clicked += OnHomeImportLocalClicked;
        HomeSearchEntry.TextChanged += OnHomeSearchTextChanged;
        HomePanelView.SizeChanged += OnHomeLayoutChanged;
        HomeSortMenuButton.SizeChanged += OnHomeLayoutChanged;
    }

    private void WireEditorChromeEvents()
    {
        if (_editorChromeEventsWired)
            return;

        _editorChromeEventsWired = true;

        FindInEditor<ImageButton>("TopHomeButton").Clicked += OnHomeClicked;
        FindInEditor<ImageButton>("TopImportButton").Clicked += OnHomeImportLocalClicked;
        FindInEditor<ImageButton>("TopSettingsButton").Clicked += OnOpenSettingsClicked;
        FindInEditor<ImageButton>("TopSearchButton").Clicked += OnEditorSearchClicked;
        FindInEditor<ImageButton>("TopModePenButton").Clicked += OnTopModeToggleClicked;
        FindInEditor<ImageButton>("TopThumbnailButton").Clicked += OnThumbnailToggleClicked;

        UndoButton.Clicked += OnUndoClicked;
        RedoButton.Clicked += OnRedoClicked;
        PenModeButton.Clicked += OnPenModeClicked;
        HighlighterButton.Clicked += OnHighlighterClicked;
        PencilButton.Clicked += OnPencilClicked;
        MarkerButton.Clicked += OnMarkerClicked;
        EraserButton.Clicked += OnEraserClicked;
        ClearButton2.Clicked += OnClearClicked;
        FindInEditor<ImageButton>("TopInlineLayerButton").Clicked += OnLayerToggleClicked;
        TextToolButton.Clicked += OnTextToolClicked;
        ImageToolButton.Clicked += OnImageToolClicked;
        ShapeToolButton.Clicked += OnShapeToolClicked;
        PrevPageButton.Clicked += OnPrevPageClicked;
        NextPageButton.Clicked += OnNextPageClicked;

        ColorBlack.Clicked += OnColorBlackClicked;
        ColorBlue.Clicked += OnColorBlueClicked;
        ColorRed.Clicked += OnColorRedClicked;
        ColorGreen.Clicked += OnColorGreenClicked;
        ColorOrange.Clicked += OnColorOrangeClicked;
        ColorWhite.Clicked += OnColorWhiteClicked;

        InputModePenButton.Clicked += OnInputModePenClicked;
        InputModeFingerButton.Clicked += OnInputModeFingerClicked;
        InputModeReadButton.Clicked += OnInputModeReadClicked;

        StrokeWidthSlider.ValueChanged += OnStrokeWidthChanged;
        PressureSensitivitySlider.ValueChanged += OnPressureSensitivityChanged;
        FindInEditor<ImageButton>("DrawingToolbarCloseButton").Clicked += OnDrawingToolbarCloseClicked;
        PixelEraserModeButton.Clicked += OnPixelEraserModeClicked;
        StrokeEraserModeButton.Clicked += OnStrokeEraserModeClicked;
        LassoEraserModeButton.Clicked += OnLassoEraserModeClicked;
        FindInEditor<ImageButton>("AddLayerButton").Clicked += OnAddLayerClicked;
        FindInEditor<ImageButton>("DeleteLayerButton").Clicked += OnDeleteLayerClicked;
        ThumbnailCloseButton.Clicked += OnThumbnailCloseClicked;
        ThumbnailOverlaySwitch.Toggled += OnThumbnailOverlayToggled;

        EditorChromeView.SizeChanged += OnEditorChromeLayoutChanged;
        TopBarPanel.SizeChanged += OnEditorChromeLayoutChanged;
        TopTabsScrollView.SizeChanged += OnEditorChromeLayoutChanged;
        TopToolsScrollView.SizeChanged += OnEditorChromeLayoutChanged;
        TopTabsScrollView.Scrolled += OnTopBarScrolled;
        TopToolsScrollView.Scrolled += OnTopBarScrolled;
        WireInkToolReorderGestures();
    }

    private void WireDrawerEvents()
    {
        if (_drawerEventsWired)
            return;

        _drawerEventsWired = true;

        FindInDrawer<ImageButton>("DrawerHeaderCloseButton").Clicked += OnMenuClicked;
        FindInDrawer<Button>("DrawerAllDocsButton").Clicked += OnDrawerAllDocsClicked;
        FindInDrawer<Button>("DrawerRecentButton").Clicked += OnDrawerRecentClicked;
        FindInDrawer<Button>("DrawerFavoriteButton").Clicked += OnDrawerFavoriteClicked;
        FindInDrawer<Button>("DrawerTrashButton").Clicked += OnDrawerTrashClicked;
        FindInDrawer<Button>("DrawerEditTagsButton").Clicked += OnDrawerEditTagsClicked;
        FindInDrawer<Button>("DrawerCreateTagButton").Clicked += OnDrawerCreateTagClicked;
        FindInDrawer<Button>("DrawerHelpButton").Clicked += OnDrawerHelpClicked;
        FindInDrawer<Button>("DrawerAboutButton").Clicked += OnDrawerAboutClicked;
        FindInDrawer<Button>("DrawerDiscountButton").Clicked += OnDrawerDiscountClicked;
        FindInDrawer<TapGestureRecognizer>("DrawerBackdropTapGesture").Tapped += OnDrawerBackdropTapped;
    }

    private void WireSettingsEvents()
    {
        if (_settingsEventsWired)
            return;

        _settingsEventsWired = true;

        FindInSettings<TapGestureRecognizer>("SettingsOverlayTapGesture").Tapped += OnSettingsOverlayTapped;
        SettingsBackButton.Clicked += OnSettingsBackClicked;
        FindInSettings<ImageButton>("SettingsCloseButton").Clicked += OnSettingsCloseClicked;

        FindInSettings<TapGestureRecognizer>("OpenPageSettingsTapGesture").Tapped += OnOpenPageSettingsTapped;
        FindInSettings<TapGestureRecognizer>("OpenDisplaySettingsTapGesture").Tapped += OnOpenDisplaySettingsTapped;
        FindInSettings<TapGestureRecognizer>("OpenLanguageSettingsTapGesture").Tapped += OnOpenLanguageSettingsTapped;

        PageDirectionVerticalButton.Clicked += OnPageDirectionVerticalClicked;
        PageDirectionHorizontalButton.Clicked += OnPageDirectionHorizontalClicked;
        FindInSettings<TapGestureRecognizer>("PageModeTapGesture").Tapped += OnPageModeTapped;
        FindInSettings<TapGestureRecognizer>("FitModeTapGesture").Tapped += OnFitModeTapped;
        FindInSettings<TapGestureRecognizer>("PageNumberPositionTapGesture").Tapped += OnPageNumberPositionTapped;
        FindInSettings<TapGestureRecognizer>("TextSelectionTapGesture").Tapped += OnTextSelectionTapped;

        ZoomFollowSwitch.Toggled += OnZoomFollowToggled;
        AllowSideWritingSwitch.Toggled += OnAllowSideWritingToggled;

        ThemeLightButton.Clicked += OnThemeLightClicked;
        ThemeDarkButton.Clicked += OnThemeDarkClicked;
        ThemeSystemButton.Clicked += OnThemeSystemClicked;
        FindInSettings<TapGestureRecognizer>("DateFormatTapGesture").Tapped += OnDateFormatTapped;
        KeepScreenOnSwitch.Toggled += OnKeepScreenOnToggled;
        DarkModeInvertSwitch.Toggled += OnDarkInvertToggled;

        LanguageChineseButton.Clicked += OnLanguageChineseClicked;
        LanguageEnglishButton.Clicked += OnLanguageEnglishClicked;

        ResetSettingsButton.Clicked += OnResetSettingsClicked;
        LoadUrlButton.Clicked += OnLoadUrlClicked;
        LoadSampleButton.Clicked += OnLoadSampleClicked;
        LocalFileButton.Clicked += OnPickFileClicked;
        FindInSettings<Button>("WorkspaceRootButton").Clicked += OnWorkspaceRootClicked;
        FindInSettings<Button>("WorkspaceUpButton").Clicked += OnWorkspaceUpClicked;
        FindInSettings<Button>("WorkspaceOpenFolderButton").Clicked += OnWorkspaceOpenFolderClicked;
        FindInSettings<Button>("WorkspaceCreateFolderButton").Clicked += OnWorkspaceCreateFolderClicked;
        FindInSettings<Button>("WorkspaceRefreshButton").Clicked += OnWorkspaceRefreshClicked;

        SearchButton.Clicked += OnSearchClicked;
        SearchPrevButton.Clicked += OnSearchPrevClicked;
        SearchNextButton.Clicked += OnSearchNextClicked;

        DisplayModePicker.SelectedIndexChanged += OnDisplayModeChanged;
        OrientationPicker.SelectedIndexChanged += OnOrientationChanged;
        FitPolicyPicker.SelectedIndexChanged += OnFitPolicyChanged;
        ZoomSlider.ValueChanged += OnZoomSliderValueChanged;
        EnableZoomSwitch.Toggled += OnEnableZoomToggled;
        EnableSwipeSwitch.Toggled += OnEnableSwipeToggled;
        EnableLinkSwitch.Toggled += OnEnableLinkToggled;
        EnableFingerDrawSwitch.Toggled += OnFingerDrawToggled;
    }
}
