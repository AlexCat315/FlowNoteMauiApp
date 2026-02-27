using SkiaSharp;
using Flow.PDFView;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Controls;
using FlowNoteMauiApp.Helpers;
using FlowNoteMauiApp.Models;
using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Layouts;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private void ApplyThumbnailLayoutModeVisual()
    {
        var listSelected = _thumbnailLayoutMode == ThumbnailLayoutMode.List;
        var gridSelected = _thumbnailLayoutMode == ThumbnailLayoutMode.Grid;
        var palette = Palette;

        ThumbnailListModeButton.BackgroundColor = listSelected
            ? palette.ModeButtonExpandedBackground
            : palette.ModeButtonCollapsedBackground;
        ThumbnailListModeButton.BorderColor = listSelected
            ? palette.ModeButtonExpandedBorder
            : palette.ModeButtonCollapsedBorder;
        ThumbnailListModeButton.BorderWidth = 1;
        ThumbnailListModeButton.TextColor = listSelected
            ? palette.ModeSelectionText
            : palette.TabInactiveText;

        ThumbnailGridModeButton.BackgroundColor = gridSelected
            ? palette.ModeButtonExpandedBackground
            : palette.ModeButtonCollapsedBackground;
        ThumbnailGridModeButton.BorderColor = gridSelected
            ? palette.ModeButtonExpandedBorder
            : palette.ModeButtonCollapsedBorder;
        ThumbnailGridModeButton.BorderWidth = 1;
        ThumbnailGridModeButton.TextColor = gridSelected
            ? palette.ModeSelectionText
            : palette.TabInactiveText;
    }

    private void ApplyThumbnailContainerLayout()
    {
        if (_thumbnailLayoutMode == ThumbnailLayoutMode.Grid)
        {
            ThumbnailList.Direction = FlexDirection.Row;
            ThumbnailList.Wrap = FlexWrap.Wrap;
            ThumbnailList.JustifyContent = FlexJustify.Start;
            ThumbnailList.AlignItems = FlexAlignItems.Start;
            return;
        }

        ThumbnailList.Direction = FlexDirection.Column;
        ThumbnailList.Wrap = FlexWrap.NoWrap;
        ThumbnailList.JustifyContent = FlexJustify.Start;
        ThumbnailList.AlignItems = FlexAlignItems.Stretch;
    }

    private void InvalidateThumbnailCache()
    {
        _thumbnailLoadCts?.Cancel();
        _thumbnailLoadCts?.Dispose();
        _thumbnailLoadCts = null;
        _thumbnailSourceCache.Clear();
        _thumbnailItemLookup.Clear();
        _thumbnailWindowStart = -1;
        _thumbnailWindowEnd = -1;
        _thumbnailSelectedPage = -1;
    }

    private void RefreshThumbnailList()
    {
        ApplyThumbnailLayoutModeVisual();
        ApplyThumbnailContainerLayout();

        if (!IsEditorInitialized || _totalPageCount <= 0)
        {
            ThumbnailList.Children.Clear();
            _thumbnailItemLookup.Clear();
            _thumbnailWindowStart = -1;
            _thumbnailWindowEnd = -1;
            _thumbnailSelectedPage = -1;
            return;
        }

        var maxVisibleItems = _thumbnailLayoutMode == ThumbnailLayoutMode.Grid
            ? MaxGridThumbnailItems
            : (_thumbnailIncludeInkOverlay ? MaxOverlayThumbnailItems : MaxPlainThumbnailItems);
        var (startIndex, endIndex) = ResolveThumbnailWindow(maxVisibleItems);
        if (_thumbnailLayoutMode == ThumbnailLayoutMode.Grid && _totalPageCount <= MaxGridThumbnailItems)
        {
            startIndex = 0;
            endIndex = _totalPageCount - 1;
        }

        var needsRebuild = ThumbnailList.Children.Count == 0
            || _thumbnailItemLookup.Count == 0
            || startIndex != _thumbnailWindowStart
            || endIndex != _thumbnailWindowEnd;
        if (!needsRebuild)
        {
            UpdateThumbnailSelection(_currentPageIndex);
            return;
        }

        _thumbnailLoadCts?.Cancel();
        _thumbnailLoadCts?.Dispose();
        _thumbnailLoadCts = new CancellationTokenSource();
        var token = _thumbnailLoadCts.Token;
        var overlaySnapshots = _thumbnailIncludeInkOverlay
            ? CaptureThumbnailStrokeSnapshots()
            : null;

        ThumbnailList.Children.Clear();
        _thumbnailItemLookup.Clear();

        if (_thumbnailLayoutMode == ThumbnailLayoutMode.List && startIndex > 0)
        {
            ThumbnailList.Children.Add(CreateThumbnailListItem(
                0,
                false,
                token,
                overlaySnapshots,
                TF("ThumbnailPageHeadFormat", "Page {0} ...", 1)));
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            ThumbnailList.Children.Add(CreateThumbnailListItem(i, i == _currentPageIndex, token, overlaySnapshots));
        }

        if (_thumbnailLayoutMode == ThumbnailLayoutMode.List && endIndex < _totalPageCount - 1)
        {
            ThumbnailList.Children.Add(CreateThumbnailListItem(
                _totalPageCount - 1,
                false,
                token,
                overlaySnapshots,
                TF("ThumbnailPageTailFormat", "... Page {0}", _totalPageCount)));
        }

        _thumbnailWindowStart = startIndex;
        _thumbnailWindowEnd = endIndex;
        _thumbnailSelectedPage = -1;
        UpdateThumbnailSelection(_currentPageIndex);
    }

    private (int Start, int End) ResolveThumbnailWindow(int maxVisibleItems)
    {
        if (_totalPageCount <= maxVisibleItems)
            return (0, _totalPageCount - 1);

        var hasExistingWindow = _thumbnailWindowStart >= 0
            && _thumbnailWindowEnd >= _thumbnailWindowStart
            && _thumbnailWindowEnd < _totalPageCount;
        if (hasExistingWindow
            && _currentPageIndex >= _thumbnailWindowStart
            && _currentPageIndex <= _thumbnailWindowEnd)
        {
            return (_thumbnailWindowStart, _thumbnailWindowEnd);
        }

        var start = Math.Clamp(_currentPageIndex - (maxVisibleItems / 2), 0, _totalPageCount - maxVisibleItems);
        var end = start + maxVisibleItems - 1;
        return (start, end);
    }

    private void UpdateThumbnailSelection(int pageIndex)
    {
        if (_thumbnailSelectedPage == pageIndex
            && _thumbnailItemLookup.TryGetValue(pageIndex, out _))
        {
            return;
        }

        if (_thumbnailSelectedPage >= 0
            && _thumbnailItemLookup.TryGetValue(_thumbnailSelectedPage, out var previousItem))
        {
            ApplyThumbnailItemSelectionVisual(previousItem, false);
        }

        if (_thumbnailItemLookup.TryGetValue(pageIndex, out var currentItem))
        {
            ApplyThumbnailItemSelectionVisual(currentItem, true);
            _thumbnailSelectedPage = pageIndex;
        }
        else
        {
            _thumbnailSelectedPage = -1;
        }
    }

    private void ApplyThumbnailItemSelectionVisual(Border item, bool isCurrent)
    {
        var palette = Palette;
        item.BackgroundColor = isCurrent ? palette.LayerSelectedBackground : Colors.Transparent;
        item.Stroke = isCurrent ? palette.LayerSelectedBorder : palette.LayerNormalBorder;

        if (item.Content is VerticalStackLayout stack
            && stack.Children.FirstOrDefault() is Border previewBorder)
        {
            previewBorder.Stroke = isCurrent ? palette.ModeButtonExpandedBorder : palette.LayerNormalBorder;
            previewBorder.BackgroundColor = isCurrent
                ? palette.ModeButtonExpandedBackground
                : palette.ModeButtonCollapsedBackground;
        }
    }

    private string BuildThumbnailCacheKey(int pageIndex)
    {
        var noteId = string.IsNullOrWhiteSpace(_currentNoteId) ? "none" : _currentNoteId!;
        var overlayKey = _thumbnailIncludeInkOverlay ? "overlay" : "pdf";
        return $"{noteId}:{pageIndex}:{ThumbnailRequestWidth}x{ThumbnailRequestHeight}:{overlayKey}";
    }

    private Border CreateThumbnailListItem(
        int pageIndex,
        bool isCurrent,
        CancellationToken token,
        IReadOnlyList<ThumbnailStrokeSnapshot>? overlaySnapshots = null,
        string? titleOverride = null)
    {
        var previewImage = new Image
        {
            Aspect = Aspect.AspectFit,
            Source = "icon_file.png",
            WidthRequest = ThumbnailPreviewWidth,
            HeightRequest = ThumbnailPreviewHeight,
            MinimumWidthRequest = ThumbnailPreviewWidth,
            MinimumHeightRequest = ThumbnailPreviewHeight,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var cacheKey = BuildThumbnailCacheKey(pageIndex);
        if (_thumbnailSourceCache.TryGetValue(cacheKey, out var cachedSource))
        {
            previewImage.Source = cachedSource;
        }
        else
        {
            _ = LoadThumbnailForPageAsync(pageIndex, cacheKey, previewImage, token, overlaySnapshots);
        }

        var item = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(8, 8, 8, 6),
            Content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Border
                    {
                        WidthRequest = 122,
                        HeightRequest = 172,
                        HorizontalOptions = LayoutOptions.Center,
                        StrokeThickness = 1,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                        Padding = 2,
                        Content = previewImage
                    },
                    new Label
                    {
                        Text = titleOverride ?? TF("ThumbnailPageFormat", "Page {0}", pageIndex + 1),
                        FontSize = 11,
                        HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = ThemePrimaryText
                    }
                }
            }
        };

        if (_thumbnailLayoutMode == ThumbnailLayoutMode.Grid)
        {
            item.WidthRequest = 126;
            item.Margin = new Thickness(0, 0, 6, 6);
        }

        ApplyThumbnailItemSelectionVisual(item, isCurrent);
        if (!_thumbnailItemLookup.ContainsKey(pageIndex))
        {
            _thumbnailItemLookup[pageIndex] = item;
        }

        item.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                if (!EnsurePdfLoaded())
                    return;

                PdfViewer.GoToPage(pageIndex);
                _currentPageIndex = pageIndex;
                UpdateThumbnailSelection(pageIndex);
            })
        });

        return item;
    }

    private void OnThumbnailListModeClicked(object? sender, EventArgs e)
    {
        if (_thumbnailLayoutMode == ThumbnailLayoutMode.List)
            return;

        _thumbnailLayoutMode = ThumbnailLayoutMode.List;
        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
    }

    private void OnThumbnailGridModeClicked(object? sender, EventArgs e)
    {
        if (_thumbnailLayoutMode == ThumbnailLayoutMode.Grid)
            return;

        _thumbnailLayoutMode = ThumbnailLayoutMode.Grid;
        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
    }

    private async Task LoadThumbnailForPageAsync(
        int pageIndex,
        string cacheKey,
        Image target,
        CancellationToken token,
        IReadOnlyList<ThumbnailStrokeSnapshot>? overlaySnapshots)
    {
        if (!IsEditorInitialized || token.IsCancellationRequested)
            return;

        var gateEntered = false;
        try
        {
            await _thumbnailRenderSemaphore.WaitAsync(token).ConfigureAwait(false);
            gateEntered = true;

            var thumbnailStream = await PdfViewer
                .GetThumbnailAsync(pageIndex, ThumbnailRequestWidth, ThumbnailRequestHeight)
                .ConfigureAwait(false);
            if (thumbnailStream is null || token.IsCancellationRequested)
                return;

            byte[] bytes;
            using (thumbnailStream)
            using (var memory = new MemoryStream())
            {
                await thumbnailStream.CopyToAsync(memory, token).ConfigureAwait(false);
                if (memory.Length == 0 || token.IsCancellationRequested)
                    return;

                bytes = memory.ToArray();
            }

            if (_thumbnailIncludeInkOverlay
                && overlaySnapshots is { Count: > 0 }
                && !token.IsCancellationRequested)
            {
                var pageBounds = await GetCachedPageBoundsAsync(pageIndex).ConfigureAwait(false);
                if (pageBounds is PdfPageBounds bounds)
                {
                    var pageSnapshots = overlaySnapshots
                        .Where(snapshot => DoesSnapshotIntersectPage(snapshot, bounds))
                        .ToArray();
                    if (pageSnapshots.Length == 0)
                        goto publish_image;

                    var composedBytes = await Task
                        .Run(() => ComposeThumbnailWithInkOverlay(bytes, bounds, pageSnapshots, token), token)
                        .ConfigureAwait(false);
                    if (composedBytes is { Length: > 0 })
                    {
                        bytes = composedBytes;
                    }
                }
            }

        publish_image:
            if (token.IsCancellationRequested)
                return;

            var source = ImageSource.FromStream(() => new MemoryStream(bytes));
            _thumbnailSourceCache[cacheKey] = source;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    target.Source = source;
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
        finally
        {
            if (gateEntered)
            {
                _thumbnailRenderSemaphore.Release();
            }
        }
    }

    private IReadOnlyList<ThumbnailStrokeSnapshot> CaptureThumbnailStrokeSnapshots()
    {
        if (!IsEditorInitialized)
            return Array.Empty<ThumbnailStrokeSnapshot>();

        var snapshots = new List<ThumbnailStrokeSnapshot>();
        foreach (var layer in DrawingCanvas.Layers)
        {
            if (!layer.IsVisible || layer.Opacity <= 0.001f)
                continue;

            var layerOpacity = Math.Clamp(layer.Opacity, 0f, 1f);
            foreach (var stroke in layer.Strokes)
            {
                if (stroke.Points.Count < 2)
                    continue;

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

    private static bool TryGetStrokeBounds(
        DrawingStroke stroke,
        out float minX,
        out float minY,
        out float maxX,
        out float maxY)
    {
        minX = float.MaxValue;
        minY = float.MaxValue;
        maxX = float.MinValue;
        maxY = float.MinValue;
        if (stroke.Points.Count == 0)
            return false;

        foreach (var point in stroke.Points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        return true;
    }

    private static byte[]? ComposeThumbnailWithInkOverlay(
        byte[] baseThumbnailBytes,
        PdfPageBounds pageBounds,
        IReadOnlyList<ThumbnailStrokeSnapshot> snapshots,
        CancellationToken token,
        bool requireIntersection = true,
        float? overridePageOriginX = null,
        float? overridePageOriginY = null)
    {
        using var baseBitmap = SKBitmap.Decode(baseThumbnailBytes);
        if (baseBitmap is null || baseBitmap.Width <= 0 || baseBitmap.Height <= 0)
            return null;

        var imageInfo = new SKImageInfo(baseBitmap.Width, baseBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(imageInfo);
        if (surface is null)
            return null;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(baseBitmap, 0, 0);
        using var overlaySurface = SKSurface.Create(imageInfo);
        if (overlaySurface is null)
            return null;

        var overlayCanvas = overlaySurface.Canvas;
        overlayCanvas.Clear(SKColors.Transparent);

        var safePageWidth = Math.Max(1d, pageBounds.Width);
        var safePageHeight = Math.Max(1d, pageBounds.Height);
        var pageOriginX = overridePageOriginX ?? (float)pageBounds.X;
        var pageOriginY = overridePageOriginY ?? (float)pageBounds.Y;
        var scaleX = (float)(imageInfo.Width / safePageWidth);
        var scaleY = (float)(imageInfo.Height / safePageHeight);
        var translateX = -pageOriginX * scaleX;
        var translateY = -pageOriginY * scaleY;
        var strokeScale = Math.Max(0.12f, (Math.Abs(scaleX) + Math.Abs(scaleY)) * 0.5f);
        var transform = SKMatrix.CreateScaleTranslation(scaleX, scaleY, translateX, translateY);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        foreach (var snapshot in snapshots)
        {
            token.ThrowIfCancellationRequested();

            var stroke = snapshot.Stroke;
            if (stroke.Points.Count < 2 || (requireIntersection && !DoesSnapshotIntersectPage(snapshot, pageBounds)))
                continue;

            if (ShouldDrawThumbnailPressureStrokeSegments(stroke))
            {
                DrawThumbnailPressureStrokeSegments(overlayCanvas, stroke, snapshot.LayerOpacity, strokeScale, transform, paint);
                continue;
            }

            DrawThumbnailStrokeSegments(overlayCanvas, stroke, snapshot.LayerOpacity, strokeScale, transform, paint);
        }

        overlayCanvas.Flush();
        using var overlayImage = overlaySurface.Snapshot();
        canvas.DrawImage(overlayImage, 0, 0);
        canvas.Flush();
        using var composedImage = surface.Snapshot();
        using var encoded = composedImage.Encode(SKEncodedImageFormat.Png, 96);
        return encoded?.ToArray();
    }

    private static SKBlendMode GetThumbnailStrokeBlendMode(DrawingStroke stroke)
    {
        if (stroke.IsEraser)
            return SKBlendMode.Clear;

        if (stroke.BrushType == BrushType.Highlighter || stroke.BrushType == BrushType.Marker)
            return SKBlendMode.Multiply;

        return SKBlendMode.SrcOver;
    }

    private static bool DoesSnapshotIntersectPage(ThumbnailStrokeSnapshot snapshot, PdfPageBounds pageBounds)
    {
        var pageLeft = pageBounds.X;
        var pageTop = pageBounds.Y;
        var pageRight = pageBounds.X + pageBounds.Width;
        var pageBottom = pageBounds.Y + pageBounds.Height;
        return snapshot.MaxX >= pageLeft
            && snapshot.MinX <= pageRight
            && snapshot.MaxY >= pageTop
            && snapshot.MinY <= pageBottom;
    }

    private static bool ShouldDrawThumbnailPressureStrokeSegments(DrawingStroke stroke)
    {
        if (stroke.IsEraser || !stroke.Options.PressureEnabled || stroke.Points.Count < 2)
            return false;

        return stroke.BrushType == BrushType.Pen
            || stroke.BrushType == BrushType.Pencil
            || stroke.BrushType == BrushType.Watercolor;
    }

    private static void DrawThumbnailStrokeSegments(
        SKCanvas canvas,
        DrawingStroke stroke,
        float layerOpacity,
        float strokeScale,
        SKMatrix transform,
        SKPaint paint)
    {
        var alpha = stroke.IsEraser
            ? (byte)0
            : (byte)Math.Clamp((int)Math.Round(stroke.Color.Alpha * layerOpacity * stroke.Opacity), 0, 255);
        paint.Color = stroke.IsEraser ? SKColors.Transparent : stroke.Color.WithAlpha(alpha);
        paint.BlendMode = GetThumbnailStrokeBlendMode(stroke);
        paint.StrokeWidth = Math.Max(0.25f, stroke.StrokeWidth * strokeScale);
        using var mappedPath = new SKPath();
        stroke.CreatePath().Transform(transform, mappedPath);
        canvas.DrawPath(mappedPath, paint);
    }

    private static void DrawThumbnailPressureStrokeSegments(
        SKCanvas canvas,
        DrawingStroke stroke,
        float layerOpacity,
        float strokeScale,
        SKMatrix transform,
        SKPaint paint)
    {
        var alpha = (byte)Math.Clamp(
            (int)Math.Round(stroke.Color.Alpha * layerOpacity * stroke.Opacity),
            0,
            255);
        paint.Color = stroke.Color.WithAlpha(alpha);
        paint.BlendMode = GetThumbnailStrokeBlendMode(stroke);

        var minPressure = Math.Max(0.02f, stroke.Options.MinPressure);
        var maxPressure = Math.Max(minPressure + 0.01f, stroke.Options.MaxPressure);
        var smoothing = 0.18f + (Math.Clamp(stroke.Options.SmoothingFactor, 0f, 1f) * 0.48f);
        var streamline = Math.Clamp(stroke.Options.Streamline, 0f, 1f);
        var widthPressure = Math.Clamp(stroke.Points[0].Pressure, minPressure, maxPressure);
        for (var index = 1; index < stroke.Points.Count; index++)
        {
            var previous = stroke.Points[index - 1];
            var current = stroke.Points[index];
            var mappedPrev = MapPoint(transform, previous.X, previous.Y);
            var mappedCurrent = MapPoint(transform, current.X, current.Y);
            var dtMs = Math.Max(1L, current.Timestamp - previous.Timestamp);
            var dx = current.X - previous.X;
            var dy = current.Y - previous.Y;
            var distance = MathF.Sqrt((dx * dx) + (dy * dy));
            var velocity = distance / dtMs;
            var velocityFactor = Math.Clamp(1f - (velocity * (0.14f + (streamline * 0.35f))), 0.55f, 1.05f);
            var targetPressure = Math.Clamp(((previous.Pressure + current.Pressure) * 0.5f) * velocityFactor, minPressure, maxPressure);
            widthPressure = widthPressure + ((targetPressure - widthPressure) * smoothing);
            paint.StrokeWidth = Math.Max(0.25f, stroke.StrokeWidth * strokeScale * widthPressure);
            canvas.DrawLine(mappedPrev, mappedCurrent, paint);
        }
    }

    private static SKPoint MapPoint(SKMatrix matrix, float x, float y)
    {
        var mappedX = (matrix.ScaleX * x) + (matrix.SkewX * y) + matrix.TransX;
        var mappedY = (matrix.SkewY * x) + (matrix.ScaleY * y) + matrix.TransY;
        return new SKPoint(mappedX, mappedY);
    }

    private static bool DoesStrokeIntersectPage(DrawingStroke stroke, PdfPageBounds pageBounds)
    {
        if (stroke.Points.Count == 0)
            return false;

        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        foreach (var point in stroke.Points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        var pageLeft = pageBounds.X;
        var pageTop = pageBounds.Y;
        var pageRight = pageBounds.X + pageBounds.Width;
        var pageBottom = pageBounds.Y + pageBounds.Height;
        return maxX >= pageLeft
            && minX <= pageRight
            && maxY >= pageTop
            && minY <= pageBottom;
    }

}
