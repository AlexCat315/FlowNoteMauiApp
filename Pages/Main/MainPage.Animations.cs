namespace FlowNoteMauiApp;

public partial class MainPage
{
    private const uint PopupInDurationMs = 150;
    private const uint PopupOutDurationMs = 120;
    private const uint ScreenInDurationMs = 190;

    private void AnimatePopupIn(VisualElement panel)
    {
        panel.AbortAnimation("flow-popup-in");
        panel.AbortAnimation("flow-popup-out");
        panel.Opacity = 0;
        panel.Scale = 0.97;
        panel.TranslationY = 8;
        _ = Task.WhenAll(
            panel.FadeToAsync(1, PopupInDurationMs, Easing.CubicOut),
            panel.ScaleToAsync(1, PopupInDurationMs, Easing.CubicOut),
            panel.TranslateToAsync(panel.TranslationX, 0, PopupInDurationMs, Easing.CubicOut));
    }

    private void AnimatePopupOut(VisualElement panel, Action? onCompleted = null)
    {
        if (!panel.IsVisible)
        {
            onCompleted?.Invoke();
            return;
        }

        panel.AbortAnimation("flow-popup-in");
        panel.AbortAnimation("flow-popup-out");
        _ = AnimatePopupOutCoreAsync(panel, onCompleted);
    }

    private async Task AnimatePopupOutCoreAsync(VisualElement panel, Action? onCompleted)
    {
        try
        {
            await Task.WhenAll(
                panel.FadeToAsync(0, PopupOutDurationMs, Easing.CubicIn),
                panel.ScaleToAsync(0.975, PopupOutDurationMs, Easing.CubicIn),
                panel.TranslateToAsync(panel.TranslationX, 8, PopupOutDurationMs, Easing.CubicIn));
        }
        catch
        {
        }
        finally
        {
            panel.Opacity = 1;
            panel.Scale = 1;
            panel.TranslationY = 0;
            onCompleted?.Invoke();
        }
    }

    private void AnimateScreenEntry(VisualElement screen)
    {
        screen.AbortAnimation("flow-screen-in");
        screen.Opacity = 0;
        screen.Scale = 0.996;
        screen.TranslationY = 10;
        _ = Task.WhenAll(
            screen.FadeToAsync(1, ScreenInDurationMs, Easing.CubicOut),
            screen.ScaleToAsync(1, ScreenInDurationMs, Easing.CubicOut),
            screen.TranslateToAsync(0, 0, ScreenInDurationMs, Easing.CubicOut));
    }
}
