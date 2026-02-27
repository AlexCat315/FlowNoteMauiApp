using SkiaSharp;

namespace FlowNoteMauiApp.Core.Rendering.ToolIcons;

public static class ProceduralToolIconRenderer
{
    public static SKBitmap Render(ToolIconKind toolKind, SKColor accentColor, int size = 256)
    {
        var safeSize = Math.Clamp(size, 64, 512);
        var bitmap = new SKBitmap(safeSize, safeSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // Draw the tool upside-down in local coordinates, then flip vertically so the tip points down.
        canvas.Save();
        canvas.Translate(0, safeSize);
        canvas.Scale(1f, -1f);
        DrawToolUpright(canvas, toolKind, accentColor, safeSize);
        canvas.Restore();

        var shadowLeft = safeSize * 0.24f;
        var shadowTop = safeSize * 0.84f;
        var shadowRight = safeSize * 0.76f;
        var shadowBottom = safeSize * 0.94f;
        DrawGroundShadow(canvas, new SKRect(shadowLeft, shadowTop, shadowRight, shadowBottom));

        canvas.Flush();
        return bitmap;
    }

    private static void DrawToolUpright(SKCanvas canvas, ToolIconKind toolKind, SKColor accentColor, int size)
    {
        var centerX = size * 0.5f;
        var bodyTop = size * 0.26f;
        var bodyBottom = size * 0.86f;
        var nibTop = size * 0.08f;

        switch (toolKind)
        {
            case ToolIconKind.Ballpoint:
            {
                var bodyRect = DrawTubeBody(canvas, centerX, bodyTop, size * 0.28f, bodyBottom - bodyTop, size * 0.10f);
                DrawGripBand(canvas, bodyRect, 0.58f, SKColor.Parse("101010"));
                DrawAccentWindow(canvas,
                    new SKRect(centerX - (size * 0.055f), bodyRect.Top + (size * 0.14f), centerX + (size * 0.055f), bodyRect.Top + (size * 0.42f)),
                    accentColor);
                DrawMetalNib(canvas, centerX, nibTop, bodyTop);
                break;
            }
            case ToolIconKind.Fountain:
            {
                var bodyRect = DrawTubeBody(canvas, centerX, bodyTop - (size * 0.01f), size * 0.27f, bodyBottom - bodyTop + (size * 0.01f), size * 0.10f);
                DrawGripBand(canvas, bodyRect, 0.60f, SKColor.Parse("101010"));
                DrawAccentBand(canvas, bodyRect, 0.16f, accentColor);
                DrawInkTip(canvas, centerX, nibTop, bodyTop);
                break;
            }
            case ToolIconKind.Pencil:
            {
                var bodyRect = DrawTubeBody(canvas, centerX, bodyTop, size * 0.27f, bodyBottom - bodyTop, size * 0.10f);
                DrawGripBand(canvas, bodyRect, 0.57f, SKColor.Parse("141414"));
                DrawAccentBand(canvas, bodyRect, 0.20f, accentColor);
                DrawPencilTip(canvas, centerX, nibTop, bodyTop);
                break;
            }
            case ToolIconKind.Marker:
            {
                var bodyRect = DrawTubeBody(canvas, centerX, size * 0.32f, size * 0.32f, size * 0.54f, size * 0.11f);
                DrawAccentCap(canvas, new SKRect(bodyRect.Left, size * 0.15f, bodyRect.Right, size * 0.32f), accentColor);
                DrawGripBand(canvas, bodyRect, 0.49f, Darken(accentColor, 0.1f));
                DrawChiselTip(canvas, centerX, size * 0.15f, size * 0.32f, accentColor);
                break;
            }
            case ToolIconKind.Eraser:
            {
                DrawEraser(canvas, centerX, accentColor, size);
                break;
            }
        }
    }

    private static void DrawEraser(SKCanvas canvas, float centerX, SKColor accentColor, int size)
    {
        var bodyRect = DrawTubeBody(canvas, centerX, size * 0.30f, size * 0.30f, size * 0.48f, size * 0.12f);

        // Sleeve band
        var sleeveRect = new SKRect(bodyRect.Left + 2f, bodyRect.Top + (size * 0.16f), bodyRect.Right - 2f, bodyRect.Top + (size * 0.24f));
        using (var sleevePaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(sleeveRect.Left, sleeveRect.Top),
                new SKPoint(sleeveRect.Right, sleeveRect.Top),
                new[]
                {
                    SKColor.Parse("D9E3EF"),
                    SKColor.Parse("B6C4D5"),
                    SKColor.Parse("D9E3EF")
                },
                null,
                SKShaderTileMode.Clamp)
        })
        {
            canvas.DrawRoundRect(sleeveRect, 2f, 2f, sleevePaint);
        }

        // Rubber head (colored)
        var rubberRect = new SKRect(bodyRect.Left, size * 0.17f, bodyRect.Right, size * 0.30f);
        DrawAccentCap(canvas, rubberRect, accentColor);
    }

    private static SKRect DrawTubeBody(SKCanvas canvas, float centerX, float top, float width, float height, float radius)
    {
        var rect = SKRect.Create(centerX - (width / 2f), top, width, height);
        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.MidY),
                new SKPoint(rect.Right, rect.MidY),
                new[]
                {
                    SKColor.Parse("FFFFFF"),
                    SKColor.Parse("E8EDF4"),
                    SKColor.Parse("FFFFFF")
                },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRoundRect(rect, radius, radius, fillPaint);

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.15f,
            Color = SKColor.Parse("CDD6E2")
        };
        canvas.DrawRoundRect(rect, radius, radius, strokePaint);

        var highlight = new SKRect(rect.Left + (width * 0.16f), rect.Top + 4f, rect.Left + (width * 0.26f), rect.Bottom - 6f);
        using var highlightPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, 138)
        };
        canvas.DrawRoundRect(highlight, 2f, 2f, highlightPaint);
        return rect;
    }

    private static void DrawGroundShadow(SKCanvas canvas, SKRect rect)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(rect.MidX, rect.MidY),
                rect.Width * 0.45f,
                new[]
                {
                    new SKColor(12, 24, 40, 54),
                    new SKColor(12, 24, 40, 0)
                },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawOval(rect, paint);
    }

    private static void DrawGripBand(SKCanvas canvas, SKRect bodyRect, float verticalRatio, SKColor toneColor)
    {
        var y = bodyRect.Top + (bodyRect.Height * verticalRatio);
        var rect = new SKRect(bodyRect.Left + 1.6f, y, bodyRect.Right - 1.6f, y + 5f);
        var centerColor = Darken(toneColor, 0.1f);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Right, rect.Top),
                new[]
                {
                    Darken(centerColor, 0.45f),
                    centerColor,
                    Darken(centerColor, 0.45f)
                },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRoundRect(rect, 1.8f, 1.8f, paint);
    }

    private static void DrawAccentWindow(SKCanvas canvas, SKRect rect, SKColor accentColor)
    {
        var left = Lighten(accentColor, 0.20f);
        var right = Darken(accentColor, 0.28f);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.MidY),
                new SKPoint(rect.Right, rect.MidY),
                new[] { left, accentColor, right },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRoundRect(rect, 2f, 2f, paint);
    }

    private static void DrawAccentBand(SKCanvas canvas, SKRect bodyRect, float verticalRatio, SKColor accentColor)
    {
        var y = bodyRect.Top + (bodyRect.Height * verticalRatio);
        var rect = new SKRect(bodyRect.Left + 1.8f, y, bodyRect.Right - 1.8f, y + 5.5f);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Right, rect.Top),
                new[]
                {
                    Darken(accentColor, 0.26f),
                    accentColor,
                    Darken(accentColor, 0.26f)
                },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRoundRect(rect, 2f, 2f, paint);
    }

    private static void DrawAccentCap(SKCanvas canvas, SKRect rect, SKColor accentColor)
    {
        var topColor = Lighten(accentColor, 0.22f);
        var bottomColor = Darken(accentColor, 0.16f);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.MidX, rect.Top),
                new SKPoint(rect.MidX, rect.Bottom),
                new[] { topColor, accentColor, bottomColor },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRoundRect(rect, 6f, 6f, paint);

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = Darken(accentColor, 0.35f)
        };
        canvas.DrawRoundRect(rect, 6f, 6f, strokePaint);
    }

    private static void DrawMetalNib(SKCanvas canvas, float centerX, float top, float bottom)
    {
        using var nibPath = new SKPath();
        nibPath.MoveTo(centerX, top);
        nibPath.LineTo(centerX - 8.2f, bottom);
        nibPath.LineTo(centerX + 8.2f, bottom);
        nibPath.Close();

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(centerX, top),
                new SKPoint(centerX, bottom),
                new[]
                {
                    SKColor.Parse("96A2B2"),
                    SKColor.Parse("E8EDF3"),
                    SKColor.Parse("798597")
                },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawPath(nibPath, paint);

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = SKColor.Parse("667386")
        };
        canvas.DrawPath(nibPath, strokePaint);

        using var splitPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("5F6C7E"),
            StrokeWidth = 0.9f
        };
        canvas.DrawLine(centerX, top + 3f, centerX, bottom - 2f, splitPaint);
    }

    private static void DrawInkTip(SKCanvas canvas, float centerX, float top, float bottom)
    {
        using var tipPath = new SKPath();
        tipPath.MoveTo(centerX, top);
        tipPath.LineTo(centerX - 7.2f, bottom);
        tipPath.LineTo(centerX + 7.2f, bottom);
        tipPath.Close();

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(centerX, top),
                new SKPoint(centerX, bottom),
                new[]
                {
                    SKColor.Parse("1E2128"),
                    SKColor.Parse("050608")
                },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawPath(tipPath, paint);

        using var highlightPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, 88),
            StrokeWidth = 0.9f
        };
        canvas.DrawLine(centerX - 1.2f, top + 3f, centerX - 2.2f, bottom - 4f, highlightPaint);
    }

    private static void DrawPencilTip(SKCanvas canvas, float centerX, float top, float bottom)
    {
        using var woodPath = new SKPath();
        woodPath.MoveTo(centerX, top);
        woodPath.LineTo(centerX - 7.4f, bottom);
        woodPath.LineTo(centerX + 7.4f, bottom);
        woodPath.Close();

        using var woodPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(centerX, top),
                new SKPoint(centerX, bottom),
                new[]
                {
                    SKColor.Parse("F2C799"),
                    SKColor.Parse("D39A67")
                },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawPath(woodPath, woodPaint);

        using var leadPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("141518")
        };
        canvas.DrawCircle(centerX, top + 0.9f, 2.2f, leadPaint);
    }

    private static void DrawChiselTip(SKCanvas canvas, float centerX, float top, float bottom, SKColor accentColor)
    {
        using var tipPath = new SKPath();
        tipPath.MoveTo(centerX - 9f, bottom - 2f);
        tipPath.LineTo(centerX + 8f, bottom - 6f);
        tipPath.LineTo(centerX + 8f, top + 3f);
        tipPath.LineTo(centerX - 9f, top + 7f);
        tipPath.Close();

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(centerX - 9f, top + 3f),
                new SKPoint(centerX + 8f, bottom - 2f),
                new[]
                {
                    Lighten(accentColor, 0.12f),
                    Darken(accentColor, 0.22f)
                },
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawPath(tipPath, paint);
    }

    private static SKColor Lighten(SKColor color, float amount)
    {
        return LerpColor(color, SKColors.White, Math.Clamp(amount, 0f, 1f));
    }

    private static SKColor Darken(SKColor color, float amount)
    {
        return LerpColor(color, SKColors.Black, Math.Clamp(amount, 0f, 1f));
    }

    private static SKColor LerpColor(SKColor from, SKColor to, float amount)
    {
        var clamped = Math.Clamp(amount, 0f, 1f);
        var r = (byte)Math.Round(from.Red + ((to.Red - from.Red) * clamped));
        var g = (byte)Math.Round(from.Green + ((to.Green - from.Green) * clamped));
        var b = (byte)Math.Round(from.Blue + ((to.Blue - from.Blue) * clamped));
        var a = (byte)Math.Round(from.Alpha + ((to.Alpha - from.Alpha) * clamped));
        return new SKColor(r, g, b, a);
    }
}
