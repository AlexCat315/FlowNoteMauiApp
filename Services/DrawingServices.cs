using FlowNoteMauiApp.Core.Drawing;
using FlowNoteMauiApp.Core.AI;
using SkiaSharp;

namespace FlowNoteMauiApp.Services;

public class SkiaDrawingEngine : IDrawingEngine
{
    private ToolType _currentTool = ToolType.Pen;
    private SKColor _currentColor = SKColors.Black;
    private float _strokeWidth = 3f;
    private bool _pressureSensitivity = true;

    public void Initialize()
    {
    }

    public void Render(object canvas, object surface)
    {
    }

    public void SetTool(ToolType tool)
    {
        _currentTool = tool;
    }

    public void SetColor(byte r, byte g, byte b, byte a = 255)
    {
        _currentColor = new SKColor(r, g, b, a);
    }

    public void SetStrokeWidth(float width)
    {
        _strokeWidth = width;
    }

    public void SetPressureSensitivity(bool enabled)
    {
        _pressureSensitivity = enabled;
    }

    public SKColor CurrentColor => _currentColor;
    public float StrokeWidth => _strokeWidth;
    public ToolType CurrentTool => _currentTool;
    public bool PressureSensitivity => _pressureSensitivity;
}

public class LayerManager : ILayerManager
{
    private readonly List<Layer> _layers = new();
    private int _activeLayerIndex = 0;

    public void AddLayer(string name)
    {
        _layers.Add(new Layer { Name = name });
        _activeLayerIndex = _layers.Count - 1;
    }

    public void RemoveLayer(int index)
    {
        if (index >= 0 && index < _layers.Count && _layers.Count > 1)
        {
            _layers.RemoveAt(index);
            if (_activeLayerIndex >= _layers.Count)
                _activeLayerIndex = _layers.Count - 1;
        }
    }

    public void MergeDown(int index)
    {
        if (index > 0 && index < _layers.Count)
        {
            _layers[index - 1].Strokes.AddRange(_layers[index].Strokes);
            _layers.RemoveAt(index);
        }
    }

    public void DuplicateLayer(int index)
    {
        if (index >= 0 && index < _layers.Count)
        {
            var duplicated = new Layer
            {
                Name = _layers[index].Name + " (Copy)",
                Strokes = new(_layers[index].Strokes),
                IsVisible = _layers[index].IsVisible,
                IsLocked = _layers[index].IsLocked,
                Opacity = _layers[index].Opacity
            };
            _layers.Insert(index + 1, duplicated);
        }
    }

    public void MoveLayer(int fromIndex, int toIndex)
    {
        if (fromIndex >= 0 && fromIndex < _layers.Count &&
            toIndex >= 0 && toIndex < _layers.Count)
        {
            var layer = _layers[fromIndex];
            _layers.RemoveAt(fromIndex);
            _layers.Insert(toIndex, layer);
        }
    }

    public IReadOnlyList<Layer> Layers => _layers;
    public int ActiveLayerIndex
    {
        get => _activeLayerIndex;
        set => _activeLayerIndex = Math.Clamp(value, 0, _layers.Count - 1);
    }
}

public class Layer
{
    public string Name { get; set; } = "Layer";
    public List<Models.DrawingStroke> Strokes { get; set; } = new();
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public float Opacity { get; set; } = 1.0f;
    public SKColor BackgroundColor { get; set; } = SKColors.Transparent;
}

public class StrokeRecorder : IStrokeRecorder
{
    private readonly List<StrokePoint> _points = new();
    private bool _isRecording = false;

    public void StartRecording()
    {
        _points.Clear();
        _isRecording = true;
    }

    public void StopRecording()
    {
        _isRecording = false;
    }

    public void AddPoint(float x, float y, float pressure)
    {
        if (_isRecording)
        {
            _points.Add(new StrokePoint
            {
                X = x,
                Y = y,
                Pressure = pressure,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }

    public byte[]? GetRecording()
    {
        if (_points.Count == 0)
            return null;

        var json = System.Text.Json.JsonSerializer.Serialize(_points);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public void LoadRecording(byte[] data)
    {
        var json = System.Text.Encoding.UTF8.GetString(data);
        _points.Clear();
        _points.AddRange(System.Text.Json.JsonSerializer.Deserialize<List<StrokePoint>>(json) ?? new List<StrokePoint>());
    }
}
