using Android.App;
using Android.Content.PM;
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
        ApplyImmersiveStatusBar();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
        {
            ApplyImmersiveStatusBar();
        }
    }

    private void ApplyImmersiveStatusBar()
    {
        var window = Window;
        var decorView = window?.DecorView;
        if (window is null || decorView is null)
        {
            return;
        }

        WindowCompat.SetDecorFitsSystemWindows(window, false);
        window.AddFlags(WindowManagerFlags.LayoutNoLimits);
#pragma warning disable CA1422
        window.SetStatusBarColor(Android.Graphics.Color.Transparent);
#pragma warning restore CA1422

        var insetsController = WindowCompat.GetInsetsController(window, decorView);
        if (insetsController is not null)
        {
            insetsController.SystemBarsBehavior =
                WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            insetsController.Hide(WindowInsetsCompat.Type.StatusBars());
        }
    }
}
