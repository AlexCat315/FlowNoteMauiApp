namespace FlowNoteMauiApp.Core.Drawing;

public interface IDrawingEngine
{
    void Initialize();
    void Render(object canvas, object surface);
    void SetTool(ToolType tool);
    void SetColor(byte r, byte g, byte b, byte a = 255);
    void SetStrokeWidth(float width);
    void SetPressureSensitivity(bool enabled);
}

public enum ToolType
{
    Pen,
    Highlighter,
    Eraser,
    Pencil,
    Marker,
    Watercolor,
    Select,
    Shape
}

public interface ILayerManager
{
    void AddLayer(string name);
    void RemoveLayer(int index);
    void MergeDown(int index);
    void DuplicateLayer(int index);
    void MoveLayer(int fromIndex, int toIndex);
}

public interface IStrokeRecorder
{
    void StartRecording();
    void StopRecording();
    void AddPoint(float x, float y, float pressure);
    byte[]? GetRecording();
    void LoadRecording(byte[] data);
}
