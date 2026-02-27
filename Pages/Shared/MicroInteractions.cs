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
        RegisterMicroInteraction(OpenColorWheelButton);
        RegisterMicroInteraction(ApplyColorWheelButton);
        RegisterMicroInteraction(CancelColorWheelButton);
        RegisterMicroInteraction(ThumbnailListModeButton);
        RegisterMicroInteraction(ThumbnailGridModeButton);
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
        RegisterMicroInteraction(ImportBfNoteButton);
        RegisterMicroInteraction(ExportBfNoteButton);
        RegisterMicroInteraction(ExportOriginalPdfButton);
        RegisterMicroInteraction(ExportOverlayPdfButton);
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
            var baseScaleX = visual.ScaleX <= 0d ? 1d : visual.ScaleX;
            var baseScaleY = visual.ScaleY <= 0d ? 1d : visual.ScaleY;
            await AnimateScaleAsync(visual, baseScaleX * 1.03d, baseScaleY * 1.03d, 75, Easing.CubicOut);
            await AnimateScaleAsync(visual, baseScaleX, baseScaleY, 110, Easing.CubicIn);
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
                var pressedScaleX = Math.Max(0.01d, state.BaseScaleX * 0.95d);
                var pressedScaleY = Math.Max(0.01d, state.BaseScaleY * 0.95d);
                return AnimateScaleAsync(visual, pressedScaleX, pressedScaleY, 80, Easing.CubicOut);
            }

            return AnimateScaleAsync(visual, state.BaseScaleX, state.BaseScaleY, 130, Easing.SpringOut);
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
            state.BaseScaleX = visual.ScaleX;
            state.BaseScaleY = visual.ScaleY;
            state.HasBaseValues = true;
        }

        return state;
    }

    private static void CaptureInteractionBase(VisualElement visual, MicroInteractionState state)
    {
        state.BaseScaleX = visual.ScaleX;
        state.BaseScaleY = visual.ScaleY;
        state.HasBaseValues = true;
    }

    private static Task AnimateScaleAsync(VisualElement visual, double scaleX, double scaleY, uint duration, Easing easing)
    {
        var targetScaleX = Math.Max(0.01d, scaleX);
        var targetScaleY = Math.Max(0.01d, scaleY);
        var startScaleX = visual.ScaleX;
        var startScaleY = visual.ScaleY;
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            visual.AbortAnimation("flow-micro-scale");
            var animation = new Animation();
            animation.Add(0d, 1d, new Animation(v => visual.ScaleX = v, startScaleX, targetScaleX, easing));
            animation.Add(0d, 1d, new Animation(v => visual.ScaleY = v, startScaleY, targetScaleY, easing));
            animation.Commit(
                visual,
                "flow-micro-scale",
                rate: 16,
                length: duration,
                easing: Easing.Linear,
                finished: (_, _) => completion.TrySetResult(true));
        }
        catch
        {
            completion.TrySetResult(true);
        }

        return completion.Task;
    }

    private sealed class MicroInteractionState
    {
        public double BaseScaleX { get; set; } = 1d;
        public double BaseScaleY { get; set; } = 1d;
        public bool HasBaseValues { get; set; }
    }
}
