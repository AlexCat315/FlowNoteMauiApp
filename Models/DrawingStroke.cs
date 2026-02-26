using SkiaSharp;

namespace FlowNoteMauiApp.Models;

public enum BrushType
{
    Pen,
    Highlighter,
    Eraser,
    Pencil,
    Marker,
    Watercolor
}

public class StrokeOptions
{
    public bool PressureEnabled { get; set; } = true;
    public bool SmoothingEnabled { get; set; } = true;
    public float SmoothingFactor { get; set; } = 0.5f;
    public float MinPressure { get; set; } = 0.1f;
    public float MaxPressure { get; set; } = 1.0f;
    public float TaperEnabled { get; set; } = 0f;
    public float TaperStart { get; set; } = 0f;
    public float TaperEnd { get; set; } = 0f;
    public float Streamline { get; set; } = 0f;
}

public class DrawingStroke
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public List<DrawingPoint> Points { get; set; } = new();
    public SKColor Color { get; set; } = SKColors.Black;
    public float StrokeWidth { get; set; } = 3f;
    public float Opacity { get; set; } = 1.0f;
    public bool IsEraser { get; set; } = false;
    public BrushType BrushType { get; set; } = BrushType.Pen;
    public StrokeOptions Options { get; set; } = new();

    private SKPath? _cachedPath;
    private bool _pathDirty = true;

    public void AddPoint(DrawingPoint point)
    {
        Points.Add(point);
        _pathDirty = true;
    }

    public void Clear()
    {
        Points.Clear();
        _cachedPath = null;
    }

    public void MarkDirty()
    {
        _pathDirty = true;
    }

    private List<DrawingPoint> GetSmoothedPoints()
    {
        if (!Options.SmoothingEnabled || Points.Count < 3)
            return Points;

        var smoothed = new List<DrawingPoint>();
        
        smoothed.Add(Points[0]);
        
        for (int i = 1; i < Points.Count - 1; i++)
        {
            var prev = Points[i - 1];
            var curr = Points[i];
            var next = Points[i + 1];

            var smoothedPoint = new DrawingPoint(
                curr.X * Options.SmoothingFactor + (prev.X + next.X) / 2 * (1 - Options.SmoothingFactor),
                curr.Y * Options.SmoothingFactor + (prev.Y + next.Y) / 2 * (1 - Options.SmoothingFactor),
                curr.Pressure,
                curr.Timestamp
            );
            
            smoothed.Add(smoothedPoint);
        }
        
        smoothed.Add(Points[^1]);
        
        return smoothed;
    }

    private float GetPressureAtIndex(int index)
    {
        if (index < 0 || index >= Points.Count)
            return 1.0f;
        
        var pressure = Points[index].Pressure;
        pressure = Math.Clamp(pressure, Options.MinPressure, Options.MaxPressure);
        
        var taperStart = Options.TaperStart;
        var taperEnd = Options.TaperEnd;
        
        if (taperStart > 0 && index < taperStart * Points.Count)
        {
            var t = (float)index / (taperStart * Points.Count);
            pressure *= t * t;
        }
        
        if (taperEnd > 0 && index > (1 - taperEnd) * Points.Count)
        {
            var t = (float)(Points.Count - 1 - index) / (taperEnd * Points.Count);
            pressure *= t * t;
        }
        
        return pressure;
    }

    public SKPath CreatePath()
    {
        if (_cachedPath != null && !_pathDirty)
            return _cachedPath;

        _cachedPath = new SKPath();
        
        if (Points.Count == 0)
            return _cachedPath;

        if (Points.Count == 1)
        {
            var p = Points[0];
            _cachedPath.MoveTo(p.X, p.Y);
            _cachedPath.LineTo(p.X + 0.1f, p.Y + 0.1f);
            return _cachedPath;
        }

        var smoothedPoints = GetSmoothedPoints();

        if (BrushType == BrushType.Highlighter || BrushType == BrushType.Watercolor || BrushType == BrushType.Marker)
        {
            return CreateVariableWidthPath(smoothedPoints);
        }

        return CreateSmoothPath(smoothedPoints);
    }

    private SKPath CreateSmoothPath(List<DrawingPoint> points)
    {
        var path = new SKPath();
        
        if (points.Count < 2)
            return path;

        path.MoveTo(points[0].X, points[0].Y);

        if (points.Count == 2)
        {
            path.LineTo(points[1].X, points[1].Y);
            return path;
        }

        for (int i = 1; i < points.Count - 1; i++)
        {
            var p0 = points[i - 1];
            var p1 = points[i];
            var p2 = points[i + 1];

            var midX = (p1.X + p2.X) / 2;
            var midY = (p1.Y + p2.Y) / 2;

            path.QuadTo(p1.X, p1.Y, midX, midY);
        }

        var lastPoint = points[^1];
        var secondLast = points[^2];
        path.QuadTo(secondLast.X, secondLast.Y, lastPoint.X, lastPoint.Y);

        return path;
    }

    private SKPath CreateVariableWidthPath(List<DrawingPoint> points)
    {
        var path = new SKPath();
        
        if (points.Count < 2)
            return path;

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            
            var pressure1 = Options.PressureEnabled ? GetPressureAtIndex(i) : 1.0f;
            var pressure2 = Options.PressureEnabled ? GetPressureAtIndex(i + 1) : 1.0f;
            
            var width1 = StrokeWidth * pressure1;
            var width2 = StrokeWidth * pressure2;

            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len = (float)Math.Sqrt(dx * dx + dy * dy);
            
            if (len < 0.001f)
                continue;

            var nx = -dy / len;
            var ny = dx / len;

            var x1a = p1.X + nx * width1 / 2;
            var y1a = p1.Y + ny * width1 / 2;
            var x1b = p1.X - nx * width1 / 2;
            var y1b = p1.Y - ny * width1 / 2;
            
            var x2a = p2.X + nx * width2 / 2;
            var y2a = p2.Y + ny * width2 / 2;
            var x2b = p2.X - nx * width2 / 2;
            var y2b = p2.Y - ny * width2 / 2;

            if (i == 0)
            {
                path.MoveTo(x1a, y1a);
            }
            
            path.LineTo(x2a, y2a);
        }

        for (int i = points.Count - 1; i > 0; i--)
        {
            var p1 = points[i];
            var p2 = points[i - 1];
            
            var pressure1 = Options.PressureEnabled ? GetPressureAtIndex(i) : 1.0f;
            var pressure2 = Options.PressureEnabled ? GetPressureAtIndex(i - 1) : 1.0f;
            
            var width1 = StrokeWidth * pressure1;
            var width2 = StrokeWidth * pressure2;

            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len = (float)Math.Sqrt(dx * dx + dy * dy);
            
            if (len < 0.001f)
                continue;

            var nx = -dy / len;
            var ny = dx / len;

            var x1a = p1.X + nx * width1 / 2;
            var y1a = p1.Y + ny * width1 / 2;
            var x1b = p1.X - nx * width1 / 2;
            var y1b = p1.Y - ny * width1 / 2;
            
            var x2a = p2.X + nx * width2 / 2;
            var y2a = p2.Y + ny * width2 / 2;
            var x2b = p2.X - nx * width2 / 2;
            var y2b = p2.Y - ny * width2 / 2;

            path.LineTo(x2b, y2b);
        }

        path.Close();
        return path;
    }
}
