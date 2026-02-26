namespace FlowNoteMauiApp;

public partial class MainPage
{
    private static readonly BindableProperty MicroInteractionAttachedProperty =
        BindableProperty.CreateAttached(
            "MicroInteractionAttached",
            typeof(bool),
            typeof(MainPage),
            false);

    private bool _microInteractionsWired;

    private void WireMicroInteractions()
    {
        if (_microInteractionsWired)
            return;

        _microInteractionsWired = true;

        RegisterMicroInteraction(FindInHome<ImageButton>("HomeMenuButton"));
        RegisterMicroInteraction(FindInHome<ImageButton>("HomeDrawerRecentButton"));
        RegisterMicroInteraction(FindInHome<ImageButton>("HomeDrawerAllDocsButton"));
        RegisterMicroInteraction(FindInHome<ImageButton>("HomeDrawerFavoriteButton"));
        RegisterMicroInteraction(FindInHome<ImageButton>("HomeRefreshButton"));
        RegisterMicroInteraction(FindInHome<ImageButton>("HomeOpenSettingsButton"));
        RegisterMicroInteraction(FilterAllButton);
        RegisterMicroInteraction(FilterPdfButton);
        RegisterMicroInteraction(FilterNoteButton);
        RegisterMicroInteraction(FilterFolderButton);
        RegisterMicroInteraction(FindInHome<ImageButton>("HomeSortReorderButton"));
        RegisterMicroInteraction(FindInHome<ImageButton>("HomeSortIconButton"));
        RegisterMicroInteraction(HomeSortButton);
        RegisterMicroInteraction(QuickPenButton);
        RegisterMicroInteraction(FindInHome<ImageButton>("HomeImportButton"));

        RegisterMicroInteraction(FindInEditor<ImageButton>("TopHomeButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("TopImportButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("TopSettingsButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("TopSearchButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("TopLayerButton"));
        RegisterMicroInteraction(UndoButton);
        RegisterMicroInteraction(RedoButton);
        RegisterMicroInteraction(PenModeButton);
        RegisterMicroInteraction(HighlighterButton);
        RegisterMicroInteraction(EraserButton);
        RegisterMicroInteraction(ClearButton2);
        RegisterMicroInteraction(FindInEditor<ImageButton>("TopInlineLayerButton"));
        RegisterMicroInteraction(TextToolButton);
        RegisterMicroInteraction(ImageToolButton);
        RegisterMicroInteraction(ShapeToolButton);
        RegisterMicroInteraction(PrevPageButton);
        RegisterMicroInteraction(NextPageButton);
        RegisterMicroInteraction(ColorBlack);
        RegisterMicroInteraction(ColorBlue);
        RegisterMicroInteraction(ColorRed);
        RegisterMicroInteraction(ColorGreen);
        RegisterMicroInteraction(ColorOrange);
        RegisterMicroInteraction(ColorWhite);
        RegisterMicroInteraction(InputModePenButton);
        RegisterMicroInteraction(InputModeFingerButton);
        RegisterMicroInteraction(InputModeReadButton);
        RegisterMicroInteraction(FindInEditor<ImageButton>("DrawingToolbarLayerButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("DrawingToolbarCloseButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("AddLayerButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("DeleteLayerButton"));

        RegisterMicroInteraction(FindInDrawer<ImageButton>("DrawerHeaderCloseButton"));
        RegisterMicroInteraction(FindInDrawer<Button>("DrawerAllDocsButton"));
        RegisterMicroInteraction(FindInDrawer<Button>("DrawerRecentButton"));
        RegisterMicroInteraction(FindInDrawer<Button>("DrawerFavoriteButton"));
        RegisterMicroInteraction(FindInDrawer<Button>("DrawerTrashButton"));
        RegisterMicroInteraction(FindInDrawer<Button>("DrawerEditTagsButton"));
        RegisterMicroInteraction(FindInDrawer<Button>("DrawerCreateTagButton"));
        RegisterMicroInteraction(FindInDrawer<Button>("DrawerHelpButton"));
        RegisterMicroInteraction(FindInDrawer<Button>("DrawerAboutButton"));
        RegisterMicroInteraction(FindInDrawer<Button>("DrawerDiscountButton"));

        RegisterMicroInteraction(SettingsBackButton);
        RegisterMicroInteraction(FindInSettings<ImageButton>("SettingsCloseButton"));
        RegisterMicroInteraction(PageDirectionVerticalButton);
        RegisterMicroInteraction(PageDirectionHorizontalButton);
        RegisterMicroInteraction(ThemeLightButton);
        RegisterMicroInteraction(ThemeDarkButton);
        RegisterMicroInteraction(ThemeSystemButton);
        RegisterMicroInteraction(LanguageChineseButton);
        RegisterMicroInteraction(LanguageEnglishButton);
        RegisterMicroInteraction(ResetSettingsButton);
        RegisterMicroInteraction(LoadUrlButton);
        RegisterMicroInteraction(LoadSampleButton);
        RegisterMicroInteraction(LocalFileButton);
        RegisterMicroInteraction(FindInSettings<Button>("WorkspaceRootButton"));
        RegisterMicroInteraction(FindInSettings<Button>("WorkspaceUpButton"));
        RegisterMicroInteraction(FindInSettings<Button>("WorkspaceOpenFolderButton"));
        RegisterMicroInteraction(FindInSettings<Button>("WorkspaceCreateFolderButton"));
        RegisterMicroInteraction(FindInSettings<Button>("WorkspaceRefreshButton"));
        RegisterMicroInteraction(SearchButton);
        RegisterMicroInteraction(SearchPrevButton);
        RegisterMicroInteraction(SearchNextButton);
    }

    private void RegisterMicroInteraction(VisualElement? element)
    {
        if (element == null)
            return;

        if (element.GetValue(MicroInteractionAttachedProperty) is bool attached && attached)
            return;

        element.SetValue(MicroInteractionAttachedProperty, true);
        switch (element)
        {
            case ImageButton imageButton:
                imageButton.Pressed += OnMicroInteractionPressed;
                imageButton.Released += OnMicroInteractionReleased;
                imageButton.Clicked += OnMicroInteractionClicked;
                break;
            case Button button:
                button.Pressed += OnMicroInteractionPressed;
                button.Released += OnMicroInteractionReleased;
                button.Clicked += OnMicroInteractionClicked;
                break;
        }
    }

    private void OnMicroInteractionPressed(object? sender, EventArgs e)
    {
        if (sender is not VisualElement visual)
            return;

        _ = AnimateMicroPressStateAsync(visual, pressed: true);
    }

    private void OnMicroInteractionReleased(object? sender, EventArgs e)
    {
        if (sender is not VisualElement visual)
            return;

        _ = AnimateMicroPressStateAsync(visual, pressed: false);
    }

    private async void OnMicroInteractionClicked(object? sender, EventArgs e)
    {
        if (sender is not VisualElement visual)
            return;

        try
        {
            visual.AbortAnimation("flow-micro-click");
            await visual.ScaleToAsync(1.03, 75, Easing.CubicOut);
            await visual.ScaleToAsync(1.0, 110, Easing.CubicIn);
        }
        catch
        {
        }
    }

    private static Task AnimateMicroPressStateAsync(VisualElement visual, bool pressed)
    {
        try
        {
            visual.AbortAnimation("flow-micro-press");
            if (pressed)
            {
                return Task.WhenAll(
                    visual.ScaleToAsync(0.95, 80, Easing.CubicOut),
                    visual.TranslateToAsync(0, 1.5, 80, Easing.CubicOut));
            }

            return Task.WhenAll(
                visual.ScaleToAsync(1.0, 130, Easing.SpringOut),
                visual.TranslateToAsync(0, 0, 130, Easing.SpringOut));
        }
        catch
        {
            return Task.CompletedTask;
        }
    }
}
