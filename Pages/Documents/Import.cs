using System.Buffers;
using System.Diagnostics;
using FlowNoteMauiApp.Resources;
using Microsoft.Maui.Devices;
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
    private bool _isImportingPdf;
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

        if (_isImportingPdf)
            return;

        _isImportingPdf = true;
        SetImportButtonsEnabled(false);
        try
        {
            await ShowImportProgressAsync("Importing PDF", "Connecting...");
            ShowStatus("Downloading PDF...");
            using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            await UpdateImportProgressAsync(0.06, "Downloading...");

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            var bytes = await ReadAllBytesWithProgressAsync(
                responseStream,
                contentLength,
                progress =>
                {
                    if (progress < 0d)
                        return UpdateImportProgressAsync(0.40, "Downloading...");

                    var phase = 0.08 + (progress * 0.62);
                    return UpdateImportProgressAsync(phase, $"Downloading {progress * 100d:0}%");
                });

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

            await UpdateImportProgressAsync(0.78, "Saving document...");
            await SaveCurrentDrawingStateAsync();
            var note = await _workspaceService.ImportPdfAsync(suggestedName, bytes, _workspaceFolder);
            QueuePrimeHomeCoverCache(note, bytes);

            if (openAfterImport)
            {
                await UpdateImportProgressAsync(0.92, "Opening editor...");
                await OpenWorkspaceNoteAsync(note);
            }
            else
            {
                await UpdateImportProgressAsync(0.92, "Refreshing home...");
                await RefreshWorkspaceViewsAsync();
                ShowStatus($"Imported: {note.Name}");
                ShowHomeScreen();
            }

            await UpdateImportProgressAsync(1d, "Import complete");
        }
        catch (Exception ex)
        {
            ShowStatus($"{AppResources.LoadFailed}: {ex.Message}");
            if (showAlertOnError)
                await DisplayAlertAsync(AppResources.LoadFailed, ex.Message, "OK");
        }
        finally
        {
            _isImportingPdf = false;
            SetImportButtonsEnabled(true);
            await HideImportProgressAsync();
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
        if (_isImportingPdf)
            return;

        var now = DateTime.UtcNow;
        if (now < _pickerCooldownUntilUtc)
            return;

        _isPickingPdf = true;
        _isImportingPdf = true;
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

            await ShowImportProgressAsync("Importing PDF", "Reading selected file...");
            await using var stream = await result.OpenReadAsync();
            var streamLength = TryGetStreamLength(stream);
            var data = await ReadAllBytesWithProgressAsync(
                stream,
                streamLength,
                progress =>
                {
                    if (progress < 0d)
                        return UpdateImportProgressAsync(0.36, "Reading selected file...");

                    var phase = 0.08 + (progress * 0.58);
                    return UpdateImportProgressAsync(phase, $"Reading {progress * 100d:0}%");
                });
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

            await UpdateImportProgressAsync(0.74, "Saving document...");
            await SaveCurrentDrawingStateAsync();
            var note = await _workspaceService.ImportPdfAsync(importedName, data, _workspaceFolder);
            QueuePrimeHomeCoverCache(note, data);

            if (openAfterImport)
            {
                await UpdateImportProgressAsync(0.92, "Opening editor...");
                await OpenWorkspaceNoteAsync(note);
            }
            else
            {
                await UpdateImportProgressAsync(0.92, "Refreshing home...");
                await RefreshWorkspaceViewsAsync();
                ShowStatus($"Imported: {note.Name}");
                ShowHomeScreen();
            }

            await UpdateImportProgressAsync(1d, "Import complete");
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
            _isImportingPdf = false;
            _pickerCooldownUntilUtc = DateTime.UtcNow.Add(PickerReentryCooldown);
            SetImportButtonsEnabled(true);
            await HideImportProgressAsync();
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

    private Task ShowImportProgressAsync(string title, string detail)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            ImportProgressTitleLabel.Text = title;
            ImportProgressDetailLabel.Text = detail;
            ImportProgressBar.Progress = 0d;
            ImportProgressOverlay.InputTransparent = false;
            ImportProgressOverlay.IsVisible = true;
        });
    }

    private Task UpdateImportProgressAsync(double progress, string detail)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!ImportProgressOverlay.IsVisible)
                return;

            ImportProgressBar.Progress = Math.Clamp(progress, 0d, 1d);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                ImportProgressDetailLabel.Text = detail;
            }
        });
    }

    private Task HideImportProgressAsync()
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            ImportProgressOverlay.IsVisible = false;
            ImportProgressOverlay.InputTransparent = true;
            ImportProgressBar.Progress = 0d;
            ImportProgressTitleLabel.Text = string.Empty;
            ImportProgressDetailLabel.Text = string.Empty;
        });
    }

    private static long? TryGetStreamLength(Stream stream)
    {
        if (!stream.CanSeek)
            return null;

        try
        {
            var length = stream.Length;
            if (length > 0 && stream.Position != 0)
            {
                stream.Position = 0;
            }

            return length > 0 ? length : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]> ReadAllBytesWithProgressAsync(
        Stream stream,
        long? totalBytes,
        Func<double, Task> reportProgressAsync,
        CancellationToken token = default)
    {
        const int bufferSize = 128 * 1024;
        using var memory = totalBytes is > 0 and <= int.MaxValue
            ? new MemoryStream((int)totalBytes.Value)
            : new MemoryStream();

        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        long totalRead = 0;
        var lastTick = Stopwatch.GetTimestamp();
        var lastReportedProgress = -1d;

        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), token).ConfigureAwait(false);
                if (read <= 0)
                    break;

                await memory.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                totalRead += read;

                var progress = totalBytes is > 0
                    ? Math.Clamp(totalRead / (double)totalBytes.Value, 0d, 1d)
                    : -1d;
                var now = Stopwatch.GetTimestamp();
                var shouldReport = progress < 0d
                    ? Stopwatch.GetElapsedTime(lastTick, now).TotalMilliseconds >= 140
                    : progress >= 1d
                      || progress - lastReportedProgress >= 0.02d
                      || Stopwatch.GetElapsedTime(lastTick, now).TotalMilliseconds >= 120;
                if (!shouldReport)
                    continue;

                await reportProgressAsync(progress).ConfigureAwait(false);
                lastTick = now;
                if (progress >= 0d)
                {
                    lastReportedProgress = progress;
                }
            }

            await reportProgressAsync(1d).ConfigureAwait(false);
            return memory.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
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
