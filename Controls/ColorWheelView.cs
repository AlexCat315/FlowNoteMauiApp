using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace FlowNoteMauiApp.Controls;

public sealed class ColorWheelView : SKCanvasView
{
    private float _hue;
    private SKPoint _center;
    private float _outerRadius;
    private float _innerRadius;

    public event EventHandler? SelectedColorChanged;

    public float Hue
    {
        get => _hue;
        set
        {
            var normalized = NormalizeHue(value);
            if (Math.Abs(_hue - normalized) < 0.001f)
                return;

            _hue = normalized;
            InvalidateSurface();
            SelectedColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ColorWheelView()
    {
        EnableTouchEvents = true;
        Touch += OnTouch;
    }

    public SKColor BuildColor(float saturation, float value)
    {
        return HsvToColor(Hue, Math.Clamp(saturation, 0f, 1f), Math.Clamp(value, 0f, 1f));
    }

    public void SetFromColor(SKColor color)
    {
        ToHsv(color, out var hue, out _, out _);
        _hue = hue;
        InvalidateSurface();
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var info = e.Info;
        if (info.Width <= 0 || info.Height <= 0)
            return;

        _center = new SKPoint(info.Width * 0.5f, info.Height * 0.5f);
        _outerRadius = Math.Min(info.Width, info.Height) * 0.46f;
        _innerRadius = _outerRadius * 0.68f;
        var ringRadius = (_outerRadius + _innerRadius) * 0.5f;
        var ringStroke = Math.Max(8f, _outerRadius - _innerRadius);

        using (var ringPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = ringStroke,
            Shader = SKShader.CreateSweepGradient(
                _center,
                new[]
                {
                    SKColor.Parse("FF0000"),
                    SKColor.Parse("FFFF00"),
                    SKColor.Parse("00FF00"),
                    SKColor.Parse("00FFFF"),
                    SKColor.Parse("0000FF"),
                    SKColor.Parse("FF00FF"),
                    SKColor.Parse("FF0000")
                },
                null)
        })
        {
            canvas.DrawCircle(_center, ringRadius, ringPaint);
        }

        using (var innerFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = BuildColor(1f, 1f).WithAlpha(36)
        })
        {
            canvas.DrawCircle(_center, _innerRadius - 2f, innerFill);
        }

        var angleRad = (Hue - 90f) * (MathF.PI / 180f);
        var markerRadius = ringRadius;
        var marker = new SKPoint(
            _center.X + (MathF.Cos(angleRad) * markerRadius),
            _center.Y + (MathF.Sin(angleRad) * markerRadius));

        using (var markerShadow = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.Black.WithAlpha(88)
        })
        {
            canvas.DrawCircle(marker.X, marker.Y, 8f, markerShadow);
        }

        using (var markerFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = BuildColor(1f, 1f)
        })
        {
            canvas.DrawCircle(marker.X, marker.Y, 6f, markerFill);
        }

        using (var markerStroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = SKColors.White
        })
        {
            canvas.DrawCircle(marker.X, marker.Y, 6f, markerStroke);
        }
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        if (e.ActionType is not SKTouchAction.Pressed and not SKTouchAction.Moved)
        {
            e.Handled = false;
            return;
        }

        var dx = e.Location.X - _center.X;
        var dy = e.Location.Y - _center.Y;
        var distance = MathF.Sqrt((dx * dx) + (dy * dy));
        var minDistance = _innerRadius - 14f;
        var maxDistance = _outerRadius + 14f;
        if (distance < minDistance || distance > maxDistance)
        {
            e.Handled = false;
            return;
        }

        var angle = MathF.Atan2(dy, dx) * (180f / MathF.PI) + 90f;
        if (angle < 0f)
            angle += 360f;

        Hue = angle;
        e.Handled = true;
    }

    private static float NormalizeHue(float hue)
    {
        var result = hue % 360f;
        if (result < 0f)
            result += 360f;
        return result;
    }

    private static SKColor HsvToColor(float hue, float saturation, float value)
    {
        var h = NormalizeHue(hue);
        var s = Math.Clamp(saturation, 0f, 1f);
        var v = Math.Clamp(value, 0f, 1f);

        var c = v * s;
        var x = c * (1f - Math.Abs(((h / 60f) % 2f) - 1f));
        var m = v - c;

        float r1;
        float g1;
        float b1;
        if (h < 60f)
        {
            r1 = c;
            g1 = x;
            b1 = 0f;
        }
        else if (h < 120f)
        {
            r1 = x;
            g1 = c;
            b1 = 0f;
        }
        else if (h < 180f)
        {
            r1 = 0f;
            g1 = c;
            b1 = x;
        }
        else if (h < 240f)
        {
            r1 = 0f;
            g1 = x;
            b1 = c;
        }
        else if (h < 300f)
        {
            r1 = x;
            g1 = 0f;
            b1 = c;
        }
        else
        {
            r1 = c;
            g1 = 0f;
            b1 = x;
        }

        var r = (byte)Math.Clamp((int)Math.Round((r1 + m) * 255f), 0, 255);
        var g = (byte)Math.Clamp((int)Math.Round((g1 + m) * 255f), 0, 255);
        var b = (byte)Math.Clamp((int)Math.Round((b1 + m) * 255f), 0, 255);
        return new SKColor(r, g, b, 255);
    }

    private static void ToHsv(SKColor color, out float hue, out float saturation, out float value)
    {
        var r = color.Red / 255f;
        var g = color.Green / 255f;
        var b = color.Blue / 255f;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        hue = 0f;
        if (delta > 0.0001f)
        {
            if (max == r)
            {
                hue = 60f * (((g - b) / delta) % 6f);
            }
            else if (max == g)
            {
                hue = 60f * (((b - r) / delta) + 2f);
            }
            else
            {
                hue = 60f * (((r - g) / delta) + 4f);
            }
        }

        if (hue < 0f)
            hue += 360f;

        value = max;
        saturation = max <= 0.0001f ? 0f : delta / max;
    }
}
