using System.Runtime.CompilerServices;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private static readonly BindableProperty MicroInteractionAttachedProperty =
        BindableProperty.CreateAttached(
            "MicroInteractionAttached",
            typeof(bool),
            typeof(MainPage),
            false);
    private static readonly ConditionalWeakTable<VisualElement, MicroInteractionState> MicroInteractionStateMap = new();

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
        RegisterMicroInteraction(HomeSortMenuButton);
        RegisterMicroInteraction(FilterAllButton);
        RegisterMicroInteraction(FilterPdfButton);
        RegisterMicroInteraction(FilterNoteButton);
        RegisterMicroInteraction(FilterFolderButton);
        RegisterMicroInteraction(HomeSortTimeAscButton);
        RegisterMicroInteraction(HomeSortTimeDescButton);
        RegisterMicroInteraction(HomeSortNameAscButton);
        RegisterMicroInteraction(HomeSortNameDescButton);
        RegisterMicroInteraction(QuickPenButton);
        RegisterMicroInteraction(FindInHome<ImageButton>("HomeImportButton"));

        RegisterMicroInteraction(FindInEditor<ImageButton>("TopHomeButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("TopImportButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("TopSettingsButton"));
        RegisterMicroInteraction(FindInEditor<ImageButton>("TopSearchButton"));
        RegisterMicroInteraction(TopModePenButton);
        RegisterMicroInteraction(FindInEditor<ImageButton>("TopThumbnailButton"));
        RegisterMicroInteraction(UndoButton);
        RegisterMicroInteraction(RedoButton);
        RegisterMicroInteraction(PenModeButton);
        RegisterMicroInteraction(HighlighterButton);
        RegisterMicroInteraction(PencilButton);
        RegisterMicroInteraction(MarkerButton);
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
        RegisterMicroInteraction(FindInEditor<ImageButton>("DrawingToolbarCloseButton"));
        RegisterMicroInteraction(PixelEraserModeButton);
        RegisterMicroInteraction(StrokeEraserModeButton);
        RegisterMicroInteraction(LassoEraserModeButton);
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

        var state = GetInteractionState(visual);
        CaptureInteractionBase(visual, state);
        _ = AnimateMicroPressStateAsync(visual, state, pressed: true);
    }

    private void OnMicroInteractionReleased(object? sender, EventArgs e)
    {
        if (sender is not VisualElement visual)
            return;

        var state = GetInteractionState(visual);
        _ = AnimateMicroPressStateAsync(visual, state, pressed: false);
    }

    private async void OnMicroInteractionClicked(object? sender, EventArgs e)
    {
        if (sender is not VisualElement visual)
            return;

        try
        {
            visual.AbortAnimation("flow-micro-click");
            var baseScale = visual.Scale <= 0d ? 1d : visual.Scale;
            await visual.ScaleToAsync(baseScale * 1.03, 75, Easing.CubicOut);
            await visual.ScaleToAsync(baseScale, 110, Easing.CubicIn);
        }
        catch
        {
        }
    }

    private static Task AnimateMicroPressStateAsync(VisualElement visual, MicroInteractionState state, bool pressed)
    {
        try
        {
            visual.AbortAnimation("flow-micro-press");
            if (pressed)
            {
                var pressedScale = Math.Max(0.01d, state.BaseScale * 0.95d);
                return Task.WhenAll(
                    visual.ScaleToAsync(pressedScale, 80, Easing.CubicOut),
                    visual.TranslateToAsync(state.BaseTranslationX, state.BaseTranslationY + 1.5d, 80, Easing.CubicOut));
            }

            return Task.WhenAll(
                visual.ScaleToAsync(state.BaseScale, 130, Easing.SpringOut),
                visual.TranslateToAsync(state.BaseTranslationX, state.BaseTranslationY, 130, Easing.SpringOut));
        }
        catch
        {
            return Task.CompletedTask;
        }
    }

    private static MicroInteractionState GetInteractionState(VisualElement visual)
    {
        var state = MicroInteractionStateMap.GetOrCreateValue(visual);
        if (!state.HasBaseValues)
        {
            state.BaseScale = visual.Scale;
            state.BaseTranslationX = visual.TranslationX;
            state.BaseTranslationY = visual.TranslationY;
            state.HasBaseValues = true;
        }

        return state;
    }

    private static void CaptureInteractionBase(VisualElement visual, MicroInteractionState state)
    {
        state.BaseScale = visual.Scale;
        state.BaseTranslationX = visual.TranslationX;
        state.BaseTranslationY = visual.TranslationY;
        state.HasBaseValues = true;
    }

    private sealed class MicroInteractionState
    {
        public double BaseScale { get; set; } = 1d;
        public double BaseTranslationX { get; set; }
        public double BaseTranslationY { get; set; }
        public bool HasBaseValues { get; set; }
    }
}
