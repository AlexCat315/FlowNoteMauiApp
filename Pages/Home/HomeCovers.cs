using System.Collections.Concurrent;
using System.Diagnostics;
using Flow.PDFView;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Models;
using Microsoft.Maui.Storage;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private const int HomeCoverRequestWidth = 280;
    private const int HomeCoverRequestHeight = 392;
    private readonly SemaphoreSlim _homeCoverRenderSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, ImageSource> _homeCoverSourceCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<ImageSource?>> _homeCoverLoadTasks = new(StringComparer.Ordinal);
    private PdfView? _homeCoverRendererView;

    private void BindHomeCoverPreview(WorkspaceNote note, Image previewImage)
    {
        var cacheKey = BuildHomeCoverCacheKey(note);
        if (_homeCoverSourceCache.TryGetValue(cacheKey, out var cachedSource))
        {
            previewImage.Source = cachedSource;
            return;
        }

        var token = _homeFeedRenderCts?.Token ?? CancellationToken.None;
        _ = LoadAndApplyHomeCoverAsync(note, cacheKey, previewImage, token);
    }

    private async Task LoadAndApplyHomeCoverAsync(
        WorkspaceNote note,
        string cacheKey,
        Image previewImage,
        CancellationToken token)
    {
        try
        {
            var loadTask = _homeCoverLoadTasks.GetOrAdd(
                cacheKey,
                _ => LoadHomeCoverSourceAsync(note, cacheKey, token));
            var source = await loadTask.ConfigureAwait(false);
            if (source is null || token.IsCancellationRequested)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    previewImage.Source = source;
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeCover] load failed for {note.Id}: {ex.Message}");
        }
        finally
        {
            _homeCoverLoadTasks.TryRemove(cacheKey, out _);
        }
    }

    private async Task<ImageSource?> LoadHomeCoverSourceAsync(
        WorkspaceNote note,
        string cacheKey,
        CancellationToken token)
    {
        if (_homeCoverSourceCache.TryGetValue(cacheKey, out var cachedSource))
            return cachedSource;

        if (TryLoadHomeCoverFromDisk(cacheKey, out var diskSource))
        {
            _homeCoverSourceCache[cacheKey] = diskSource;
            return diskSource;
        }

        await _homeCoverRenderSemaphore.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_homeCoverSourceCache.TryGetValue(cacheKey, out cachedSource))
                return cachedSource;

            var bytes = await _workspaceService.GetPdfBytesAsync(note.Id).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0 || token.IsCancellationRequested)
                return null;

            var coverBytes = await RenderHomeCoverBytesAsync(bytes, token).ConfigureAwait(false);
            if (coverBytes is null || coverBytes.Length == 0 || token.IsCancellationRequested)
                return null;

            SaveHomeCoverToDisk(cacheKey, coverBytes);
            var source = ImageSource.FromStream(() => new MemoryStream(coverBytes));
            _homeCoverSourceCache[cacheKey] = source;
            return source;
        }
        finally
        {
            _homeCoverRenderSemaphore.Release();
        }
    }

    private async Task<byte[]?> RenderHomeCoverBytesAsync(byte[] pdfBytes, CancellationToken token)
    {
        await using var thumbnailStream = await RenderHomeCoverStreamAsync(pdfBytes, token).ConfigureAwait(false);
        if (thumbnailStream is null || token.IsCancellationRequested)
            return null;

        using var memory = new MemoryStream();
        await thumbnailStream.CopyToAsync(memory, token).ConfigureAwait(false);
        return memory.Length > 0 ? memory.ToArray() : null;
    }

    private async Task<Stream?> RenderHomeCoverStreamAsync(byte[] pdfBytes, CancellationToken token)
    {
        var renderer = await EnsureHomeCoverRendererViewAsync().ConfigureAwait(false);

        var loadTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<DocumentLoadedEventArgs>? onLoaded = null;
        EventHandler<PdfErrorEventArgs>? onError = null;

        onLoaded = (_, _) => loadTcs.TrySetResult(true);
        onError = (_, args) => loadTcs.TrySetException(
            new InvalidOperationException(string.IsNullOrWhiteSpace(args.Message) ? "PDF load failed." : args.Message));

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            renderer.DocumentLoaded += onLoaded;
            renderer.Error += onError;
            renderer.Source = new BytesPdfSource(pdfBytes);
        });

        using var registration = token.Register(() => loadTcs.TrySetCanceled(token));
        try
        {
            await loadTcs.Task.ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return null;

            return await renderer
                .GetThumbnailAsync(0, HomeCoverRequestWidth, HomeCoverRequestHeight)
                .ConfigureAwait(false);
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                renderer.DocumentLoaded -= onLoaded;
                renderer.Error -= onError;
                renderer.Source = null;
            });
        }
    }

    private async Task<PdfView> EnsureHomeCoverRendererViewAsync()
    {
        if (_homeCoverRendererView is not null)
            return _homeCoverRendererView;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_homeCoverRendererView is not null)
                return;

            _homeCoverRendererView = new PdfView
            {
                IsVisible = false,
                Opacity = 0,
                InputTransparent = true,
                WidthRequest = 1,
                HeightRequest = 1,
                MinZoom = 1f,
                MaxZoom = 1f,
                Zoom = 1f,
                EnableZoom = false,
                EnableSwipe = false,
                EnableTapGestures = false,
                EnableLinkNavigation = false,
                DisplayMode = PdfDisplayMode.SinglePage,
                ScrollOrientation = PdfScrollOrientation.Vertical,
                FitPolicy = FitPolicy.Width
            };

            EditorHost.Children.Insert(0, _homeCoverRendererView);
        });

        return _homeCoverRendererView!;
    }

    private static string BuildHomeCoverCacheKey(WorkspaceNote note)
    {
        var version = note.ModifiedAtUtc.Ticks <= 0 ? note.CreatedAtUtc.Ticks : note.ModifiedAtUtc.Ticks;
        return $"{note.Id}_{version}";
    }

    private static string ResolveHomeCoverCacheDirectory()
    {
        var cacheRoot = FileSystem.Current.CacheDirectory;
        return Path.Combine(cacheRoot, "home-covers");
    }

    private static string BuildHomeCoverFilePath(string cacheKey)
    {
        return Path.Combine(ResolveHomeCoverCacheDirectory(), $"{cacheKey}.png");
    }

    private static bool TryLoadHomeCoverFromDisk(string cacheKey, out ImageSource source)
    {
        source = null!;
        try
        {
            var path = BuildHomeCoverFilePath(cacheKey);
            if (!File.Exists(path))
                return false;

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0)
                return false;

            source = ImageSource.FromStream(() => new MemoryStream(bytes));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveHomeCoverToDisk(string cacheKey, byte[] bytes)
    {
        try
        {
            var directory = ResolveHomeCoverCacheDirectory();
            Directory.CreateDirectory(directory);
            var path = BuildHomeCoverFilePath(cacheKey);
            File.WriteAllBytes(path, bytes);
        }
        catch
        {
        }
    }
}
