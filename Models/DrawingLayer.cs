using SkiaSharp;

namespace FlowNoteMauiApp.Models;

public class DrawingLayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Layer";
    public List<DrawingStroke> Strokes { get; set; } = new();
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public float Opacity { get; set; } = 1.0f;
    public SKColor BackgroundColor { get; set; } = SKColors.Transparent;

    public void AddStroke(DrawingStroke stroke)
    {
        Strokes.Add(stroke);
    }

    public void RemoveStroke(string strokeId)
    {
        var stroke = Strokes.FirstOrDefault(s => s.Id == strokeId);
        if (stroke != null)
        {
            Strokes.Remove(stroke);
        }
    }

    public void Clear()
    {
        Strokes.Clear();
    }

    public DrawingLayer Clone()
    {
        return new DrawingLayer
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name + " (Copy)",
            IsVisible = IsVisible,
            IsLocked = IsLocked,
            Opacity = Opacity,
            BackgroundColor = BackgroundColor,
            Strokes = Strokes.Select(s => new DrawingStroke
            {
                Id = Guid.NewGuid().ToString(),
                Points = new List<DrawingPoint>(s.Points),
                Color = s.Color,
                StrokeWidth = s.StrokeWidth,
                Opacity = s.Opacity,
                IsEraser = s.IsEraser
            }).ToList()
        };
    }
}
