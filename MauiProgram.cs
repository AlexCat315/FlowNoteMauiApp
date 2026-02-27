using Flow.PDFView;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using FlowNoteMauiApp.Core.Services;
using FlowNoteMauiApp.Data.Persistence;

namespace FlowNoteMauiApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiPdfView()
			.UseSkiaSharp()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<IWorkspaceService, WorkspaceStore>();
		builder.Services.AddSingleton<IDrawingPersistenceService, DrawingStateStore>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
