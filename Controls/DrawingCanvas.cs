using System.Collections.ObjectModel;
using FlowNoteMauiApp.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Microsoft.Maui.Controls;

namespace FlowNoteMauiApp.Controls;

public class DrawingCanvas : SKCanvasView
{
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

    public static readonly BindableProperty IsPenModeProperty =
        BindableProperty.Create(nameof(IsPenMode), typeof(bool), typeof(DrawingCanvas),
            defaultValue: true, propertyChanged: OnPenModeChanged);

    public static readonly BindableProperty EnableDrawingProperty =
        BindableProperty.Create(nameof(EnableDrawing), typeof(bool), typeof(DrawingCanvas),
            defaultValue: false, propertyChanged: OnEnableDrawingChanged);

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

    private DrawingStroke? _currentStroke;
    private bool _isDrawing;
    private bool _isStylus;
    private bool _isTwoFingerMode;

    public DrawingCanvas()
    {
        Layers = new ObservableCollection<DrawingLayer>
        {
            new DrawingLayer { Name = "Layer 1" }
        };
        IgnorePixelScaling = false;
        EnableTouchEvents = true;
        
        Touch += OnCanvasTouch;
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
    }

    private static void OnEnableDrawingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DrawingCanvas canvas)
        {
            canvas.InputTransparent = !(bool)newValue;
        }
    }

    private void OnCanvasTouch(object? sender, SKTouchEventArgs e)
    {
        if (!EnableDrawing)
        {
            e.Handled = false;
            return;
        }

        _isStylus = e.DeviceType == SKTouchDeviceType.Pen;

        if (IsPenMode && !_isStylus)
        {
            e.Handled = false;
            return;
        }

        if (Layers == null || Layers.Count == 0 || CurrentLayerIndex < 0 || CurrentLayerIndex >= Layers.Count)
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

        var location = new DrawingPoint(
            e.Location.X + (float)ScrollX,
            e.Location.Y + (float)ScrollY,
            e.Pressure, 0);

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

    private void StartDrawing(DrawingPoint point)
    {
        _isDrawing = true;
        
        SKColor strokeColor;
        float strokeOpacity;
        
        if (IsHighlighter)
        {
            strokeColor = StrokeColor;
            strokeOpacity = 0.4f;
        }
        else if (IsErasing)
        {
            strokeColor = SKColors.White;
            strokeOpacity = 1.0f;
        }
        else
        {
            strokeColor = StrokeColor;
            strokeOpacity = 1.0f;
        }
        
        _currentStroke = new DrawingStroke
        {
            Color = strokeColor,
            StrokeWidth = IsHighlighter ? StrokeWidth * 3 : StrokeWidth,
            IsEraser = IsErasing,
            Opacity = strokeOpacity
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

        if (Layers != null && CurrentLayerIndex >= 0 && CurrentLayerIndex < Layers.Count)
        {
            var currentLayer = Layers[CurrentLayerIndex];
            if (!currentLayer.IsLocked)
            {
                currentLayer.AddStroke(_currentStroke);
            }
        }

        _isDrawing = false;
        _currentStroke = null;
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        canvas.Translate(-(float)ScrollX, -(float)ScrollY);

        if (Layers == null || Layers.Count == 0)
            return;

        using var layerPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        for (int i = 0; i < Layers.Count; i++)
        {
            var layer = Layers[i];
            if (!layer.IsVisible)
                continue;

            if (layer.BackgroundColor != SKColors.Transparent)
            {
                canvas.Clear(layer.BackgroundColor);
            }

            foreach (var stroke in layer.Strokes)
            {
                if (stroke.Points.Count < 2)
                    continue;

                layerPaint.Color = stroke.Color.WithAlpha((byte)(stroke.Color.Alpha * layer.Opacity * stroke.Opacity));
                layerPaint.StrokeWidth = stroke.StrokeWidth;

                if (stroke.IsEraser)
                {
                    layerPaint.BlendMode = SKBlendMode.Clear;
                }
                else
                {
                    layerPaint.BlendMode = SKBlendMode.SrcOver;
                }

                var path = stroke.CreatePath();
                canvas.DrawPath(path, layerPaint);
            }

            if (i == CurrentLayerIndex && _currentStroke != null && _currentStroke.Points.Count >= 2)
            {
                layerPaint.Color = _currentStroke.Color;
                layerPaint.StrokeWidth = _currentStroke.StrokeWidth;
                layerPaint.BlendMode = SKBlendMode.SrcOver;

                var path = _currentStroke.CreatePath();
                canvas.DrawPath(path, layerPaint);
            }
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
    }

    public void RemoveLayer(int index)
    {
        if (index < 0 || index >= Layers.Count)
            return;

        if (Layers.Count <= 1)
            return;

        Layers.RemoveAt(index);
        if (CurrentLayerIndex >= Layers.Count)
        {
            CurrentLayerIndex = Layers.Count - 1;
        }
    }

    public void ClearCurrentLayer()
    {
        if (Layers == null || CurrentLayerIndex < 0 || CurrentLayerIndex >= Layers.Count)
            return;

        Layers[CurrentLayerIndex].Clear();
        InvalidateSurface();
    }

    public void ClearAllLayers()
    {
        if (Layers == null)
            return;

        foreach (var layer in Layers)
        {
            layer.Clear();
        }
        InvalidateSurface();
    }

    private readonly Stack<DrawingStroke> _undoStack = new();
    private readonly Stack<DrawingStroke> _redoStack = new();

    public void Undo()
    {
        if (Layers == null || CurrentLayerIndex < 0 || CurrentLayerIndex >= Layers.Count)
            return;

        var currentLayer = Layers[CurrentLayerIndex];
        if (currentLayer.Strokes.Count > 0)
        {
            var stroke = currentLayer.Strokes[^1];
            currentLayer.Strokes.RemoveAt(currentLayer.Strokes.Count - 1);
            _undoStack.Push(stroke);
            _redoStack.Clear();
            InvalidateSurface();
        }
    }

    public void Redo()
    {
        if (Layers == null || CurrentLayerIndex < 0 || CurrentLayerIndex >= Layers.Count)
            return;

        if (_redoStack.Count > 0)
        {
            var stroke = _redoStack.Pop();
            Layers[CurrentLayerIndex].Strokes.Add(stroke);
            InvalidateSurface();
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
}
