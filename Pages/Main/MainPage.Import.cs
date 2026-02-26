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

            var isApple = DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.MacCatalyst;
            if (!isApple)
            {
                options.FileTypes = PdfPickerFileType;
            }

            var result = await FilePicker.Default.PickAsync(options);

            if (result is null)
            {
                ShowStatus(AppResources.FileSelectionCancelled);
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
            ShowStatus(AppResources.FileSelectionCancelled);
        }
        catch (Exception ex)
        {
            ShowStatus($"{AppResources.SelectFileFailed}: {ex.Message}");
        }
        finally
        {
            _isPickingPdf = false;
            _pickerCooldownUntilUtc = DateTime.UtcNow.Add(PickerReentryCooldown);
            SetImportButtonsEnabled(true);
        }
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
