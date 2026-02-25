namespace FlowNoteMauiApp.Models;

public class DrawingPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Pressure { get; set; } = 1.0f;
    public long Timestamp { get; set; }

    public DrawingPoint() { }

    public DrawingPoint(float x, float y, float pressure = 1.0f, long timestamp = 0)
    {
        X = x;
        Y = y;
        Pressure = pressure;
        Timestamp = timestamp == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : timestamp;
    }
}
