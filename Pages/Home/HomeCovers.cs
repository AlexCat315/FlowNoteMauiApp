using System.Collections.Concurrent;
using System.Diagnostics;
using Flow.PDFView;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Models;
using Microsoft.Maui.Storage;
using SkiaSharp;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private const int HomeCoverRequestWidth = 376;
    private const int HomeCoverRequestHeight = 528;
    private static readonly TimeSpan HomeCoverLoadTimeout = TimeSpan.FromSeconds(32);
    private static readonly TimeSpan HomeCoverRendererReadyTimeout = TimeSpan.FromSeconds(3);
    private readonly SemaphoreSlim _homeCoverRenderSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<string, ImageSource> _homeCoverSourceCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<ImageSource?>> _homeCoverLoadTasks = new(StringComparer.Ordinal);
    private PdfView? _homeCoverRendererView;
    private Task? _homeCoverRendererReadyTask;

    private void BindHomeCoverPreview(WorkspaceNote note, Image previewImage, Image? placeholderImage = null)
    {
        if (TryLoadCustomHomeCover(note.Id, out var customSource))
        {
            ApplyHomeCoverSource(previewImage, placeholderImage, customSource);
            return;
        }

        var cacheKey = BuildHomeCoverCacheKey(note);
        if (_homeCoverSourceCache.TryGetValue(cacheKey, out var cachedSource))
        {
            ApplyHomeCoverSource(previewImage, placeholderImage, cachedSource);
            return;
        }

        _ = LoadAndApplyHomeCoverAsync(note, cacheKey, previewImage, placeholderImage, CancellationToken.None);
    }

    private async Task LoadAndApplyHomeCoverAsync(
        WorkspaceNote note,
        string cacheKey,
        Image previewImage,
        Image? placeholderImage,
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
                    ApplyHomeCoverSource(previewImage, placeholderImage, source);
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

            var coverBytes = await RenderHomeCoverBytesAsync(note, bytes, token).ConfigureAwait(false);
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

    private sealed class HomeCoverRenderResult
    {
        public required Stream ThumbnailStream { get; init; }
        public required PdfPageBounds? PageBounds { get; init; }
    }

    private async Task<byte[]?> RenderHomeCoverBytesAsync(WorkspaceNote note, byte[] pdfBytes, CancellationToken token)
    {
        var renderResult = await RenderHomeCoverStreamAsync(pdfBytes, token).ConfigureAwait(false);
        if (renderResult is null || token.IsCancellationRequested)
            return null;

        await using var thumbnailStream = renderResult.ThumbnailStream;
        using var memory = new MemoryStream();
        await thumbnailStream.CopyToAsync(memory, token).ConfigureAwait(false);
        if (memory.Length <= 0)
            return null;

        var baseCoverBytes = memory.ToArray();
        return await TryComposeHomeCoverWithInkAsync(note, baseCoverBytes, renderResult.PageBounds, token).ConfigureAwait(false);
    }

    private async Task<HomeCoverRenderResult?> RenderHomeCoverStreamAsync(byte[] pdfBytes, CancellationToken token)
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
            var completed = await Task
                .WhenAny(loadTcs.Task, Task.Delay(HomeCoverLoadTimeout, token))
                .ConfigureAwait(false);
            if (!ReferenceEquals(completed, loadTcs.Task))
                throw new TimeoutException("Home cover render timed out.");

            await loadTcs.Task.ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return null;

            var pageBounds = await renderer.GetPageBoundsAsync(0).ConfigureAwait(false);
            var thumbnailStream = await renderer
                .GetThumbnailAsync(0, HomeCoverRequestWidth, HomeCoverRequestHeight)
                .ConfigureAwait(false);
            if (thumbnailStream is null)
                return null;

            return new HomeCoverRenderResult
            {
                ThumbnailStream = thumbnailStream,
                PageBounds = pageBounds
            };
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

    private async Task<byte[]?> TryComposeHomeCoverWithInkAsync(
        WorkspaceNote note,
        byte[] baseCoverBytes,
        PdfPageBounds? pageBounds,
        CancellationToken token)
    {
        if (baseCoverBytes.Length == 0 || token.IsCancellationRequested)
            return null;

        if (pageBounds is null || pageBounds.Value.Width <= 0 || pageBounds.Value.Height <= 0)
            return baseCoverBytes;

        DrawingDocumentState? state = null;
        if (string.Equals(note.Id, _currentNoteId, StringComparison.Ordinal) && IsEditorInitialized)
        {
            try
            {
                state = await MainThread
                    .InvokeOnMainThreadAsync(() => DrawingCanvas.ExportState())
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HomeCover] live ink-state export failed for {note.Id}: {ex.Message}");
            }
        }

        if (state is null)
        {
            try
            {
                state = await _drawingPersistenceService.LoadAsync(note.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HomeCover] ink-state load failed for {note.Id}: {ex.Message}");
            }
        }

        if (state is null || state.Layers.Count == 0 || token.IsCancellationRequested)
            return baseCoverBytes;

        var snapshots = BuildHomeCoverStrokeSnapshots(state);
        if (snapshots.Count == 0 || token.IsCancellationRequested)
            return baseCoverBytes;

        try
        {
            var bounds = pageBounds.Value;
            var hasIntersectingStroke = false;
            foreach (var snapshot in snapshots)
            {
                if (!DoesSnapshotIntersectPage(snapshot, bounds))
                    continue;

                hasIntersectingStroke = true;
                break;
            }

            var composed = ComposeThumbnailWithInkOverlay(baseCoverBytes, bounds, snapshots, token);
            if (composed is { Length: > 0 } && hasIntersectingStroke)
                return composed;

            if (!hasIntersectingStroke)
            {
                var fallback = ComposeThumbnailWithInkOverlay(
                    baseCoverBytes,
                    bounds,
                    snapshots,
                    token,
                    requireIntersection: false,
                    overridePageOriginX: 0f,
                    overridePageOriginY: 0f);
                if (fallback is { Length: > 0 })
                    return fallback;
            }

            return composed ?? baseCoverBytes;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeCover] ink compose failed for {note.Id}: {ex.Message}");
            return baseCoverBytes;
        }
    }

    private static List<ThumbnailStrokeSnapshot> BuildHomeCoverStrokeSnapshots(DrawingDocumentState state)
    {
        var snapshots = new List<ThumbnailStrokeSnapshot>();
        foreach (var layerState in state.Layers)
        {
            if (!layerState.IsVisible || layerState.Opacity <= 0.001f)
                continue;

            var layerOpacity = Math.Clamp(layerState.Opacity, 0f, 1f);
            foreach (var strokeState in layerState.Strokes)
            {
                if (strokeState.Points.Count < 2)
                    continue;

                var stroke = MaterializeStroke(strokeState);
                if (!TryGetStrokeBounds(stroke, out var minX, out var minY, out var maxX, out var maxY))
                    continue;

                snapshots.Add(new ThumbnailStrokeSnapshot
                {
                    Stroke = stroke,
                    LayerOpacity = layerOpacity,
                    MinX = minX,
                    MinY = minY,
                    MaxX = maxX,
                    MaxY = maxY
                });
            }
        }

        return snapshots;
    }

    private static DrawingStroke MaterializeStroke(DrawingStrokeState strokeState)
    {
        var stroke = new DrawingStroke
        {
            Color = FromArgb(strokeState.Color),
            StrokeWidth = strokeState.StrokeWidth,
            Opacity = strokeState.Opacity,
            IsEraser = strokeState.IsEraser,
            BrushType = strokeState.BrushType,
            Options = new StrokeOptions
            {
                PressureEnabled = strokeState.PressureEnabled,
                SmoothingEnabled = strokeState.SmoothingEnabled,
                SmoothingFactor = strokeState.SmoothingFactor,
                MinPressure = strokeState.MinPressure,
                MaxPressure = strokeState.MaxPressure,
                TaperStart = strokeState.TaperStart,
                TaperEnd = strokeState.TaperEnd,
                Streamline = strokeState.Streamline
            }
        };

        foreach (var point in strokeState.Points)
        {
            stroke.AddPoint(new DrawingPoint(point.X, point.Y, point.Pressure, point.Timestamp));
        }

        return stroke;
    }

    private static SKColor FromArgb(uint argb)
    {
        var a = (byte)((argb >> 24) & 0xFF);
        var r = (byte)((argb >> 16) & 0xFF);
        var g = (byte)((argb >> 8) & 0xFF);
        var b = (byte)(argb & 0xFF);
        return new SKColor(r, g, b, a);
    }

    private async Task<PdfView> EnsureHomeCoverRendererViewAsync()
    {
        if (_homeCoverRendererView is not null)
        {
            if (_homeCoverRendererReadyTask is not null)
            {
                await WaitForHomeCoverRendererReadyAsync(_homeCoverRendererReadyTask).ConfigureAwait(false);
            }
            return _homeCoverRendererView;
        }

        var readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _homeCoverRendererReadyTask = readyTcs.Task;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (_homeCoverRendererView is not null)
            {
                readyTcs.TrySetResult(true);
                return;
            }

            EnsureUiBootstrapped();

            _homeCoverRendererView = new PdfView
            {
                IsVisible = true,
                Opacity = 0.001,
                InputTransparent = true,
                WidthRequest = HomeCoverRequestWidth,
                HeightRequest = HomeCoverRequestHeight,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
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

            _homeCoverRendererView.Loaded += OnHomeCoverRendererLoaded;
            EditorHost.Children.Insert(0, _homeCoverRendererView);
            if (_homeCoverRendererView.Handler is not null)
            {
                _homeCoverRendererView.Loaded -= OnHomeCoverRendererLoaded;
                readyTcs.TrySetResult(true);
            }

            void OnHomeCoverRendererLoaded(object? sender, EventArgs args)
            {
                if (_homeCoverRendererView is null)
                    return;

                _homeCoverRendererView.Loaded -= OnHomeCoverRendererLoaded;
                readyTcs.TrySetResult(true);
            }
        });

        if (_homeCoverRendererReadyTask is not null)
        {
            await WaitForHomeCoverRendererReadyAsync(_homeCoverRendererReadyTask).ConfigureAwait(false);
        }

        return _homeCoverRendererView!;
    }

    private static async Task WaitForHomeCoverRendererReadyAsync(Task readyTask)
    {
        var completed = await Task.WhenAny(readyTask, Task.Delay(HomeCoverRendererReadyTimeout)).ConfigureAwait(false);
        if (ReferenceEquals(completed, readyTask))
        {
            await readyTask.ConfigureAwait(false);
        }
    }

    private static void ApplyHomeCoverSource(Image previewImage, Image? placeholderImage, ImageSource source)
    {
        previewImage.Source = source;
        if (placeholderImage is not null)
        {
            placeholderImage.IsVisible = false;
            placeholderImage.Opacity = 0;
        }
    }

    private async Task PrimeHomeCoverCacheAsync(WorkspaceNote note, byte[] pdfBytes, CancellationToken token = default)
    {
        if (pdfBytes.Length == 0)
            return;

        var cacheKey = BuildHomeCoverCacheKey(note);
        if (_homeCoverSourceCache.ContainsKey(cacheKey))
            return;

        if (TryLoadHomeCoverFromDisk(cacheKey, out var diskSource))
        {
            _homeCoverSourceCache[cacheKey] = diskSource;
            return;
        }

        await _homeCoverRenderSemaphore.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_homeCoverSourceCache.ContainsKey(cacheKey))
                return;

            var coverBytes = await RenderHomeCoverBytesAsync(note, pdfBytes, token).ConfigureAwait(false);
            if (coverBytes is null || coverBytes.Length == 0)
                return;

            SaveHomeCoverToDisk(cacheKey, coverBytes);
            _homeCoverSourceCache[cacheKey] = ImageSource.FromStream(() => new MemoryStream(coverBytes));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeCover] pre-generate failed for {note.Id}: {ex.Message}");
        }
        finally
        {
            _homeCoverRenderSemaphore.Release();
        }
    }

    private string BuildHomeCoverCacheKey(WorkspaceNote note)
    {
        var version = note.ModifiedAtUtc.Ticks <= 0 ? note.CreatedAtUtc.Ticks : note.ModifiedAtUtc.Ticks;
        var inkVersion = ResolveInkStateVersionTicks(note.Id);
        var liveRevision = _liveInkRevisionByNoteId.TryGetValue(note.Id, out var revision)
            ? revision
            : 0L;
        return $"{note.Id}_{version}_{inkVersion}_{liveRevision}";
    }

    private static string ResolveHomeCoverCacheDirectory()
    {
        var cacheRoot = FileSystem.Current.AppDataDirectory;
        return Path.Combine(cacheRoot, "home-covers");
    }

    private static string ResolveCustomHomeCoverDirectory()
    {
        var cacheRoot = FileSystem.Current.AppDataDirectory;
        return Path.Combine(cacheRoot, "home-custom-covers");
    }

    private static string BuildHomeCoverFilePath(string cacheKey)
    {
        return Path.Combine(ResolveHomeCoverCacheDirectory(), $"{cacheKey}.png");
    }

    private static string BuildCustomHomeCoverPath(string noteId)
    {
        var safeId = noteId.Trim().Replace('/', '_').Replace('\\', '_');
        return Path.Combine(ResolveCustomHomeCoverDirectory(), $"{safeId}.png");
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

    private static bool TryLoadCustomHomeCover(string noteId, out ImageSource source)
    {
        source = null!;
        if (string.IsNullOrWhiteSpace(noteId))
            return false;

        try
        {
            var path = BuildCustomHomeCoverPath(noteId);
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

    private static long ResolveInkStateVersionTicks(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return 0;

        try
        {
            var path = BuildInkStateFilePath(noteId);
            if (!File.Exists(path))
                return 0;

            return ComputeInkStateFingerprint(path);
        }
        catch
        {
            return 0;
        }
    }

    private static long ComputeInkStateFingerprint(string path)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;

        var info = new FileInfo(path);
        hash ^= (ulong)info.Length;
        hash *= prime;
        hash ^= (ulong)info.LastWriteTimeUtc.Ticks;
        hash *= prime;

        using var stream = File.OpenRead(path);
        Span<byte> buffer = stackalloc byte[4096];
        var remaining = 96 * 1024;
        while (remaining > 0)
        {
            var read = stream.Read(buffer[..Math.Min(buffer.Length, remaining)]);
            if (read <= 0)
                break;

            remaining -= read;
            for (var i = 0; i < read; i++)
            {
                hash ^= buffer[i];
                hash *= prime;
            }
        }

        return unchecked((long)(hash & 0x7FFFFFFFFFFFFFFF));
    }

    private static string BuildInkStateFilePath(string noteId)
    {
        var safeId = noteId.Trim().Replace('/', '_').Replace('\\', '_');
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlowNote",
            "workspace",
            "ink");
        return Path.Combine(root, $"{safeId}.json");
    }

    private void QueuePrimeHomeCoverCache(WorkspaceNote note, byte[] pdfBytes)
    {
        if (pdfBytes.Length == 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await PrimeHomeCoverCacheAsync(note, pdfBytes).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HomeCover] queue pre-generate failed for {note.Id}: {ex.Message}");
            }
        });
    }

    private void InvalidateHomeCoverCacheForNote(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return;

        var prefix = $"{noteId}_";
        var keys = _homeCoverSourceCache.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        foreach (var key in keys)
        {
            _homeCoverSourceCache.TryRemove(key, out _);
            _homeCoverLoadTasks.TryRemove(key, out _);
            try
            {
                var path = BuildHomeCoverFilePath(key);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }

    private async Task<bool> SetCustomHomeCoverAsync(string noteId, FileResult pickedImage, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(noteId))
            return false;

        try
        {
            await using var stream = await pickedImage.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, token).ConfigureAwait(false);
            var raw = memory.ToArray();
            if (raw.Length == 0)
                return false;

            using var bitmap = SKBitmap.Decode(raw);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
                return false;

            const int targetWidth = 376;
            const int targetHeight = 528;
            using var resized = bitmap.Resize(
                new SKImageInfo(targetWidth, targetHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            using var finalBitmap = resized ?? bitmap.Copy();
            using var image = SKImage.FromBitmap(finalBitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 96);
            var bytes = encoded?.ToArray();
            if (bytes is null || bytes.Length == 0)
                return false;

            var directory = ResolveCustomHomeCoverDirectory();
            Directory.CreateDirectory(directory);
            var path = BuildCustomHomeCoverPath(noteId);
            await File.WriteAllBytesAsync(path, bytes, token).ConfigureAwait(false);
            InvalidateHomeCoverCacheForNote(noteId);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
