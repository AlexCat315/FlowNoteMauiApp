namespace FlowNoteMauiApp;

public partial class MainPage
{
    private static readonly TimeSpan BackToHomeConfirmWindow = TimeSpan.FromSeconds(1.6);
    private DateTime _lastBackPressedUtc = DateTime.MinValue;

    protected override bool OnBackButtonPressed()
    {
        EnsureUiBootstrapped();

        if (SettingsOverlayView.IsVisible)
        {
            SetSettingsVisible(false);
            return true;
        }

        if (DrawerOverlayView.IsVisible)
        {
            SetDrawerVisible(false);
            return true;
        }

        if (HomeSortPanel.IsVisible)
        {
            SetHomeSortPanelVisible(false);
            return true;
        }

        if (InputModePanel.IsVisible)
        {
            SetInputModePanelVisible(false);
            return true;
        }

        if (DrawingToolbarPanel.IsVisible)
        {
            AnimatePopupOut(DrawingToolbarPanel, () => DrawingToolbarPanel.IsVisible = false);
            return true;
        }

        if (ThumbnailPanel.IsVisible)
        {
            AnimatePopupOut(ThumbnailPanel, () => ThumbnailPanel.IsVisible = false);
            return true;
        }

        if (LayerPanel.IsVisible)
        {
            AnimatePopupOut(LayerPanel, () => LayerPanel.IsVisible = false);
            return true;
        }

        if (EditorChromeView.IsVisible && !HomePanelView.IsVisible)
        {
            var now = DateTime.UtcNow;
            if (now - _lastBackPressedUtc <= BackToHomeConfirmWindow)
            {
                _lastBackPressedUtc = DateTime.MinValue;
                ShowHomeScreen();
                return true;
            }

            _lastBackPressedUtc = now;
            ShowStatus(T("BackToHomeHint", "Press back again to return to Home."));
            return true;
        }

        _lastBackPressedUtc = DateTime.MinValue;
        return base.OnBackButtonPressed();
    }
}
