namespace FlowNoteMauiApp.Core.AI;

public interface IHandwritingRecognizer
{
    Task<string> RecognizeTextAsync(byte[] imageData);
    Task<string> RecognizeTextAsync(string strokesJson);
    void LoadModel(string modelPath);
}

public interface IShapeRecognizer
{
    Task<ShapeRecognitionResult> RecognizeShapeAsync(List<StrokePoint> points);
    void LoadModel(string modelPath);
}

public class ShapeRecognitionResult
{
    public ShapeType Type { get; set; }
    public float Confidence { get; set; }
    public object? Bounds { get; set; }
}

public enum ShapeType
{
    Unknown,
    Line,
    Rectangle,
    Circle,
    Ellipse,
    Triangle,
    Arrow
}

public class StrokePoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Pressure { get; set; }
    public long Timestamp { get; set; }
}
