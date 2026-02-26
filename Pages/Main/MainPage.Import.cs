using FlowNoteMauiApp.Resources;
using Microsoft.Maui.Devices;

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
        await PickAndImportPdfAsync(openAfterImport: true);
    }

    private async Task PickAndImportPdfAsync(bool openAfterImport)
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = AppResources.SelectPdfFile,
                FileTypes = PdfPickerFileType
            };

            // Some iOS/MacCatalyst environments return null from PickAsync; retry with PickMultipleAsync.
            var result = await MainThread.InvokeOnMainThreadAsync(() => FilePicker.Default.PickAsync(options));
            var isApplePlatform = DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.MacCatalyst;
            if (result is null && isApplePlatform)
            {
                var fallback = await MainThread.InvokeOnMainThreadAsync(() => FilePicker.Default.PickMultipleAsync(options));
                result = fallback?.FirstOrDefault();
            }

            if (result is null && isApplePlatform)
            {
                var anyTypeOptions = new PickOptions
                {
                    PickerTitle = AppResources.SelectPdfFile
                };
                result = await MainThread.InvokeOnMainThreadAsync(() => FilePicker.Default.PickAsync(anyTypeOptions));
            }

            if (result is null)
            {
                ShowStatus(AppResources.FileSelectionCancelled);
                return;
            }

            if (!string.Equals(Path.GetExtension(result.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus(AppResources.SelectPdfFileOnly);
                return;
            }

            await using var stream = await result.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var data = memory.ToArray();
            if (data.Length == 0)
            {
                ShowStatus(AppResources.FileEmpty);
                return;
            }

            await SaveCurrentDrawingStateAsync();
            var note = await _workspaceService.ImportPdfAsync(result.FileName, data, _workspaceFolder);
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
            ShowStatus($"{AppResources.SelectFileFailed}: {ex.Message}");
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
