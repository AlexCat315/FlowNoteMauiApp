using Foundation;
using UIKit;

namespace FlowNoteMauiApp;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
	{
		var launched = base.FinishedLaunching(application, launchOptions);

		foreach (var scene in application.ConnectedScenes)
		{
			if (scene is not UIWindowScene windowScene)
				continue;

			var titlebar = windowScene.Titlebar;
			if (titlebar is null)
				continue;

			titlebar.TitleVisibility = UITitlebarTitleVisibility.Hidden;
			titlebar.Toolbar = null;
		}

		return launched;
	}
}
