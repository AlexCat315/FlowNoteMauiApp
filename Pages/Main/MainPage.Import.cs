using FlowNoteMauiApp.Resources;
using Microsoft.Maui.Devices;
using System.Diagnostics;
#if IOS || MACCATALYST
using Foundation;
using UIKit;
#endif

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private static readonly FilePickerFileType PdfPickerFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        { DevicePlatform.iOS, new[] { "com.adobe.pdf", "public.pdf" } },
        { DevicePlatform.MacCatalyst, new[] { "com.adobe.pdf", "public.pdf" } },
        { DevicePlatform.Android, new[] { "application/pdf" } },
        { DevicePlatform.WinUI, new[] { ".pdf" } }
    });
    private static readonly TimeSpan PickerReentryCooldown = TimeSpan.FromMilliseconds(1600);
    private bool _isPickingPdf;
    private DateTime _pickerCooldownUntilUtc = DateTime.MinValue;

    private async void OnLoadUrlClicked(object? sender, EventArgs e)
    {
        await LoadFromUrlAsync(UrlEntry.Text, showAlertOnError: true, openAfterImport: true);
    }

    private async void OnLoadSampleClicked(object? sender, EventArgs e)
    {
        UrlEntry.Text = DefaultSampleUrl;
        await LoadFromUrlAsync(DefaultSampleUrl, showAlertOnError: true, openAfterImport: true);
    }

    private async Task LoadFromUrlAsync(string? input, bool showAlertOnError, bool openAfterImport)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            ShowStatus(AppResources.InvalidUrl);
            if (showAlertOnError)
                await DisplayAlertAsync(AppResources.InvalidUrl, AppResources.EnterFullUrl, "OK");
            return;
        }

        try
        {
            ShowStatus("Downloading PDF...");
            var bytes = await _httpClient.GetByteArrayAsync(uri);
            if (bytes.Length == 0)
            {
                ShowStatus(AppResources.FileEmpty);
                return;
            }

            var suggestedName = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(suggestedName) || !suggestedName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                suggestedName = $"{uri.Host}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            }

            await SaveCurrentDrawingStateAsync();
            var note = await _workspaceService.ImportPdfAsync(suggestedName, bytes, _workspaceFolder);
            await RefreshWorkspaceViewsAsync();

            if (openAfterImport)
            {
                await OpenWorkspaceNoteAsync(note);
            }
            else
            {
                ShowStatus($"Imported: {note.Name}");
                ShowHomeScreen();
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"{AppResources.LoadFailed}: {ex.Message}");
            if (showAlertOnError)
                await DisplayAlertAsync(AppResources.LoadFailed, ex.Message, "OK");
        }
    }

    private async void OnPickFileClicked(object? sender, EventArgs e)
    {
        SetSettingsVisible(false);
        SetDrawerVisible(false);
        await PickAndImportPdfAsync(openAfterImport: true);
    }

    private async Task PickAndImportPdfAsync(bool openAfterImport)
    {
        if (_isPickingPdf)
            return;

        var now = DateTime.UtcNow;
        if (now < _pickerCooldownUntilUtc)
            return;

        _isPickingPdf = true;
        _pickerCooldownUntilUtc = now.Add(PickerReentryCooldown);
        SetImportButtonsEnabled(false);
        try
        {
            var options = new PickOptions
            {
                PickerTitle = AppResources.SelectPdfFile
            };

            var platform = DeviceInfo.Platform;
            var isIos = platform == DevicePlatform.iOS;
            var isMacCatalyst = platform == DevicePlatform.MacCatalyst;
            var useFileTypeFilter = !isIos && !isMacCatalyst;
            if (useFileTypeFilter)
            {
                options.FileTypes = PdfPickerFileType;
            }

            LogPicker($"pick-start platform={platform} isIos={isIos} useFilter={useFileTypeFilter}");
            var result = await PickSingleFileAsync(options, useMacCatalystPickerWorkaround: isMacCatalyst);

            if (result is null)
            {
                LogPicker("pick-result null");
                ShowStatus(AppResources.FileSelectionCancelled);
                return;
            }

            LogPicker($"pick-result file={result.FileName} contentType={result.ContentType}");

            await using var stream = await result.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var data = memory.ToArray();
            if (data.Length == 0)
            {
                ShowStatus(AppResources.FileEmpty);
                return;
            }

            var extension = Path.GetExtension(result.FileName);
            var isPdfByName = string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
            var isPdfByMime = !string.IsNullOrWhiteSpace(result.ContentType)
                && result.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
            var isPdfByHeader = data.Length >= 4
                && data[0] == 0x25 && data[1] == 0x50 && data[2] == 0x44 && data[3] == 0x46;
            if (!isPdfByName && !isPdfByMime && !isPdfByHeader)
            {
                ShowStatus(AppResources.SelectPdfFileOnly);
                return;
            }

            var importedName = string.IsNullOrWhiteSpace(result.FileName)
                ? $"Imported-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf"
                : result.FileName;
            if (!importedName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                importedName += ".pdf";
            }

            await SaveCurrentDrawingStateAsync();
            var note = await _workspaceService.ImportPdfAsync(importedName, data, _workspaceFolder);
            await RefreshWorkspaceViewsAsync();

            if (openAfterImport)
            {
                await OpenWorkspaceNoteAsync(note);
            }
            else
            {
                ShowStatus($"Imported: {note.Name}");
                ShowHomeScreen();
            }
        }
        catch (OperationCanceledException)
        {
            LogPicker("pick-cancelled operation canceled");
            ShowStatus(AppResources.FileSelectionCancelled);
        }
        catch (Exception ex)
        {
            LogPicker($"pick-failed {ex.GetType().Name}: {ex.Message}");
            ShowStatus($"{AppResources.SelectFileFailed}: {ex.Message}");
        }
        finally
        {
            _isPickingPdf = false;
            _pickerCooldownUntilUtc = DateTime.UtcNow.Add(PickerReentryCooldown);
            SetImportButtonsEnabled(true);
        }
    }

    private async Task<FileResult?> PickSingleFileAsync(PickOptions options, bool useMacCatalystPickerWorkaround)
    {
        if (useMacCatalystPickerWorkaround)
        {
#if IOS || MACCATALYST
            var nativeResult = await PickSingleFileWithNativePickerAsync();
            if (nativeResult is not null)
            {
                return nativeResult;
            }

            LogPicker("pick-native-result null on maccatalyst, fallback to maui-picker");
#endif
        }

        return await FilePicker.Default.PickAsync(options);
    }

#if IOS || MACCATALYST
    private async Task<FileResult?> PickSingleFileWithNativePickerAsync()
    {
        var tcs = new TaskCompletionSource<FileResult?>(TaskCreationOptions.RunContinuationsAsynchronously);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var presenter = GetActivePresenter();
            if (presenter is null)
            {
                LogPicker("pick-native-presenter null");
                tcs.TrySetResult(null);
                return;
            }

            LogPicker("pick-native-start");
#pragma warning disable CA1422
            var picker = new UIDocumentPickerViewController(new[] { "com.adobe.pdf", "public.pdf" }, UIDocumentPickerMode.Import)
            {
                AllowsMultipleSelection = false,
                ModalPresentationStyle = UIModalPresentationStyle.FormSheet
            };
#pragma warning restore CA1422

            NativePdfDocumentPickerDelegate? activeDelegate = null;
            activeDelegate = new NativePdfDocumentPickerDelegate(
                pickedUrl =>
                {
                    var fileResult = CreateFileResultFromNativePickedUrl(pickedUrl);
                    LogPicker(fileResult is null
                        ? "pick-native-picked null"
                        : $"pick-native-picked file={fileResult.FileName}");
                    picker.WeakDelegate = null;
                    activeDelegate = null;
                    tcs.TrySetResult(fileResult);
                },
                () =>
                {
                    LogPicker("pick-native-cancelled");
                    picker.WeakDelegate = null;
                    activeDelegate = null;
                    tcs.TrySetResult(null);
                });
            picker.Delegate = activeDelegate;

            presenter.PresentViewController(picker, true, null);
        });

        return await tcs.Task;
    }

    private static FileResult? CreateFileResultFromNativePickedUrl(NSUrl? pickedUrl)
    {
        if (pickedUrl is null || !pickedUrl.IsFileUrl)
        {
            return null;
        }

        var sourcePath = pickedUrl.Path;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var originalFileName = Path.GetFileName(sourcePath);
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".pdf";
            }

            originalFileName = $"Imported-{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
        }

        var localFileName = originalFileName;
        var localPath = Path.Combine(FileSystem.CacheDirectory, localFileName);
        if (File.Exists(localPath))
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(localFileName);
            var ext = Path.GetExtension(localFileName);
            localFileName = $"{nameWithoutExt}-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            localPath = Path.Combine(FileSystem.CacheDirectory, localFileName);
        }
        var hasSecurityScope = pickedUrl.StartAccessingSecurityScopedResource();

        try
        {
            File.Copy(sourcePath, localPath, overwrite: true);
            return new FileResult(localPath);
        }
        catch (Exception ex)
        {
            LogPicker($"pick-native-copy-failed {ex.GetType().Name}: {ex.Message}");
            return new FileResult(sourcePath);
        }
        finally
        {
            if (hasSecurityScope)
            {
                pickedUrl.StopAccessingSecurityScopedResource();
            }
        }
    }

    private static UIViewController? GetActivePresenter()
    {
        var connectedScene = UIApplication.SharedApplication
            .ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault(scene => scene.ActivationState == UISceneActivationState.ForegroundActive);

        var window = connectedScene?.Windows.FirstOrDefault(w => w.IsKeyWindow)
            ?? UIApplication.SharedApplication
                .ConnectedScenes
                .OfType<UIWindowScene>()
                .SelectMany(scene => scene.Windows)
                .FirstOrDefault(w => w.IsKeyWindow);

        var controller = window?.RootViewController;
        while (controller?.PresentedViewController is not null)
        {
            controller = controller.PresentedViewController;
        }

        return controller;
    }

    private sealed class NativePdfDocumentPickerDelegate : UIDocumentPickerDelegate
    {
        private readonly Action<NSUrl?> _picked;
        private readonly Action _cancelled;

        public NativePdfDocumentPickerDelegate(Action<NSUrl?> picked, Action cancelled)
        {
            _picked = picked;
            _cancelled = cancelled;
        }

        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
        {
            _picked(url);
        }

        public override void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            _picked(urls.FirstOrDefault());
        }

        public override void WasCancelled(UIDocumentPickerViewController controller)
        {
            _cancelled();
        }
    }
#endif

    [Conditional("DEBUG")]
    private static void LogPicker(string message)
    {
        Debug.WriteLine($"[FlowNote Picker] {message}");
    }

    private void SetImportButtonsEnabled(bool enabled)
    {
        try
        {
            FindInHome<ImageButton>("HomeImportButton").IsEnabled = enabled;
        }
        catch
        {
        }

        try
        {
            FindInEditor<ImageButton>("TopImportButton").IsEnabled = enabled;
        }
        catch
        {
        }

        try
        {
            LocalFileButton.IsEnabled = enabled;
        }
        catch
        {
        }
    }

    private void OnReloadClicked(object? sender, EventArgs e)
    {
        if (!EnsurePdfLoaded(showHint: true))
            return;

        PdfViewer.Reload();
        ShowStatus(AppResources.ReloadTriggered);
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        SetSettingsVisible(false);
        SetDrawerVisible(!HomeDrawerOverlay.IsVisible);
    }

    private void OnSettingsCloseClicked(object? sender, EventArgs e)
    {
        SetSettingsVisible(false);
    }

    private async void OnHomeClicked(object? sender, EventArgs e)
    {
        await RefreshWorkspaceViewsAsync();
        ShowHomeScreen();
    }

    private async void OnHomeImportLocalClicked(object? sender, EventArgs e)
    {
        SetSettingsVisible(false);
        SetDrawerVisible(false);
        var openAfterImport = TopBarPanel.IsVisible;
        await PickAndImportPdfAsync(openAfterImport);
    }

    private async void OnHomeImportUrlClicked(object? sender, EventArgs e)
    {
        await LoadFromUrlAsync(HomeUrlEntry.Text, showAlertOnError: true, openAfterImport: false);
    }

    private async void OnHomeRefreshClicked(object? sender, EventArgs e)
    {
        await RefreshWorkspaceViewsAsync();
    }
}
