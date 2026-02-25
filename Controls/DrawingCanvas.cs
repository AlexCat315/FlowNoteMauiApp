using System.Collections.ObjectModel;
using FlowNoteMauiApp.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace FlowNoteMauiApp.Controls;

public class DrawingCanvas : SKCanvasView
{
    public enum TwoFingerSwipeDirection
    {
        PreviousPage,
        NextPage
    }

    public sealed class TwoFingerSwipeEventArgs : EventArgs
    {
        public TwoFingerSwipeEventArgs(TwoFingerSwipeDirection direction)
        {
            Direction = direction;
        }

        public TwoFingerSwipeDirection Direction { get; }
    }

    public static readonly BindableProperty LayersProperty =
        BindableProperty.Create(nameof(Layers), typeof(ObservableCollection<DrawingLayer>), typeof(DrawingCanvas),
            propertyChanged: OnLayersChanged);

    public static readonly BindableProperty CurrentLayerIndexProperty =
        BindableProperty.Create(nameof(CurrentLayerIndex), typeof(int), typeof(DrawingCanvas),
            defaultValue: 0, propertyChanged: OnCurrentLayerIndexChanged);

    public static readonly BindableProperty StrokeColorProperty =
        BindableProperty.Create(nameof(StrokeColor), typeof(SKColor), typeof(DrawingCanvas),
            defaultValue: SKColors.Black, propertyChanged: OnDrawingPropertyChanged);

    public static readonly BindableProperty StrokeWidthProperty =
        BindableProperty.Create(nameof(StrokeWidth), typeof(float), typeof(DrawingCanvas),
            defaultValue: 3f, propertyChanged: OnDrawingPropertyChanged);

    public static readonly BindableProperty IsErasingProperty =
        BindableProperty.Create(nameof(IsErasing), typeof(bool), typeof(DrawingCanvas),
            defaultValue: false, propertyChanged: OnDrawingPropertyChanged);

    public static readonly BindableProperty IsHighlighterProperty =
        BindableProperty.Create(nameof(IsHighlighter), typeof(bool), typeof(DrawingCanvas),
            defaultValue: false, propertyChanged: OnDrawingPropertyChanged);

    public static readonly BindableProperty ScrollXProperty =
        BindableProperty.Create(nameof(ScrollX), typeof(double), typeof(DrawingCanvas),
            defaultValue: 0.0, propertyChanged: OnScrollChanged);

    public static readonly BindableProperty ScrollYProperty =
        BindableProperty.Create(nameof(ScrollY), typeof(double), typeof(DrawingCanvas),
            defaultValue: 0.0, propertyChanged: OnScrollChanged);

    public static readonly BindableProperty ViewportZoomProperty =
        BindableProperty.Create(nameof(ViewportZoom), typeof(float), typeof(DrawingCanvas),
            defaultValue: 1f, propertyChanged: OnScrollChanged);

    public static readonly BindableProperty IsPenModeProperty =
        BindableProperty.Create(nameof(IsPenMode), typeof(bool), typeof(DrawingCanvas),
            defaultValue: true, propertyChanged: OnPenModeChanged);

    public static readonly BindableProperty EnableDrawingProperty =
        BindableProperty.Create(nameof(EnableDrawing), typeof(bool), typeof(DrawingCanvas),
            defaultValue: false, propertyChanged: OnEnableDrawingChanged);

    public static readonly BindableProperty EnableTwoFingerSwipeNavigationProperty =
        BindableProperty.Create(nameof(EnableTwoFingerSwipeNavigation), typeof(bool), typeof(DrawingCanvas),
            defaultValue: true);

    private readonly Stack<StrokeHistoryEntry> _undoStack = new();
    private readonly Stack<StrokeHistoryEntry> _redoStack = new();
    private readonly HashSet<long> _activeTouchIds = new();
    private readonly Dictionary<long, SKPoint> _activeTouchPoints = new();
    private DrawingStroke? _currentStroke;
    private bool _isDrawing;
    private bool _suspendDrawingUntilTouchesReleased;
    private bool _isTwoFingerGestureActive;
    private SKPoint _twoFingerAnchor;
    private const float TwoFingerSwipeThreshold = 120f;

    public event EventHandler? StrokeCommitted;
    public event EventHandler<TwoFingerSwipeEventArgs>? TwoFingerSwipe;

    private readonly record struct StrokeHistoryEntry(int LayerIndex, DrawingStroke Stroke);

    public DrawingCanvas()
    {
        Layers = new ObservableCollection<DrawingLayer>
        {
            new() { Name = "Layer 1" }
        };

        IgnorePixelScaling = false;
        EnableTouchEvents = true;
        Touch += OnCanvasTouch;
    }

    public ObservableCollection<DrawingLayer> Layers
    {
        get => (ObservableCollection<DrawingLayer>)GetValue(LayersProperty);
        set => SetValue(LayersProperty, value);
    }

    public int CurrentLayerIndex
    {
        get => (int)GetValue(CurrentLayerIndexProperty);
        set => SetValue(CurrentLayerIndexProperty, value);
    }

    public SKColor StrokeColor
    {
        get => (SKColor)GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    public float StrokeWidth
    {
        get => (float)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public bool IsErasing
    {
        get => (bool)GetValue(IsErasingProperty);
        set => SetValue(IsErasingProperty, value);
    }

    public bool IsHighlighter
    {
        get => (bool)GetValue(IsHighlighterProperty);
        set => SetValue(IsHighlighterProperty, value);
    }

    public double ScrollX
    {
        get => (double)GetValue(ScrollXProperty);
        set => SetValue(ScrollXProperty, value);
    }

    public double ScrollY
    {
        get => (double)GetValue(ScrollYProperty);
        set => SetValue(ScrollYProperty, value);
    }

    public float ViewportZoom
    {
        get => (float)GetValue(ViewportZoomProperty);
        set => SetValue(ViewportZoomProperty, value);
    }

    public bool IsPenMode
    {
        get => (bool)GetValue(IsPenModeProperty);
        set => SetValue(IsPenModeProperty, value);
    }

    public bool EnableDrawing
    {
        get => (bool)GetValue(EnableDrawingProperty);
        set => SetValue(EnableDrawingProperty, value);
    }

    public bool EnableTwoFingerSwipeNavigation
    {
        get => (bool)GetValue(EnableTwoFingerSwipeNavigationProperty);
        set => SetValue(EnableTwoFingerSwipeNavigationProperty, value);
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void AddLayer(string name = "Layer")
    {
        var newLayer = new DrawingLayer
        {
            Name = $"{name} {Layers.Count + 1}"
        };
        Layers.Add(newLayer);
        CurrentLayerIndex = Layers.Count - 1;
        InvalidateSurface();
        StrokeCommitted?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveLayer(int index)
    {
        if (index < 0 || index >= Layers.Count)
            return;
        if (Layers.Count <= 1)
            return;

        Layers.RemoveAt(index);
        CurrentLayerIndex = Math.Clamp(CurrentLayerIndex, 0, Layers.Count - 1);
        ResetHistory();
        InvalidateSurface();
        StrokeCommitted?.Invoke(this, EventArgs.Empty);
    }

    public void ClearCurrentLayer()
    {
        if (Layers.Count == 0 || CurrentLayerIndex < 0 || CurrentLayerIndex >= Layers.Count)
            return;

        Layers[CurrentLayerIndex].Clear();
        ResetHistory();
        InvalidateSurface();
        StrokeCommitted?.Invoke(this, EventArgs.Empty);
    }

    public void ClearAllLayers()
    {
        foreach (var layer in Layers)
        {
            layer.Clear();
        }
        ResetHistory();
        InvalidateSurface();
        StrokeCommitted?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
            return;

        var entry = _undoStack.Pop();
        if (entry.LayerIndex < 0 || entry.LayerIndex >= Layers.Count)
            return;

        var layer = Layers[entry.LayerIndex];
        var removed = false;
        if (layer.Strokes.Count > 0 && ReferenceEquals(layer.Strokes[^1], entry.Stroke))
        {
            layer.Strokes.RemoveAt(layer.Strokes.Count - 1);
            removed = true;
        }
        else
        {
            removed = layer.Strokes.Remove(entry.Stroke);
        }

        if (!removed)
            return;

        _redoStack.Push(entry);
        InvalidateSurface();
        StrokeCommitted?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
            return;

        var entry = _redoStack.Pop();
        if (entry.LayerIndex < 0 || entry.LayerIndex >= Layers.Count)
            return;

        Layers[entry.LayerIndex].Strokes.Add(entry.Stroke);
        _undoStack.Push(entry);
        InvalidateSurface();
        StrokeCommitted?.Invoke(this, EventArgs.Empty);
    }

    public DrawingDocumentState ExportState()
    {
        var state = new DrawingDocumentState
        {
            CurrentLayerIndex = Math.Clamp(CurrentLayerIndex, 0, Math.Max(0, Layers.Count - 1)),
            Layers = new List<DrawingLayerState>(Layers.Count)
        };

        foreach (var layer in Layers)
        {
            var layerState = new DrawingLayerState
            {
                Name = layer.Name,
                IsVisible = layer.IsVisible,
                IsLocked = layer.IsLocked,
                Opacity = layer.Opacity,
                BackgroundColor = ToArgb(layer.BackgroundColor)
            };

            foreach (var stroke in layer.Strokes)
            {
                var strokeState = new DrawingStrokeState
                {
                    Color = ToArgb(stroke.Color),
                    StrokeWidth = stroke.StrokeWidth,
                    Opacity = stroke.Opacity,
                    IsEraser = stroke.IsEraser,
                    BrushType = stroke.BrushType,
                    PressureEnabled = stroke.Options.PressureEnabled,
                    SmoothingEnabled = stroke.Options.SmoothingEnabled,
                    SmoothingFactor = stroke.Options.SmoothingFactor,
                    MinPressure = stroke.Options.MinPressure,
                    MaxPressure = stroke.Options.MaxPressure,
                    TaperStart = stroke.Options.TaperStart,
                    TaperEnd = stroke.Options.TaperEnd,
                    Streamline = stroke.Options.Streamline,
                    Points = stroke.Points
                        .Select(p => new DrawingPoint(p.X, p.Y, p.Pressure, p.Timestamp))
                        .ToList()
                };
                layerState.Strokes.Add(strokeState);
            }

            state.Layers.Add(layerState);
        }

        return state;
    }

    public void ImportState(DrawingDocumentState? state)
    {
        if (state == null || state.Layers.Count == 0)
        {
            Layers = new ObservableCollection<DrawingLayer>
            {
                new() { Name = "Layer 1" }
            };
            CurrentLayerIndex = 0;
            ResetHistory();
            InvalidateSurface();
            return;
        }

        var loadedLayers = new ObservableCollection<DrawingLayer>();
        foreach (var layerState in state.Layers)
        {
            var layer = new DrawingLayer
            {
                Name = string.IsNullOrWhiteSpace(layerState.Name) ? "Layer" : layerState.Name,
                IsVisible = layerState.IsVisible,
                IsLocked = layerState.IsLocked,
                Opacity = layerState.Opacity,
                BackgroundColor = FromArgb(layerState.BackgroundColor)
            };

            foreach (var strokeState in layerState.Strokes)
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

                layer.Strokes.Add(stroke);
            }

            loadedLayers.Add(layer);
        }

        Layers = loadedLayers;
        CurrentLayerIndex = Math.Clamp(state.CurrentLayerIndex, 0, Layers.Count - 1);
        ResetHistory();
        InvalidateSurface();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var zoom = Math.Max(0.1f, ViewportZoom);
        canvas.SetMatrix(SKMatrix.CreateScaleTranslation(
            zoom,
            zoom,
            -(float)ScrollX,
            -(float)ScrollY));

        if (Layers.Count == 0)
            return;

        using var layerPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        for (var i = 0; i < Layers.Count; i++)
        {
            var layer = Layers[i];
            if (!layer.IsVisible)
                continue;

            foreach (var stroke in layer.Strokes)
            {
                if (stroke.Points.Count < 2)
                    continue;

                layerPaint.Color = stroke.Color.WithAlpha((byte)(stroke.Color.Alpha * layer.Opacity * stroke.Opacity));
                layerPaint.StrokeWidth = stroke.StrokeWidth;
                layerPaint.BlendMode = GetBlendMode(stroke);

                var path = stroke.CreatePath();
                canvas.DrawPath(path, layerPaint);
            }

            if (i == CurrentLayerIndex && _currentStroke != null && _currentStroke.Points.Count >= 2)
            {
                layerPaint.Color = _currentStroke.Color;
                layerPaint.StrokeWidth = _currentStroke.StrokeWidth;
                layerPaint.BlendMode = GetBlendMode(_currentStroke);

                var path = _currentStroke.CreatePath();
                canvas.DrawPath(path, layerPaint);
            }
        }
    }

    private static void OnLayersChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DrawingCanvas canvas)
        {
            canvas.InvalidateSurface();
        }
    }

    private static void OnCurrentLayerIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DrawingCanvas canvas)
        {
            canvas.InvalidateSurface();
        }
    }

    private static void OnDrawingPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DrawingCanvas canvas)
        {
            canvas.InvalidateSurface();
        }
    }

    private static void OnScrollChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DrawingCanvas canvas)
        {
            canvas.InvalidateSurface();
        }
    }

    private static void OnPenModeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DrawingCanvas canvas)
        {
            canvas.ResetTouchTracking();
        }
    }

    private static void OnEnableDrawingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DrawingCanvas canvas)
        {
            canvas.InputTransparent = !(bool)newValue;
            if (!(bool)newValue)
            {
                canvas.ResetTouchTracking();
            }
        }
    }

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (!EnableDrawing)
        {
            e.Handled = false;
            return;
        }

        TrackTouch(e);
        if (_suspendDrawingUntilTouchesReleased && _activeTouchIds.Count == 0)
        {
            _suspendDrawingUntilTouchesReleased = false;
        }

        // Finger/capacitive mode: one-finger writes, two-finger gestures are handed to the PDF for navigation.
        if (!IsPenMode && _activeTouchIds.Count >= 2)
        {
            CancelCurrentStroke();
            _suspendDrawingUntilTouchesReleased = true;
            if (EnableTwoFingerSwipeNavigation)
            {
                HandleTwoFingerGesture();
                e.Handled = true;
            }
            else
            {
                // Let the underlying PDF view handle continuous-mode pinch/scroll.
                e.Handled = false;
            }
            InvalidateSurface();
            return;
        }

        if (_suspendDrawingUntilTouchesReleased)
        {
            e.Handled = true;
            return;
        }

        var isStylus = e.DeviceType == SKTouchDeviceType.Pen;
        if (IsPenMode && !isStylus)
        {
            e.Handled = false;
            return;
        }

        if (Layers.Count == 0 || CurrentLayerIndex < 0 || CurrentLayerIndex >= Layers.Count)
        {
            e.Handled = false;
            return;
        }

        var currentLayer = Layers[CurrentLayerIndex];
        if (currentLayer.IsLocked)
        {
            e.Handled = false;
            return;
        }

        var zoom = Math.Max(0.1f, ViewportZoom);
        var location = new DrawingPoint(
            (e.Location.X + (float)ScrollX) / zoom,
            (e.Location.Y + (float)ScrollY) / zoom,
            e.Pressure <= 0 ? 1f : e.Pressure);

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                StartDrawing(location);
                break;
            case SKTouchAction.Moved:
                ContinueDrawing(location);
                break;
            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                EndDrawing();
                break;
        }

        InvalidateSurface();
        e.Handled = true;
    }

    private void TrackTouch(SKTouchEventArgs e)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _activeTouchIds.Add(e.Id);
                _activeTouchPoints[e.Id] = e.Location;
                if (!IsPenMode && _activeTouchIds.Count >= 2 && !_isTwoFingerGestureActive)
                {
                    _isTwoFingerGestureActive = true;
                    _twoFingerAnchor = GetTouchCenter();
                }
                break;
            case SKTouchAction.Moved:
                if (_activeTouchIds.Contains(e.Id))
                {
                    _activeTouchPoints[e.Id] = e.Location;
                }
                break;
            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                _activeTouchIds.Remove(e.Id);
                _activeTouchPoints.Remove(e.Id);
                if (_activeTouchIds.Count < 2)
                {
                    _isTwoFingerGestureActive = false;
                }
                break;
        }
    }

    private void ResetTouchTracking()
    {
        _activeTouchIds.Clear();
        _activeTouchPoints.Clear();
        _suspendDrawingUntilTouchesReleased = false;
        _isTwoFingerGestureActive = false;
        CancelCurrentStroke();
    }

    private SKPoint GetTouchCenter()
    {
        if (_activeTouchPoints.Count == 0)
            return SKPoint.Empty;

        float x = 0;
        float y = 0;
        foreach (var point in _activeTouchPoints.Values)
        {
            x += point.X;
            y += point.Y;
        }

        var count = _activeTouchPoints.Count;
        return new SKPoint(x / count, y / count);
    }

    private void HandleTwoFingerGesture()
    {
        if (_activeTouchPoints.Count < 2)
            return;

        var center = GetTouchCenter();
        if (!_isTwoFingerGestureActive)
        {
            _isTwoFingerGestureActive = true;
            _twoFingerAnchor = center;
            return;
        }

        var deltaX = center.X - _twoFingerAnchor.X;
        var deltaY = center.Y - _twoFingerAnchor.Y;

        if (Math.Abs(deltaY) < TwoFingerSwipeThreshold || Math.Abs(deltaY) <= Math.Abs(deltaX))
            return;

        var direction = deltaY < 0
            ? TwoFingerSwipeDirection.NextPage
            : TwoFingerSwipeDirection.PreviousPage;

        _twoFingerAnchor = center;
        TwoFingerSwipe?.Invoke(this, new TwoFingerSwipeEventArgs(direction));
    }

    private void CancelCurrentStroke()
    {
        _isDrawing = false;
        _currentStroke = null;
    }

    private void StartDrawing(DrawingPoint point)
    {
        _isDrawing = true;

        var brushType = IsErasing
            ? BrushType.Eraser
            : IsHighlighter
                ? BrushType.Highlighter
                : BrushType.Pen;

        _currentStroke = new DrawingStroke
        {
            Color = IsErasing ? SKColors.Transparent : StrokeColor,
            StrokeWidth = IsHighlighter ? StrokeWidth * 2.4f : StrokeWidth,
            IsEraser = IsErasing,
            Opacity = IsHighlighter ? 0.28f : 1f,
            BrushType = brushType,
            Options = new StrokeOptions
            {
                PressureEnabled = !IsErasing && !IsHighlighter,
                SmoothingEnabled = true,
                SmoothingFactor = IsHighlighter ? 0.25f : 0.45f,
                MinPressure = 0.1f,
                MaxPressure = 1f,
                Streamline = IsHighlighter ? 0.15f : 0.35f
            }
        };
        _currentStroke.AddPoint(point);
    }

    private void ContinueDrawing(DrawingPoint point)
    {
        if (!_isDrawing || _currentStroke == null)
            return;

        _currentStroke.AddPoint(point);
    }

    private void EndDrawing()
    {
        if (!_isDrawing || _currentStroke == null)
            return;

        if (CurrentLayerIndex >= 0 && CurrentLayerIndex < Layers.Count && _currentStroke.Points.Count > 0)
        {
            var committedStroke = _currentStroke;
            Layers[CurrentLayerIndex].AddStroke(committedStroke);
            _undoStack.Push(new StrokeHistoryEntry(CurrentLayerIndex, committedStroke));
            _redoStack.Clear();
            StrokeCommitted?.Invoke(this, EventArgs.Empty);
        }

        _isDrawing = false;
        _currentStroke = null;
    }

    private static SKBlendMode GetBlendMode(DrawingStroke stroke)
    {
        if (stroke.IsEraser)
            return SKBlendMode.Clear;
        if (stroke.BrushType == BrushType.Highlighter)
            return SKBlendMode.Multiply;
        return SKBlendMode.SrcOver;
    }

    private void ResetHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    private static uint ToArgb(SKColor color)
    {
        return ((uint)color.Alpha << 24)
               | ((uint)color.Red << 16)
               | ((uint)color.Green << 8)
               | color.Blue;
    }

    private static SKColor FromArgb(uint value)
    {
        return new SKColor(
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF),
            (byte)((value >> 24) & 0xFF));
    }
}
