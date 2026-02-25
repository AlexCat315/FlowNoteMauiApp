using Microsoft.Extensions.DependencyInjection;

namespace FlowNoteMauiApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		LanguageManager.Initialize();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}