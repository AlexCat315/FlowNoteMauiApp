using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

// 引入必要的 AndroidX 库
using AndroidX.Core.View;
using AndroidX.Core.Content;

namespace FlowNoteMauiApp;

[Activity(Theme = "@style/MainTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        
        // 关键步骤：启用 Edge-to-edge，让应用内容延伸到系统栏（状态栏、导航栏）后面
        // 这是现代 Android 应用的标准做法，也是 Android 15 的默认行为
        WindowCompat.SetDecorFitsSystemWindows(Window, false);

        // 步骤 1: 确保系统为状态栏绘制背景（对于 Android 5.0+）
        // 注意：在 Android 15+ 上，此标志仍被设置，但 `SetStatusBarColor` 将失效。
        // 我们保留它是为了向下兼容。
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            Window.AddFlags(WindowManagerFlags.DrawsSystemBarBackgrounds);
            Window.ClearFlags(WindowManagerFlags.TranslucentStatus);
        }

        // 步骤 2: 获取当前窗口的 WindowInsetsControllerCompat
        var insetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
        
        // 步骤 3: 将状态栏设置为透明。
        // ** 这是对 Android 14 (API 34) 及以下版本起作用的传统方法。
        // ** 在 Android 15+ 上，系统默认就是 Edge-to-edge，状态栏背景就是透明的。
        // ** 我们保留此调用以确保在旧版本上的行为一致。
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
        {
            Window.SetStatusBarColor(Android.Graphics.Color.Transparent);
        }

        // 步骤 4: 设置状态栏图标和字体的颜色（浅色或深色）。
        // 这是现代且推荐的方式，在所有版本（包括 Android 15+）上都有效。
        // `true` 表示状态栏内容应为深色（适合浅色背景）。
        // `false` 表示状态栏内容应为浅色（适合深色背景）。
        // 请根据您的应用主题进行调整。
        insetsController.AppearanceLightStatusBars = true; 
        
        // 可选：如果您也希望隐藏状态栏（例如全屏游戏或阅读器），可以使用：
        // insetsController.Hide(WindowInsetsCompat.Type.StatusBars());
        // 要显示回来，使用： insetsController.Show(WindowInsetsCompat.Type.StatusBars());
    }
}