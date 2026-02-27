using System.Collections.ObjectModel;
using System.Diagnostics;
using FlowNoteMauiApp.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
#if IOS || MACCATALYST
using UIKit;
#endif

namespace FlowNoteMauiApp.Controls;

public class DrawingCanvas : SKCanvasView
{
    private enum TwoFingerGestureIntent
    {
        None,
        Pan,
        Zoom
    }

    public enum TwoFingerPanPhase
    {
        Begin,
        Update,
        End
    }

    public enum TwoFingerSwipeDirection
    {
        PreviousPage,
        NextPage
    }

    public enum EraserMode
    {
        Pixel,
        Stroke,
        Lasso
    }

    public sealed class TwoFingerSwipeEventArgs : EventArgs
    {
        public TwoFingerSwipeEventArgs(TwoFingerSwipeDirection direction)
        {
            Direction = direction;
        }

        public TwoFingerSwipeDirection Direction { get; }
    }

    public sealed class TwoFingerPanEventArgs : EventArgs
    {
        public TwoFingerPanEventArgs(
            float deltaX,
            float deltaY,
            float scaleFactor,
            float centerX,
            float centerY,
            TwoFingerPanPhase phase,
            bool isWheelInput = false)
        {
            DeltaX = deltaX;
            DeltaY = deltaY;
            ScaleFactor = scaleFactor;
            CenterX = centerX;
            CenterY = centerY;
            Phase = phase;
            IsWheelInput = isWheelInput;

            HasPan = Math.Abs(deltaX) > float.Epsilon || Math.Abs(deltaY) > float.Epsilon;
            HasZoom = Math.Abs(scaleFactor - 1f) > float.Epsilon;
        }

        public float DeltaX { get; }
        public float DeltaY { get; }
        public float ScaleFactor { get; }
        public float CenterX { get; }
        public float CenterY { get; }
        public TwoFingerPanPhase Phase { get; }
        public bool HasPan { get; }
        public bool HasZoom { get; }
        public bool IsWheelInput { get; }
    }

    public sealed class StrokeFinalizedEventArgs : EventArgs
    {
        public StrokeFinalizedEventArgs(int layerIndex, DrawingStroke stroke)
        {
            LayerIndex = layerIndex;
            Stroke = stroke;
        }

        public int LayerIndex { get; }
        public DrawingStroke Stroke { get; }
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

    public static readonly BindableProperty EraserBehaviorProperty =
        BindableProperty.Create(nameof(EraserBehavior), typeof(EraserMode), typeof(DrawingCanvas),
            defaultValue: EraserMode.Pixel, propertyChanged: OnDrawingPropertyChanged);

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

    public static readonly BindableProperty ForceInputTransparentProperty =
        BindableProperty.Create(nameof(ForceInputTransparent), typeof(bool), typeof(DrawingCanvas),
            defaultValue: false, propertyChanged: OnForceInputTransparentChanged);

    public static readonly BindableProperty EnableTwoFingerSwipeNavigationProperty =
        BindableProperty.Create(nameof(EnableTwoFingerSwipeNavigation), typeof(bool), typeof(DrawingCanvas),
            defaultValue: true);

    public static readonly BindableProperty ZoomAffectsStrokeWidthProperty =
        BindableProperty.Create(nameof(ZoomAffectsStrokeWidth), typeof(bool), typeof(DrawingCanvas),
            defaultValue: true, propertyChanged: OnDrawingPropertyChanged);

    public static readonly BindableProperty ActiveBrushTypeProperty =
        BindableProperty.Create(nameof(ActiveBrushType), typeof(BrushType), typeof(DrawingCanvas),
            defaultValue: BrushType.Pen, propertyChanged: OnDrawingPropertyChanged);

    public static readonly BindableProperty UsePressureSensitivityProperty =
        BindableProperty.Create(nameof(UsePressureSensitivity), typeof(bool), typeof(DrawingCanvas),
            defaultValue: true, propertyChanged: OnDrawingPropertyChanged);

    public static readonly BindableProperty PressureSensitivityProperty =
        BindableProperty.Create(nameof(PressureSensitivity), typeof(float), typeof(DrawingCanvas),
            defaultValue: 1f, propertyChanged: OnDrawingPropertyChanged);

    private readonly Stack<StrokeHistoryEntry> _undoStack = new();
    private readonly Stack<StrokeHistoryEntry> _redoStack = new();
    private readonly HashSet<long> _activeTouchIds = new();
    private readonly Dictionary<long, SKPoint> _activeTouchPoints = new();
    private DrawingStroke? _currentStroke;
    private bool _isDrawing;
    private bool _suspendDrawingUntilTouchesReleased;
    private bool _isTwoFingerGestureActive;
    private SKPoint _twoFingerAnchor;
    private float _twoFingerDistance;
    private const float TwoFingerSwipeThreshold = 120f;
    private const float TwoFingerPanMinDelta = 1.2f;
    private const float TwoFingerScaleMinDelta = 0.01f;
    private const float TwoFingerScaleMinDistanceDelta = 2f;
    private const float TwoFingerScaleSwitchDelta = 0.06f;
    private const float TwoFingerScaleSwitchDistanceDelta = 9f;
    private const float TwoFingerPanSwitchDelta = 10f;
    private const float TwoFingerZoomDominanceRatio = 1.25f;
    private const float MouseWheelPanStep = 16f;
    private const float StrokeFirstMoveJumpThresholdScreen = 130f;
    private const int StrokeFirstMoveJumpWindowMs = 90;
    private const float StrokeMaxSegmentLengthScreen = 24f;
    private const float StrokeInterpolationStepScreen = 9f;
    private TwoFingerGestureIntent _twoFingerGestureIntent = TwoFingerGestureIntent.None;
    private bool _suspendViewportInvalidation;
    private bool _pendingViewportInvalidation;
    private bool _isStrokeViewportLocked;
    private double _strokeViewportScrollX;
    private double _strokeViewportScrollY;
    private float _strokeViewportZoom = 1f;
    private DrawingPoint? _lastStrokePoint;
    private bool _hasStrokeSignificantMovement;
    private long _strokeStartTimestampMs;
    private DateTime _lastTouchMoveLogUtc = DateTime.MinValue;
    private readonly List<SKPoint> _lassoPoints = new();
    private bool _isLassoErasing;

    public Func<float, float, bool>? CanDrawAtViewPoint { get; set; }
    public Func<float, float, bool>? CanDrawAtDocumentPoint { get; set; }

    public event EventHandler? StrokeCommitted;
    public event EventHandler? StrokeStarted;
    public event EventHandler<StrokeFinalizedEventArgs>? StrokeFinalized;
    public event EventHandler<TwoFingerSwipeEventArgs>? TwoFingerSwipe;
    public event EventHandler<TwoFingerPanEventArgs>? TwoFingerPan;

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
        UpdateInputTransparency();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UpdatePlatformInteractionState();
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

    public EraserMode EraserBehavior
    {
        get => (EraserMode)GetValue(EraserBehaviorProperty);
        set => SetValue(EraserBehaviorProperty, value);
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

    public bool ForceInputTransparent
    {
        get => (bool)GetValue(ForceInputTransparentProperty);
        set => SetValue(ForceInputTransparentProperty, value);
    }

    public bool ZoomAffectsStrokeWidth
    {
        get => (bool)GetValue(ZoomAffectsStrokeWidthProperty);
        set => SetValue(ZoomAffectsStrokeWidthProperty, value);
    }

    public BrushType ActiveBrushType
    {
        get => (BrushType)GetValue(ActiveBrushTypeProperty);
        set => SetValue(ActiveBrushTypeProperty, value);
    }

    public bool UsePressureSensitivity
    {
        get => (bool)GetValue(UsePressureSensitivityProperty);
        set => SetValue(UsePressureSensitivityProperty, value);
    }

    public float PressureSensitivity
    {
        get => (float)GetValue(PressureSensitivityProperty);
        set => SetValue(PressureSensitivityProperty, value);
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void SetViewport(double scrollX, double scrollY, float viewportZoom)
    {
        var clampedZoom = Math.Max(0.1f, viewportZoom);
        _suspendViewportInvalidation = true;
        _pendingViewportInvalidation = false;

        try
        {
            if (Math.Abs(ScrollX - scrollX) > 0.01)
                ScrollX = scrollX;
            if (Math.Abs(ScrollY - scrollY) > 0.01)
                ScrollY = scrollY;
            if (Math.Abs(ViewportZoom - clampedZoom) > 0.0001f)
                ViewportZoom = clampedZoom;
        }
        finally
        {
            _suspendViewportInvalidation = false;
        }

        if (_pendingViewportInvalidation)
        {
            _pendingViewportInvalidation = false;
            InvalidateSurface();
        }
    }

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

    public bool RemoveStroke(DrawingStroke? stroke)
    {
        if (stroke is null)
            return false;

        for (var layerIndex = 0; layerIndex < Layers.Count; layerIndex++)
        {
            var layer = Layers[layerIndex];
            if (!layer.Strokes.Remove(stroke))
                continue;

            RemoveStrokeFromHistory(stroke);
            InvalidateSurface();
            return true;
        }

        return false;
    }

    public bool ReplaceStroke(int layerIndex, DrawingStroke? originalStroke, IReadOnlyList<DrawingStroke>? replacementStrokes)
    {
        if (originalStroke is null)
            return false;
        if (layerIndex < 0 || layerIndex >= Layers.Count)
            return false;

        var layer = Layers[layerIndex];
        var originalIndex = layer.Strokes.IndexOf(originalStroke);
        if (originalIndex < 0)
            return false;

        layer.Strokes.RemoveAt(originalIndex);
        RemoveStrokeFromHistory(originalStroke);

        if (replacementStrokes is { Count: > 0 })
        {
            var insertIndex = originalIndex;
            foreach (var stroke in replacementStrokes)
            {
                if (stroke is null || stroke.Points.Count < 2)
                    continue;

                layer.Strokes.Insert(insertIndex, stroke);
                _undoStack.Push(new StrokeHistoryEntry(layerIndex, stroke));
                insertIndex++;
            }
        }

        _redoStack.Clear();
        InvalidateSurface();
        return true;
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
        var strokeWidthScale = ZoomAffectsStrokeWidth ? 1f : (1f / zoom);

        for (var i = 0; i < Layers.Count; i++)
        {
            var layer = Layers[i];
            if (!layer.IsVisible)
                continue;

            foreach (var stroke in layer.Strokes)
            {
                if (stroke.Points.Count < 2)
                    continue;

                if (ShouldDrawPressureStrokeSegments(stroke))
                {
                    DrawPressureStrokeSegments(canvas, stroke, layer.Opacity, strokeWidthScale);
                }
                else
                {
                    layerPaint.Color = stroke.Color.WithAlpha((byte)(stroke.Color.Alpha * layer.Opacity * stroke.Opacity));
                    layerPaint.StrokeWidth = stroke.StrokeWidth * strokeWidthScale;
                    layerPaint.BlendMode = GetBlendMode(stroke);

                    var path = stroke.CreatePath();
                    canvas.DrawPath(path, layerPaint);
                }
            }

            if (i == CurrentLayerIndex && _currentStroke != null && _currentStroke.Points.Count >= 2)
            {
                if (ShouldDrawPressureStrokeSegments(_currentStroke))
                {
                    DrawPressureStrokeSegments(canvas, _currentStroke, 1f, strokeWidthScale);
                }
                else
                {
                    layerPaint.Color = _currentStroke.Color;
                    layerPaint.StrokeWidth = _currentStroke.StrokeWidth * strokeWidthScale;
                    layerPaint.BlendMode = GetBlendMode(_currentStroke);

                    var path = _currentStroke.CreatePath();
                    canvas.DrawPath(path, layerPaint);
                }
            }
        }

        if (_isLassoErasing && _lassoPoints.Count >= 2)
        {
            using var lassoPaint = new SKPaint
            {
                Color = new SKColor(90, 117, 255, 220),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.8f * strokeWidthScale,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new float[] { 8f * strokeWidthScale, 6f * strokeWidthScale }, 0)
            };
            using var lassoPath = new SKPath();
            lassoPath.MoveTo(_lassoPoints[0]);
            for (var i = 1; i < _lassoPoints.Count; i++)
            {
                lassoPath.LineTo(_lassoPoints[i]);
            }

            canvas.DrawPath(lassoPath, lassoPaint);
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
            if (canvas._suspendViewportInvalidation)
            {
                canvas._pendingViewportInvalidation = true;
                return;
            }

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
            canvas.UpdateInputTransparency();
            if (!(bool)newValue)
            {
                canvas.ResetTouchTracking();
            }
        }
    }

    private static void OnForceInputTransparentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DrawingCanvas canvas)
        {
            canvas.UpdateInputTransparency();
            if ((bool)newValue)
            {
                canvas.ResetTouchTracking();
            }
        }
    }

    private void UpdateInputTransparency()
    {
        var shouldPassThrough = ForceInputTransparent || !EnableDrawing;
        InputTransparent = shouldPassThrough;
        EnableTouchEvents = !shouldPassThrough;
        UpdatePlatformInteractionState();
        LogPointerState($"input-transparent={InputTransparent} touch-events={EnableTouchEvents} enable-drawing={EnableDrawing} force-pass-through={ForceInputTransparent}");
    }

    private void UpdatePlatformInteractionState()
    {
#if IOS || MACCATALYST
        if (Handler?.PlatformView is UIView platformView)
        {
            platformView.UserInteractionEnabled = !InputTransparent;
        }
#endif
    }

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (!EnableDrawing)
        {
            e.Handled = false;
            LogTouch("skip-disabled", e, "drawing-disabled");
            return;
        }

        var isStylus = e.DeviceType == SKTouchDeviceType.Pen;
        LogTouch("input", e, isStylus ? "stylus" : "pointer");

        if (e.ActionType == SKTouchAction.WheelChanged)
        {
            var panDeltaY = -e.WheelDelta * MouseWheelPanStep;
            if (Math.Abs(panDeltaY) > float.Epsilon)
            {
                TwoFingerPan?.Invoke(this, new TwoFingerPanEventArgs(
                    0f,
                    panDeltaY,
                    1f,
                    e.Location.X,
                    e.Location.Y,
                    TwoFingerPanPhase.Update,
                    isWheelInput: true));
            }

            e.Handled = true;
            LogTouch("wheel", e, "wheel-pan");
            return;
        }

        // Stylus mode: non-stylus pointers are passed through so PDF uses native pan/zoom behavior.
        if (IsPenMode && !isStylus)
        {
            e.Handled = false;
            LogTouch("pen-gesture", e, "native-pass-through");
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
            HandleTwoFingerGesture();
            e.Handled = true;
            InvalidateSurface();
            LogTouch("finger-gesture", e, "two-finger-pan-zoom");
            return;
        }

        if (_suspendDrawingUntilTouchesReleased)
        {
            e.Handled = true;
            LogTouch("finger-gesture", e, "suspended");
            return;
        }

        if (Layers.Count == 0 || CurrentLayerIndex < 0 || CurrentLayerIndex >= Layers.Count)
        {
            e.Handled = false;
            LogTouch("skip-layer", e, "no-active-layer");
            return;
        }

        var currentLayer = Layers[CurrentLayerIndex];
        if (currentLayer.IsLocked)
        {
            e.Handled = false;
            LogTouch("skip-layer", e, "layer-locked");
            return;
        }

        var usesAdvancedEraser = IsErasing && EraserBehavior != EraserMode.Pixel;
        if (e.ActionType == SKTouchAction.Pressed && !usesAdvancedEraser)
        {
            BeginStrokeViewportLock();
        }

        var effectiveScrollX = _isStrokeViewportLocked ? _strokeViewportScrollX : ScrollX;
        var effectiveScrollY = _isStrokeViewportLocked ? _strokeViewportScrollY : ScrollY;
        var zoom = _isStrokeViewportLocked ? _strokeViewportZoom : Math.Max(0.1f, ViewportZoom);
        var normalizedPressure = e.Pressure <= 0 ? 1f : e.Pressure;
        if (!UsePressureSensitivity || IsErasing)
        {
            normalizedPressure = 1f;
        }
        else
        {
            normalizedPressure = Math.Clamp(
                normalizedPressure * Math.Clamp(PressureSensitivity, 0.2f, 2.2f),
                0.05f,
                2f);
        }

        var location = new DrawingPoint(
            (e.Location.X + (float)effectiveScrollX) / zoom,
            (e.Location.Y + (float)effectiveScrollY) / zoom,
            normalizedPressure);

        var pointAllowed = CanDrawAtDocumentPoint?.Invoke(location.X, location.Y)
            ?? CanDrawAtViewPoint?.Invoke(e.Location.X, e.Location.Y)
            ?? true;
        if (!pointAllowed && e.ActionType == SKTouchAction.Pressed)
        {
            CancelCurrentStroke();
            e.Handled = true;
            LogTouch("skip-bounds", e, "outside-draw-zone");
            return;
        }

        if (!pointAllowed && e.ActionType == SKTouchAction.Moved)
        {
            if (_isDrawing)
            {
                EndDrawing();
                InvalidateSurface();
            }

            e.Handled = true;
            LogTouch("skip-bounds", e, "outside-draw-zone");
            return;
        }

        if (usesAdvancedEraser)
        {
            HandleAdvancedEraser(e, location);
            InvalidateSurface();
            e.Handled = true;
            LogTouch("erase", e, $"mode={EraserBehavior}");
            return;
        }

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
        LogTouch("draw", e, "stroke");
    }

    private void TrackTouch(SKTouchEventArgs e)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _activeTouchIds.Add(e.Id);
                _activeTouchPoints[e.Id] = e.Location;
                if (_activeTouchIds.Count >= 2 && !_isTwoFingerGestureActive)
                {
                    _isTwoFingerGestureActive = true;
                    _twoFingerAnchor = GetTouchCenter();
                    _twoFingerDistance = GetTwoTouchDistance();
                    _twoFingerGestureIntent = TwoFingerGestureIntent.None;
                    if (!EnableTwoFingerSwipeNavigation)
                    {
                        TwoFingerPan?.Invoke(this, new TwoFingerPanEventArgs(
                            0f,
                            0f,
                            1f,
                            _twoFingerAnchor.X,
                            _twoFingerAnchor.Y,
                            TwoFingerPanPhase.Begin));
                    }
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
                    if (!EnableTwoFingerSwipeNavigation && _isTwoFingerGestureActive)
                    {
                        TwoFingerPan?.Invoke(this, new TwoFingerPanEventArgs(
                            0f,
                            0f,
                            1f,
                            _twoFingerAnchor.X,
                            _twoFingerAnchor.Y,
                            TwoFingerPanPhase.End));
                    }

                    _isTwoFingerGestureActive = false;
                    _twoFingerDistance = 0f;
                    _twoFingerGestureIntent = TwoFingerGestureIntent.None;
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
        _twoFingerDistance = 0f;
        _twoFingerGestureIntent = TwoFingerGestureIntent.None;
        _isLassoErasing = false;
        _lassoPoints.Clear();
        CancelCurrentStroke();
    }

    [Conditional("DEBUG")]
    private void LogTouch(string phase, SKTouchEventArgs e, string branch)
    {
        if (e.DeviceType != SKTouchDeviceType.Mouse && e.DeviceType != SKTouchDeviceType.Pen)
            return;

        if (e.ActionType == SKTouchAction.Moved)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastTouchMoveLogUtc).TotalMilliseconds < 160)
                return;
            _lastTouchMoveLogUtc = now;
        }

        Debug.WriteLine(
            $"[FlowNote Pointer] phase={phase} action={e.ActionType} device={e.DeviceType} id={e.Id} " +
            $"x={e.Location.X:0.0} y={e.Location.Y:0.0} penMode={IsPenMode} enableDrawing={EnableDrawing} " +
            $"activeTouches={_activeTouchIds.Count} branch={branch}");
    }

    [Conditional("DEBUG")]
    private static void LogPointerState(string message)
    {
        Debug.WriteLine($"[FlowNote Pointer] {message}");
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

    private float GetTwoTouchDistance()
    {
        if (_activeTouchPoints.Count < 2)
            return 0f;

        using var enumerator = _activeTouchPoints.Values.GetEnumerator();
        if (!enumerator.MoveNext())
            return 0f;
        var first = enumerator.Current;

        if (!enumerator.MoveNext())
            return 0f;
        var second = enumerator.Current;

        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
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
            _twoFingerDistance = GetTwoTouchDistance();
            _twoFingerGestureIntent = TwoFingerGestureIntent.None;
            return;
        }

        var previousDistance = _twoFingerDistance;
        var distance = GetTwoTouchDistance();
        var deltaX = center.X - _twoFingerAnchor.X;
        var deltaY = center.Y - _twoFingerAnchor.Y;
        var scaleFactor = previousDistance > float.Epsilon && distance > float.Epsilon
            ? distance / previousDistance
            : 1f;
        var panMagnitude = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        var scaleDelta = MathF.Abs(scaleFactor - 1f);
        var distanceDelta = MathF.Abs(distance - previousDistance);

        _twoFingerAnchor = center;
        _twoFingerDistance = distance;

        if (!EnableTwoFingerSwipeNavigation)
        {
            var panCandidate = panMagnitude >= TwoFingerPanMinDelta;
            if (!IsPenMode)
            {
                if (!panCandidate)
                    return;

                TwoFingerPan?.Invoke(this, new TwoFingerPanEventArgs(
                    deltaX,
                    deltaY,
                    1f,
                    center.X,
                    center.Y,
                    TwoFingerPanPhase.Update));
                return;
            }

            var zoomCandidate = scaleDelta >= TwoFingerScaleMinDelta && distanceDelta >= TwoFingerScaleMinDistanceDelta;

            if (_twoFingerGestureIntent == TwoFingerGestureIntent.None)
            {
                if (!panCandidate && !zoomCandidate)
                    return;

                _twoFingerGestureIntent = zoomCandidate
                    && (!panCandidate || distanceDelta >= panMagnitude * TwoFingerZoomDominanceRatio)
                    ? TwoFingerGestureIntent.Zoom
                    : TwoFingerGestureIntent.Pan;
            }
            else if (_twoFingerGestureIntent == TwoFingerGestureIntent.Pan)
            {
                var zoomSwitch = scaleDelta >= TwoFingerScaleSwitchDelta
                    && distanceDelta >= TwoFingerScaleSwitchDistanceDelta
                    && distanceDelta >= panMagnitude * TwoFingerZoomDominanceRatio;
                if (zoomSwitch)
                {
                    _twoFingerGestureIntent = TwoFingerGestureIntent.Zoom;
                }
            }
            else
            {
                var panSwitch = panMagnitude >= TwoFingerPanSwitchDelta
                    && panMagnitude >= distanceDelta * TwoFingerZoomDominanceRatio;
                if (panSwitch)
                {
                    _twoFingerGestureIntent = TwoFingerGestureIntent.Pan;
                }
            }

            var emitPan = _twoFingerGestureIntent == TwoFingerGestureIntent.Pan && panCandidate;
            var emitZoom = _twoFingerGestureIntent == TwoFingerGestureIntent.Zoom && zoomCandidate;
            if (!emitPan && !emitZoom)
                return;

            if (!emitPan)
            {
                deltaX = 0f;
                deltaY = 0f;
            }

            if (!emitZoom)
            {
                scaleFactor = 1f;
            }

            TwoFingerPan?.Invoke(this, new TwoFingerPanEventArgs(
                deltaX,
                deltaY,
                scaleFactor,
                center.X,
                center.Y,
                TwoFingerPanPhase.Update));
            return;
        }

        if (Math.Abs(deltaY) < TwoFingerSwipeThreshold || Math.Abs(deltaY) <= Math.Abs(deltaX))
            return;

        var direction = deltaY < 0
            ? TwoFingerSwipeDirection.NextPage
            : TwoFingerSwipeDirection.PreviousPage;

        TwoFingerSwipe?.Invoke(this, new TwoFingerSwipeEventArgs(direction));
    }

    private void CancelCurrentStroke()
    {
        _isDrawing = false;
        _currentStroke = null;
        _lastStrokePoint = null;
        _hasStrokeSignificantMovement = false;
        _strokeStartTimestampMs = 0;
        EndStrokeViewportLock();
    }

    private void StartDrawing(DrawingPoint point)
    {
        _isDrawing = true;
        _lastStrokePoint = point;
        _hasStrokeSignificantMovement = false;
        _strokeStartTimestampMs = point.Timestamp;

        var brushType = IsErasing ? BrushType.Eraser : ActiveBrushType;
        var strokeWidth = StrokeWidth;
        var opacity = 1f;
        var pressureEnabled = !IsErasing && UsePressureSensitivity;
        var smoothingFactor = 0.42f;
        var streamline = 0.35f;
        var minPressure = 0.12f;
        var maxPressure = 1.25f;

        if (!IsErasing)
        {
            switch (brushType)
            {
                case BrushType.Pen:
                    opacity = 0.95f;
                    smoothingFactor = 0.42f;
                    streamline = 0.35f;
                    minPressure = 0.12f;
                    maxPressure = 1.3f;
                    break;
                case BrushType.Pencil:
                    opacity = 0.72f;
                    strokeWidth *= 0.82f;
                    smoothingFactor = 0.3f;
                    streamline = 0.25f;
                    minPressure = 0.05f;
                    maxPressure = 0.95f;
                    break;
                case BrushType.Marker:
                case BrushType.Highlighter:
                    opacity = 0.23f;
                    strokeWidth *= 2.35f;
                    pressureEnabled = false;
                    smoothingFactor = 0.26f;
                    streamline = 0.18f;
                    minPressure = 1f;
                    maxPressure = 1f;
                    break;
                case BrushType.Watercolor:
                    opacity = 0.88f;
                    strokeWidth *= 1.12f;
                    smoothingFactor = 0.5f;
                    streamline = 0.42f;
                    minPressure = 0.1f;
                    maxPressure = 1.2f;
                    break;
            }
        }

        _currentStroke = new DrawingStroke
        {
            Color = IsErasing ? SKColors.Transparent : StrokeColor,
            StrokeWidth = strokeWidth,
            IsEraser = IsErasing,
            Opacity = opacity,
            BrushType = brushType,
            Options = new StrokeOptions
            {
                PressureEnabled = pressureEnabled,
                SmoothingEnabled = true,
                SmoothingFactor = smoothingFactor,
                MinPressure = minPressure,
                MaxPressure = maxPressure,
                Streamline = streamline
            }
        };
        _currentStroke.AddPoint(point);
        StrokeStarted?.Invoke(this, EventArgs.Empty);
    }

    private void ContinueDrawing(DrawingPoint point)
    {
        if (!_isDrawing || _currentStroke == null)
            return;

        var last = _lastStrokePoint ?? _currentStroke.Points.LastOrDefault();
        if (last == null)
        {
            _currentStroke.AddPoint(point);
            _lastStrokePoint = point;
            return;
        }

        var zoom = Math.Max(0.1f, _isStrokeViewportLocked ? _strokeViewportZoom : ViewportZoom);
        var dx = point.X - last.X;
        var dy = point.Y - last.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        if (distance < 0.001f)
            return;

        var dynamicPoint = ApplyPressureDynamics(_currentStroke, last, point, distance);

        var firstMoveJumpThreshold = StrokeFirstMoveJumpThresholdScreen / zoom;
        if (!_hasStrokeSignificantMovement
            && _currentStroke.Points.Count <= 2
            && distance >= firstMoveJumpThreshold
            && dynamicPoint.Timestamp - _strokeStartTimestampMs <= StrokeFirstMoveJumpWindowMs)
        {
            // Stylus drivers can report an unstable first move; reset anchor to current point.
            if (_currentStroke.Points.Count > 0)
            {
                _currentStroke.Points[0] = dynamicPoint;
                _currentStroke.MarkDirty();
            }
            else
            {
                _currentStroke.AddPoint(dynamicPoint);
            }

            _lastStrokePoint = dynamicPoint;
            LogPointerState($"first-move-jump-filtered dist={distance:0.0} zoom={zoom:0.00}");
            return;
        }

        var maxSegmentLength = StrokeMaxSegmentLengthScreen / zoom;
        if (distance > maxSegmentLength)
        {
            var step = Math.Max(0.1f, StrokeInterpolationStepScreen / zoom);
            var segmentCount = Math.Max(1, (int)MathF.Ceiling(distance / step));
            for (var i = 1; i <= segmentCount; i++)
            {
                var t = i / (float)segmentCount;
                var interpolated = new DrawingPoint(
                    last.X + (dx * t),
                    last.Y + (dy * t),
                    last.Pressure + ((dynamicPoint.Pressure - last.Pressure) * t),
                    last.Timestamp + (long)((dynamicPoint.Timestamp - last.Timestamp) * t));
                _currentStroke.AddPoint(interpolated);
                _lastStrokePoint = interpolated;
            }
        }
        else
        {
            _currentStroke.AddPoint(dynamicPoint);
            _lastStrokePoint = dynamicPoint;
        }

        if (!_hasStrokeSignificantMovement && distance >= (4f / zoom))
        {
            _hasStrokeSignificantMovement = true;
        }
    }

    private void EndDrawing()
    {
        if (!_isDrawing || _currentStroke == null)
        {
            EndStrokeViewportLock();
            return;
        }

        if (CurrentLayerIndex >= 0 && CurrentLayerIndex < Layers.Count && _currentStroke.Points.Count > 0)
        {
            var committedStroke = _currentStroke;
            Layers[CurrentLayerIndex].AddStroke(committedStroke);
            _undoStack.Push(new StrokeHistoryEntry(CurrentLayerIndex, committedStroke));
            _redoStack.Clear();
            StrokeFinalized?.Invoke(this, new StrokeFinalizedEventArgs(CurrentLayerIndex, committedStroke));
            StrokeCommitted?.Invoke(this, EventArgs.Empty);
        }

        _isDrawing = false;
        _currentStroke = null;
        _lastStrokePoint = null;
        _hasStrokeSignificantMovement = false;
        _strokeStartTimestampMs = 0;
        EndStrokeViewportLock();
    }

    private void BeginStrokeViewportLock()
    {
        _strokeViewportScrollX = ScrollX;
        _strokeViewportScrollY = ScrollY;
        _strokeViewportZoom = Math.Max(0.1f, ViewportZoom);
        _isStrokeViewportLocked = true;
    }

    private void EndStrokeViewportLock()
    {
        _isStrokeViewportLocked = false;
        _strokeViewportScrollX = 0d;
        _strokeViewportScrollY = 0d;
        _strokeViewportZoom = 1f;
    }

    private static bool ShouldDrawPressureStrokeSegments(DrawingStroke stroke)
    {
        if (stroke.IsEraser || !stroke.Options.PressureEnabled || stroke.Points.Count < 2)
            return false;

        return stroke.BrushType == BrushType.Pen
            || stroke.BrushType == BrushType.Pencil
            || stroke.BrushType == BrushType.Watercolor;
    }

    private static void DrawPressureStrokeSegments(SKCanvas canvas, DrawingStroke stroke, float layerOpacity, float strokeWidthScale)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            BlendMode = GetBlendMode(stroke),
            Color = stroke.Color.WithAlpha((byte)(stroke.Color.Alpha * layerOpacity * stroke.Opacity))
        };

        var minPressure = Math.Max(0.02f, stroke.Options.MinPressure);
        var maxPressure = Math.Max(minPressure + 0.01f, stroke.Options.MaxPressure);
        var smoothing = 0.18f + (Math.Clamp(stroke.Options.SmoothingFactor, 0f, 1f) * 0.48f);
        var streamline = Math.Clamp(stroke.Options.Streamline, 0f, 1f);
        var widthPressure = Math.Clamp(stroke.Points[0].Pressure, minPressure, maxPressure);
        for (var i = 1; i < stroke.Points.Count; i++)
        {
            var prev = stroke.Points[i - 1];
            var current = stroke.Points[i];
            var dtMs = Math.Max(1L, current.Timestamp - prev.Timestamp);
            var dx = current.X - prev.X;
            var dy = current.Y - prev.Y;
            var distance = MathF.Sqrt((dx * dx) + (dy * dy));
            var velocity = distance / dtMs;
            var velocityFactor = Math.Clamp(1f - (velocity * (0.14f + (streamline * 0.35f))), 0.55f, 1.05f);
            var targetPressure = Math.Clamp(((prev.Pressure + current.Pressure) * 0.5f) * velocityFactor, minPressure, maxPressure);
            widthPressure = widthPressure + ((targetPressure - widthPressure) * smoothing);
            paint.StrokeWidth = Math.Max(0.25f, stroke.StrokeWidth * strokeWidthScale * widthPressure);
            canvas.DrawLine(prev.X, prev.Y, current.X, current.Y, paint);
        }
    }

    private static DrawingPoint ApplyPressureDynamics(
        DrawingStroke stroke,
        DrawingPoint previousPoint,
        DrawingPoint currentPoint,
        float distance)
    {
        if (!stroke.Options.PressureEnabled)
            return currentPoint;

        var minPressure = Math.Max(0.02f, stroke.Options.MinPressure);
        var maxPressure = Math.Max(minPressure + 0.01f, stroke.Options.MaxPressure);
        var dtMs = Math.Max(1L, currentPoint.Timestamp - previousPoint.Timestamp);
        var velocity = distance / dtMs;
        var streamline = Math.Clamp(stroke.Options.Streamline, 0f, 1f);
        var smoothing = 0.22f + (Math.Clamp(stroke.Options.SmoothingFactor, 0f, 1f) * 0.46f);

        var simulatedPressure = Math.Clamp(
            1.16f - (velocity * (0.18f + (streamline * 0.62f))),
            minPressure,
            maxPressure);
        var hasHardwarePressure = Math.Abs(currentPoint.Pressure - 1f) > 0.03f;
        var targetPressure = hasHardwarePressure
            ? ((currentPoint.Pressure * 0.76f) + (simulatedPressure * 0.24f))
            : simulatedPressure;
        targetPressure = Math.Clamp(targetPressure, minPressure, maxPressure);

        var previousPressure = Math.Clamp(previousPoint.Pressure, minPressure, maxPressure);
        var stabilizedPressure = previousPressure + ((targetPressure - previousPressure) * smoothing);
        stabilizedPressure = Math.Clamp(stabilizedPressure, minPressure, maxPressure);

        return new DrawingPoint(
            currentPoint.X,
            currentPoint.Y,
            stabilizedPressure,
            currentPoint.Timestamp);
    }

    private static SKBlendMode GetBlendMode(DrawingStroke stroke)
    {
        if (stroke.IsEraser)
            return SKBlendMode.Clear;
        if (stroke.BrushType == BrushType.Highlighter || stroke.BrushType == BrushType.Marker)
            return SKBlendMode.Multiply;
        return SKBlendMode.SrcOver;
    }

    private void HandleAdvancedEraser(SKTouchEventArgs e, DrawingPoint point)
    {
        switch (EraserBehavior)
        {
            case EraserMode.Stroke:
                if (e.ActionType == SKTouchAction.Pressed || e.ActionType == SKTouchAction.Moved)
                {
                    var radius = Math.Max(3f, StrokeWidth * 0.75f);
                    if (EraseWholeStrokeAt(point.X, point.Y, radius))
                    {
                        StrokeCommitted?.Invoke(this, EventArgs.Empty);
                    }
                }
                break;
            case EraserMode.Lasso:
                HandleLassoEraser(e, point);
                break;
        }
    }

    private void HandleLassoEraser(SKTouchEventArgs e, DrawingPoint point)
    {
        var p = new SKPoint(point.X, point.Y);
        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                _isLassoErasing = true;
                _lassoPoints.Clear();
                _lassoPoints.Add(p);
                break;
            case SKTouchAction.Moved:
                if (!_isLassoErasing)
                    return;

                if (_lassoPoints.Count == 0)
                {
                    _lassoPoints.Add(p);
                    return;
                }

                var last = _lassoPoints[^1];
                var dx = p.X - last.X;
                var dy = p.Y - last.Y;
                if ((dx * dx) + (dy * dy) >= 1f)
                {
                    _lassoPoints.Add(p);
                }
                break;
            case SKTouchAction.Released:
            case SKTouchAction.Cancelled:
                if (!_isLassoErasing)
                    return;

                if (_lassoPoints.Count >= 3)
                {
                    var first = _lassoPoints[0];
                    var lastPoint = _lassoPoints[^1];
                    var lx = first.X - lastPoint.X;
                    var ly = first.Y - lastPoint.Y;
                    if ((lx * lx) + (ly * ly) >= 1f)
                    {
                        _lassoPoints.Add(first);
                    }

                    if (EraseStrokesInsideLasso())
                    {
                        StrokeCommitted?.Invoke(this, EventArgs.Empty);
                    }
                }

                _isLassoErasing = false;
                _lassoPoints.Clear();
                break;
        }
    }

    private bool EraseWholeStrokeAt(float x, float y, float radius)
    {
        if (Layers.Count == 0 || CurrentLayerIndex < 0 || CurrentLayerIndex >= Layers.Count)
            return false;

        var layer = Layers[CurrentLayerIndex];
        if (layer.IsLocked || layer.Strokes.Count == 0)
            return false;

        var removed = false;
        var threshold = Math.Max(2f, radius);
        for (var i = layer.Strokes.Count - 1; i >= 0; i--)
        {
            var stroke = layer.Strokes[i];
            if (stroke.IsEraser || stroke.Points.Count == 0)
                continue;

            var hitRadius = threshold + (stroke.StrokeWidth * 0.5f);
            if (!IsPointNearStroke(x, y, stroke, hitRadius))
                continue;

            layer.Strokes.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    private bool EraseStrokesInsideLasso()
    {
        if (Layers.Count == 0 || CurrentLayerIndex < 0 || CurrentLayerIndex >= Layers.Count || _lassoPoints.Count < 3)
            return false;

        var layer = Layers[CurrentLayerIndex];
        if (layer.IsLocked || layer.Strokes.Count == 0)
            return false;

        var removed = false;
        for (var i = layer.Strokes.Count - 1; i >= 0; i--)
        {
            var stroke = layer.Strokes[i];
            if (stroke.IsEraser || stroke.Points.Count == 0)
                continue;

            var center = GetStrokeCenter(stroke);
            if (!IsPointInsidePolygon(center, _lassoPoints))
                continue;

            layer.Strokes.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    private static SKPoint GetStrokeCenter(DrawingStroke stroke)
    {
        if (stroke.Points.Count == 0)
            return SKPoint.Empty;

        float sumX = 0f;
        float sumY = 0f;
        foreach (var p in stroke.Points)
        {
            sumX += p.X;
            sumY += p.Y;
        }

        var count = stroke.Points.Count;
        return new SKPoint(sumX / count, sumY / count);
    }

    private static bool IsPointInsidePolygon(SKPoint point, IReadOnlyList<SKPoint> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var xi = polygon[i].X;
            var yi = polygon[i].Y;
            var xj = polygon[j].X;
            var yj = polygon[j].Y;

            var intersect = ((yi > point.Y) != (yj > point.Y))
                && (point.X < ((xj - xi) * (point.Y - yi) / ((yj - yi) + float.Epsilon)) + xi);
            if (intersect)
                inside = !inside;
        }

        return inside;
    }

    private static bool IsPointNearStroke(float x, float y, DrawingStroke stroke, float radius)
    {
        var radiusSquared = radius * radius;
        if (stroke.Points.Count == 1)
        {
            var p = stroke.Points[0];
            var dx = p.X - x;
            var dy = p.Y - y;
            return (dx * dx) + (dy * dy) <= radiusSquared;
        }

        for (var i = 1; i < stroke.Points.Count; i++)
        {
            var a = stroke.Points[i - 1];
            var b = stroke.Points[i];
            var distanceSquared = DistanceSquaredToSegment(x, y, a.X, a.Y, b.X, b.Y);
            if (distanceSquared <= radiusSquared)
                return true;
        }

        return false;
    }

    private static float DistanceSquaredToSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        if (Math.Abs(dx) < float.Epsilon && Math.Abs(dy) < float.Epsilon)
        {
            var ex = px - ax;
            var ey = py - ay;
            return (ex * ex) + (ey * ey);
        }

        var t = ((px - ax) * dx + (py - ay) * dy) / ((dx * dx) + (dy * dy));
        t = Math.Clamp(t, 0f, 1f);
        var cx = ax + (t * dx);
        var cy = ay + (t * dy);
        var mx = px - cx;
        var my = py - cy;
        return (mx * mx) + (my * my);
    }

    private void ResetHistory()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }

    private void RemoveStrokeFromHistory(DrawingStroke stroke)
    {
        RemoveStrokeFromHistoryStack(_undoStack, stroke);
        RemoveStrokeFromHistoryStack(_redoStack, stroke);
    }

    private static void RemoveStrokeFromHistoryStack(Stack<StrokeHistoryEntry> stack, DrawingStroke stroke)
    {
        if (stack.Count == 0)
            return;

        var retained = stack
            .Reverse()
            .Where(entry => !ReferenceEquals(entry.Stroke, stroke))
            .ToArray();
        stack.Clear();
        foreach (var entry in retained)
        {
            stack.Push(entry);
        }
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
