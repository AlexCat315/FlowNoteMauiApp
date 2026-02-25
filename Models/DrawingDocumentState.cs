namespace FlowNoteMauiApp.Models;

public sealed class DrawingDocumentState
{
    public int CurrentLayerIndex { get; set; }
    public List<DrawingLayerState> Layers { get; set; } = new();
}

public sealed class DrawingLayerState
{
    public string Name { get; set; } = "Layer";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public float Opacity { get; set; } = 1f;
    public uint BackgroundColor { get; set; }
    public List<DrawingStrokeState> Strokes { get; set; } = new();
}

public sealed class DrawingStrokeState
{
    public uint Color { get; set; }
    public float StrokeWidth { get; set; } = 3f;
    public float Opacity { get; set; } = 1f;
    public bool IsEraser { get; set; }
    public BrushType BrushType { get; set; } = BrushType.Pen;
    public bool PressureEnabled { get; set; } = true;
    public bool SmoothingEnabled { get; set; } = true;
    public float SmoothingFactor { get; set; } = 0.5f;
    public float MinPressure { get; set; } = 0.1f;
    public float MaxPressure { get; set; } = 1.0f;
    public float TaperStart { get; set; }
    public float TaperEnd { get; set; }
    public float Streamline { get; set; }
    public List<DrawingPoint> Points { get; set; } = new();
}
