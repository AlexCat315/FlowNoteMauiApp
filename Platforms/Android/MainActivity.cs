using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace FlowNoteMauiApp;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplyStatusBarLayout();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
        {
            ApplyStatusBarLayout();
        }
    }

    private void ApplyStatusBarLayout()
    {
        var window = Window;
        var decorView = window?.DecorView;
        if (window is null || decorView is null)
        {
            return;
        }

        WindowCompat.SetDecorFitsSystemWindows(window, true);
        window.ClearFlags(WindowManagerFlags.LayoutNoLimits);
#pragma warning disable CA1422
        window.SetStatusBarColor(Android.Graphics.Color.Rgb(247, 248, 251));
#pragma warning restore CA1422

        var insetsController = WindowCompat.GetInsetsController(window, decorView);
        if (insetsController is not null)
        {
            var nightMask = Resources?.Configuration?.UiMode ?? UiMode.NightNo;
            var isDarkTheme = (nightMask & UiMode.NightMask) == UiMode.NightYes;
            insetsController.AppearanceLightStatusBars = !isDarkTheme;
            insetsController.Show(WindowInsetsCompat.Type.StatusBars());
        }
    }
}
