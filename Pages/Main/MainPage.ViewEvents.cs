namespace FlowNoteMauiApp;

public partial class MainPage
{
    private bool _viewEventsWired;

    private void WireComposedViewEvents()
    {
        if (_viewEventsWired)
            return;

        _viewEventsWired = true;

        WireHomePanelEvents();
        WireEditorChromeEvents();
        WireDrawerEvents();
        WireSettingsEvents();
    }

    private void WireHomePanelEvents()
    {
        FindInHome<ImageButton>("HomeMenuButton").Clicked += OnMenuClicked;
        FindInHome<ImageButton>("HomeDrawerRecentButton").Clicked += OnDrawerRecentClicked;
        FindInHome<ImageButton>("HomeDrawerAllDocsButton").Clicked += OnDrawerAllDocsClicked;
        FindInHome<ImageButton>("HomeDrawerFavoriteButton").Clicked += OnDrawerFavoriteClicked;
        FindInHome<ImageButton>("HomeRefreshButton").Clicked += OnHomeRefreshClicked;
        FindInHome<ImageButton>("HomeOpenSettingsButton").Clicked += OnOpenSettingsClicked;
        FilterAllButton.Clicked += OnHomeFilterAllClicked;
        FilterPdfButton.Clicked += OnHomeFilterPdfClicked;
        FilterNoteButton.Clicked += OnHomeFilterNoteClicked;
        FilterFolderButton.Clicked += OnHomeFilterFolderClicked;
        FindInHome<ImageButton>("HomeSortReorderButton").Clicked += OnHomeSortClicked;
        FindInHome<ImageButton>("HomeSortIconButton").Clicked += OnHomeSortClicked;
        HomeSortButton.Clicked += OnHomeSortClicked;
        QuickPenButton.Clicked += OnHomeQuickPenClicked;
        FindInHome<ImageButton>("HomeImportButton").Clicked += OnHomeImportLocalClicked;
        HomeSearchEntry.TextChanged += OnHomeSearchTextChanged;
    }

    private void WireEditorChromeEvents()
    {
        FindInEditor<ImageButton>("TopHomeButton").Clicked += OnHomeClicked;
        FindInEditor<ImageButton>("TopImportButton").Clicked += OnHomeImportLocalClicked;
        FindInEditor<ImageButton>("TopSettingsButton").Clicked += OnOpenSettingsClicked;
        FindInEditor<ImageButton>("TopSearchButton").Clicked += OnEditorSearchClicked;
        FindInEditor<ImageButton>("TopLayerButton").Clicked += OnLayerToggleClicked;

        UndoButton.Clicked += OnUndoClicked;
        RedoButton.Clicked += OnRedoClicked;
        PenModeButton.Clicked += OnPenModeClicked;
        HighlighterButton.Clicked += OnHighlighterClicked;
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
        FindInEditor<ImageButton>("DrawingToolbarLayerButton").Clicked += OnLayerToggleClicked;
        FindInEditor<ImageButton>("DrawingToolbarCloseButton").Clicked += OnDrawingToolbarCloseClicked;
        FindInEditor<ImageButton>("AddLayerButton").Clicked += OnAddLayerClicked;
        FindInEditor<ImageButton>("DeleteLayerButton").Clicked += OnDeleteLayerClicked;
    }

    private void WireDrawerEvents()
    {
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

        PageFreeMoveSwitch.Toggled += OnPageFreeMoveToggled;
        PageMoveResistanceSlider.ValueChanged += OnPageMoveResistanceChanged;
        ZoomFollowSwitch.Toggled += OnZoomFollowToggled;

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
