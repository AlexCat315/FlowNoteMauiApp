using Flow.PDFView;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using FlowNoteMauiApp.Services;
using FlowNoteMauiApp.Core.Drawing;
using FlowNoteMauiApp.Core.AI;
using FlowNoteMauiApp.Core.Security;
using FlowNoteMauiApp.Data.Repositories;

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

		builder.Services.AddSingleton<IDrawingEngine, SkiaDrawingEngine>();
		builder.Services.AddSingleton<ILayerManager, LayerManager>();
		builder.Services.AddSingleton<IStrokeRecorder, StrokeRecorder>();
				
		builder.Services.AddSingleton<IHandwritingRecognizer, HandwritingRecognizer>();
		builder.Services.AddSingleton<IShapeRecognizer, ShapeRecognizer>();
				
		builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
		builder.Services.AddSingleton<IBiometricService, BiometricService>();
				
		builder.Services.AddSingleton<IFileRepository, FileRepository>();
		builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
				
		builder.Services.AddSingleton<IDocumentService, DocumentService>();
		builder.Services.AddSingleton<ISyncService, SyncService>();
		builder.Services.AddSingleton<IWorkspaceService, WorkspaceService>();
		builder.Services.AddSingleton<IDrawingPersistenceService, DrawingPersistenceService>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
