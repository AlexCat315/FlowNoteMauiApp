using SkiaSharp;
using Flow.PDFView;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Controls;
using FlowNoteMauiApp.Helpers;
using FlowNoteMauiApp.Models;
using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Maui.Devices;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private void ResetPdfPageBoundsCache()
    {
        _pageBoundsCache.Clear();
    }

    private bool CanDrawAtDocumentLocation(float documentX, float documentY)
    {
        if (_allowSideWriting)
            return true;

        if (_pageBoundsCache.IsEmpty)
            return true;

        PdfPageBounds? nearestByVerticalDistance = null;
        var nearestDistance = double.MaxValue;
        foreach (var bounds in _pageBoundsCache.Values)
        {
            var left = bounds.X;
            var right = bounds.X + bounds.Width;
            var top = bounds.Y;
            var bottom = bounds.Y + bounds.Height;

            if (documentX >= left
                && documentX <= right
                && documentY >= top
                && documentY <= bottom)
            {
                return true;
            }

            var verticalDistance = documentY < top
                ? top - documentY
                : documentY > bottom
                    ? documentY - bottom
                    : 0d;
            if (verticalDistance < nearestDistance)
            {
                nearestDistance = verticalDistance;
                nearestByVerticalDistance = bounds;
            }
        }

        if (nearestByVerticalDistance is not PdfPageBounds nearestBounds)
            return true;

        return documentX >= nearestBounds.X
            && documentX <= nearestBounds.X + nearestBounds.Width;
    }

    private async Task PrimeAllPageBoundsAsync()
    {
        if (!IsEditorInitialized || _totalPageCount <= 0)
            return;

        if (_currentPageIndex >= 0 && _currentPageIndex < _totalPageCount)
        {
            await GetCachedPageBoundsAsync(_currentPageIndex).ConfigureAwait(false);
        }

        for (var pageIndex = 0; pageIndex < _totalPageCount; pageIndex++)
        {
            if (pageIndex == _currentPageIndex)
                continue;
            await GetCachedPageBoundsAsync(pageIndex).ConfigureAwait(false);
        }
    }

    private void EnsureSideWritingGuardBoundsReady()
    {
        if (_allowSideWriting || !IsEditorInitialized || _totalPageCount <= 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await PrimeAllPageBoundsAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        });
    }

    private async Task<PdfPageBounds?> GetCachedPageBoundsAsync(int pageIndex)
    {
        if (!IsEditorInitialized || pageIndex < 0)
            return null;

        if (_pageBoundsCache.TryGetValue(pageIndex, out var cachedBounds))
            return cachedBounds;

        var pageBounds = await PdfViewer.GetPageBoundsAsync(pageIndex).ConfigureAwait(false);
        if (pageBounds is PdfPageBounds bounds)
        {
            _pageBoundsCache[pageIndex] = bounds;
            return bounds;
        }

        return null;
    }

    private async Task<IReadOnlyList<PdfPageBounds>> GetAvailablePageBoundsAsync()
    {
        if (!IsEditorInitialized || _totalPageCount <= 0)
            return Array.Empty<PdfPageBounds>();

        var bounds = new List<PdfPageBounds>(_totalPageCount);
        for (var pageIndex = 0; pageIndex < _totalPageCount; pageIndex++)
        {
            var cachedBounds = await GetCachedPageBoundsAsync(pageIndex);
            if (cachedBounds is not PdfPageBounds pageBounds)
                continue;

            bounds.Add(pageBounds);
        }

        return bounds;
    }

    private static bool IsPointInsideAnyPage(DrawingPoint point, IReadOnlyList<PdfPageBounds> pageBoundsList)
    {
        foreach (var bounds in pageBoundsList)
        {
            if (point.X >= bounds.X
                && point.X <= bounds.X + bounds.Width
                && point.Y >= bounds.Y
                && point.Y <= bounds.Y + bounds.Height)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreAllStrokePointsInsidePages(DrawingStroke stroke, IReadOnlyList<PdfPageBounds> pageBoundsList)
    {
        if (stroke.Points.Count == 0)
            return false;

        foreach (var point in stroke.Points)
        {
            if (!IsPointInsideAnyPage(point, pageBoundsList))
                return false;
        }

        return true;
    }

    private static List<DrawingStroke> BuildClippedStrokeSegments(DrawingStroke stroke, IReadOnlyList<PdfPageBounds> pageBoundsList)
    {
        var clippedStrokes = new List<DrawingStroke>();
        var currentSegmentPoints = new List<DrawingPoint>();

        void FlushSegment()
        {
            if (currentSegmentPoints.Count < 2)
            {
                currentSegmentPoints.Clear();
                return;
            }

            clippedStrokes.Add(CreateStrokeSegment(stroke, currentSegmentPoints));
            currentSegmentPoints.Clear();
        }

        foreach (var point in stroke.Points)
        {
            if (IsPointInsideAnyPage(point, pageBoundsList))
            {
                currentSegmentPoints.Add(point);
                continue;
            }

            FlushSegment();
        }

        FlushSegment();
        return clippedStrokes;
    }

    private static DrawingStroke CreateStrokeSegment(DrawingStroke template, IReadOnlyList<DrawingPoint> points)
    {
        var segment = new DrawingStroke
        {
            Color = template.Color,
            StrokeWidth = template.StrokeWidth,
            Opacity = template.Opacity,
            IsEraser = template.IsEraser,
            BrushType = template.BrushType,
            Options = new StrokeOptions
            {
                PressureEnabled = template.Options.PressureEnabled,
                SmoothingEnabled = template.Options.SmoothingEnabled,
                SmoothingFactor = template.Options.SmoothingFactor,
                MinPressure = template.Options.MinPressure,
                MaxPressure = template.Options.MaxPressure,
                TaperEnabled = template.Options.TaperEnabled,
                TaperStart = template.Options.TaperStart,
                TaperEnd = template.Options.TaperEnd,
                Streamline = template.Options.Streamline
            }
        };

        foreach (var point in points)
        {
            segment.AddPoint(new DrawingPoint(point.X, point.Y, point.Pressure, point.Timestamp));
        }

        return segment;
    }

    private async Task<int> ClearCurrentPageInkAsync(int pageIndex)
    {
        if (!IsEditorInitialized || pageIndex < 0)
            return 0;

        var bounds = await GetCachedPageBoundsAsync(pageIndex);
        if (bounds is not PdfPageBounds pageBounds)
            return 0;

        var removedCount = 0;
        foreach (var layer in DrawingCanvas.Layers)
        {
            removedCount += layer.Strokes.RemoveAll(stroke => DoesStrokeIntersectPage(stroke, pageBounds));
        }

        return removedCount;
    }

    private void OnAddLayerClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.AddLayer();
        RefreshLayerList();
        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
        QueueInkSave();
    }

    private void OnDeleteLayerClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        if (DrawingCanvas.Layers.Count > 1)
        {
            DrawingCanvas.RemoveLayer(DrawingCanvas.CurrentLayerIndex);
            RefreshLayerList();
            InvalidateThumbnailCache();
            if (ThumbnailPanel.IsVisible)
            {
                RefreshThumbnailList();
            }
            QueueInkSave();
        }
    }

    private void RefreshLayerList()
    {
        LayerList.Children.Clear();
        if (!IsEditorInitialized)
            return;

        for (int i = 0; i < DrawingCanvas.Layers.Count; i++)
        {
            var layer = DrawingCanvas.Layers[i];
            var isSelected = i == DrawingCanvas.CurrentLayerIndex;
            var layerIndex = i;
            var palette = Palette;

            var bgColor = isSelected
                ? palette.LayerSelectedBackground
                : Colors.Transparent;

            var layerItem = new Border
            {
                BackgroundColor = bgColor,
                Padding = new Thickness(10, 8),
                Stroke = isSelected
                    ? palette.LayerSelectedBorder
                    : palette.LayerNormalBorder,
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }
            };

            var stack = new HorizontalStackLayout
            {
                Spacing = 8
            };

            var visibilityIcon = new ImageButton
            {
                Source = layer.IsVisible ? "icon_eye.png" : "icon_eye_off.png",
                WidthRequest = 14,
                HeightRequest = 14,
                Padding = 2,
                CornerRadius = 9,
                BackgroundColor = Colors.Transparent,
                Command = new Command(() =>
                {
                    layer.IsVisible = !layer.IsVisible;
                    DrawingCanvas.InvalidateSurface();
                    RefreshLayerList();
                    InvalidateThumbnailCache();
                    if (ThumbnailPanel.IsVisible)
                    {
                        RefreshThumbnailList();
                    }
                    QueueInkSave();
                })
            };
            RegisterMicroInteraction(visibilityIcon);

            var label = new Label
            {
                Text = layer.Name,
                VerticalOptions = LayoutOptions.Center,
                FontFamily = "OpenSansSemibold",
                FontSize = 13,
                TextColor = ThemePrimaryText
            };

            stack.Children.Add(visibilityIcon);
            stack.Children.Add(label);

            layerItem.Content = stack;
            layerItem.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    DrawingCanvas.CurrentLayerIndex = layerIndex;
                    RefreshLayerList();
                })
            });

            LayerList.Children.Add(layerItem);
        }
    }

    private void OnFingerDrawToggled(object? sender, ToggledEventArgs e)
    {
        if (_isUpdatingFingerDrawSwitch)
            return;

        ApplyInputMode(
            e.Value ? DrawingInputMode.FingerCapacitive : DrawingInputMode.PenStylus,
            showStatus: IsEditorInitialized,
            activateDrawing: false);
    }

    private void OnTextToolClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("StatusTextToolSoon", "Text tool is coming soon."));
    }

    private void OnImageToolClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("StatusImageToolSoon", "Image insert is coming soon."));
    }

    private void OnShapeToolClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("StatusShapeToolSoon", "Shape tool is coming soon."));
    }

    private void OnDrawingCanvasTwoFingerSwipe(object? sender, DrawingCanvas.TwoFingerSwipeEventArgs e)
    {
        if (!EnsurePdfLoaded())
            return;

        if (PdfViewer.DisplayMode != Flow.PDFView.Abstractions.PdfDisplayMode.SinglePage)
            return;

        LogInputGesture($"two-finger-swipe direction={e.Direction} page={_currentPageIndex + 1}/{Math.Max(1, _totalPageCount)}");

        if (e.Direction == DrawingCanvas.TwoFingerSwipeDirection.NextPage)
        {
            if (_currentPageIndex + 1 < _totalPageCount)
            {
                PdfViewer.GoToPage(_currentPageIndex + 1);
            }
            return;
        }

        if (_currentPageIndex > 0)
        {
            PdfViewer.GoToPage(_currentPageIndex - 1);
        }
    }

    private void OnDrawingCanvasTwoFingerPan(object? sender, DrawingCanvas.TwoFingerPanEventArgs e)
    {
        if (!EnsurePdfLoaded())
            return;

        if (_drawingInputMode == DrawingInputMode.TapRead)
            return;

        if (e.Phase == DrawingCanvas.TwoFingerPanPhase.Begin)
        {
            LogInputGesture($"pan-begin wheel={e.IsWheelInput} mode={_drawingInputMode}");
        }

        if (e.Phase == DrawingCanvas.TwoFingerPanPhase.End)
        {
            LogInputGesture($"pan-end wheel={e.IsWheelInput}");
            return;
        }

        if (_drawingInputMode == DrawingInputMode.PenStylus && e.HasZoom && !e.IsWheelInput)
        {
            PdfViewer.ZoomBy(e.ScaleFactor, e.CenterX, e.CenterY);
        }

        if (!e.HasPan)
            return;

        var adjustedX = e.DeltaX;
        var adjustedY = e.DeltaY;
        PdfViewer.PanBy(adjustedX, adjustedY);

        var now = DateTime.UtcNow;
        var shouldLogPanUpdate = (now - _lastPanUpdateLogUtc).TotalMilliseconds >= 90
            || Math.Abs(adjustedX) >= 90
            || Math.Abs(adjustedY) >= 90
            || e.HasZoom;
        if (shouldLogPanUpdate && (Math.Abs(adjustedX) > 0.1 || Math.Abs(adjustedY) > 0.1 || e.HasZoom))
        {
            _lastPanUpdateLogUtc = now;
            LogInputGesture($"pan-update wheel={e.IsWheelInput} dx={adjustedX:0.0} dy={adjustedY:0.0} zoom={e.ScaleFactor:0.000}");
        }
    }

    private static void StopTwoFingerInertia() { }

    [Conditional("DEBUG")]
    private static void LogInputGesture(string message)
    {
        Debug.WriteLine($"[FlowNote Gesture] {message}");
    }

    private void OnDrawingStrokeStarted(object? sender, EventArgs e)
    {
        StopTwoFingerInertia();
    }

    private async void OnDrawingStrokeFinalized(object? sender, DrawingCanvas.StrokeFinalizedEventArgs e)
    {
        if (!IsEditorInitialized || e.Stroke.Points.Count == 0)
            return;

        var pageBounds = await GetAvailablePageBoundsAsync();
        if (pageBounds.Count == 0)
            return;

        if (AreAllStrokePointsInsidePages(e.Stroke, pageBounds))
            return;

        var clippedStrokes = BuildClippedStrokeSegments(e.Stroke, pageBounds);
        if (!DrawingCanvas.ReplaceStroke(e.LayerIndex, e.Stroke, clippedStrokes))
            return;

        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
        QueueInkSave();
    }

    private void OnDrawingStrokeCommitted(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentNoteId))
        {
            _liveInkRevisionByNoteId.AddOrUpdate(
                _currentNoteId,
                _ => 1L,
                (_, previous) => previous + 1L);
            InvalidateHomeCoverCacheForNote(_currentNoteId);
        }

        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
        QueueInkSave();
    }

    private void QueueInkSave()
    {
        if (string.IsNullOrWhiteSpace(_currentNoteId))
            return;

        _hasUnsavedInkChanges = true;
        _inkSaveDebounce?.Cancel();
        _inkSaveDebounce?.Dispose();
        _inkSaveDebounce = new CancellationTokenSource();
        var token = _inkSaveDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(450, token);
                if (token.IsCancellationRequested)
                    return;

                await SaveCurrentDrawingStateAsync();
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task SaveCurrentDrawingStateAsync()
    {
        if (!IsEditorInitialized || string.IsNullOrWhiteSpace(_currentNoteId))
            return;

        if (!_hasUnsavedInkChanges)
            return;

        try
        {
            var state = await MainThread
                .InvokeOnMainThreadAsync(() => DrawingCanvas.ExportState())
                .ConfigureAwait(false);
            await _drawingPersistenceService.SaveAsync(_currentNoteId, state);
            InvalidateHomeCoverCacheForNote(_currentNoteId);
            _hasUnsavedInkChanges = false;
        }
        catch
        {
        }
    }

    private async Task LoadDrawingForCurrentNoteAsync()
    {
        if (!EnsureDrawingReady())
            return;

        if (string.IsNullOrWhiteSpace(_currentNoteId))
        {
            DrawingCanvas.ImportState(null);
            RefreshLayerList();
            _hasUnsavedInkChanges = false;
            return;
        }

        var state = await _drawingPersistenceService.LoadAsync(_currentNoteId);
        DrawingCanvas.ImportState(state);
        RefreshLayerList();
        _hasUnsavedInkChanges = false;
    }
}
