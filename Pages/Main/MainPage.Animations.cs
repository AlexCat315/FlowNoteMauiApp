namespace FlowNoteMauiApp;

public partial class MainPage
{
    private const uint PopupInDurationMs = 180;
    private const uint PopupOutDurationMs = 135;
    private const uint ScreenInDurationMs = 210;

    private void AnimatePopupIn(VisualElement panel)
    {
        panel.AbortAnimation("flow-popup-in");
        panel.AbortAnimation("flow-popup-out");
        panel.Opacity = 0;
        panel.Scale = 0.94;
        panel.TranslationY = 10;
        _ = Task.WhenAll(
            panel.FadeToAsync(1, PopupInDurationMs, Easing.SinOut),
            panel.ScaleToAsync(1, PopupInDurationMs, Easing.SpringOut),
            panel.TranslateToAsync(panel.TranslationX, 0, PopupInDurationMs, Easing.SpringOut));
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
                panel.ScaleToAsync(0.965, PopupOutDurationMs, Easing.CubicIn),
                panel.TranslateToAsync(panel.TranslationX, 10, PopupOutDurationMs, Easing.CubicIn));
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
        screen.Scale = 0.992;
        screen.TranslationY = 14;
        _ = Task.WhenAll(
            screen.FadeToAsync(1, ScreenInDurationMs, Easing.SinOut),
            screen.ScaleToAsync(1, ScreenInDurationMs, Easing.CubicOut),
            screen.TranslateToAsync(0, 0, ScreenInDurationMs, Easing.CubicOut));
    }
}
