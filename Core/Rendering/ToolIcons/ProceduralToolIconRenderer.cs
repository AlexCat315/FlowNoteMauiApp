using SkiaSharp;

namespace FlowNoteMauiApp.Core.Rendering.ToolIcons;

public static class ProceduralToolIconRenderer
{
    public static SKBitmap Render(ToolIconKind toolKind, SKColor accentColor, int size = 256)
    {
        var outputSize = Math.Clamp(size, 64, 512);
        var workingSize = Math.Clamp(outputSize * 2, 128, 1024);
        using var workingBitmap = new SKBitmap(workingSize, workingSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(workingBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var supersampleScale = workingSize / (float)outputSize;
            canvas.Scale(supersampleScale, supersampleScale);
            DrawToolScene(canvas, toolKind, accentColor, outputSize);
            canvas.Flush();
        }

        if (workingSize == outputSize)
            return workingBitmap.Copy();

        var output = new SKBitmap(outputSize, outputSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var workingImage = SKImage.FromBitmap(workingBitmap);
        using var outputCanvas = new SKCanvas(output);
        using var paint = new SKPaint { IsAntialias = true, IsDither = true };
        var sampling = new SKSamplingOptions(new SKCubicResampler(0.333f, 0.333f));
        outputCanvas.Clear(SKColors.Transparent);
        outputCanvas.DrawImage(workingImage, new SKRect(0f, 0f, outputSize, outputSize), sampling, paint);
        outputCanvas.Flush();
        return output;
    }

    private static void DrawToolScene(SKCanvas canvas, ToolIconKind toolKind, SKColor accentColor, int size)
    {
        // Draw the tool upside-down in local coordinates, then flip vertically so the tip points down.
        canvas.Save();
        canvas.Translate(0, size);
        canvas.Scale(1f, -1f);
        DrawToolUpright(canvas, toolKind, accentColor, size);
        canvas.Restore();

        var shadowLeft = size * 0.24f;
        var shadowTop = size * 0.84f;
        var shadowRight = size * 0.76f;
        var shadowBottom = size * 0.94f;
        DrawGroundShadow(canvas, new SKRect(shadowLeft, shadowTop, shadowRight, shadowBottom));
    }

    private static void DrawToolUpright(SKCanvas canvas, ToolIconKind toolKind, SKColor accentColor, int size)
    {
        var unit = size / 96f;
        var centerX = size * 0.5f;
        var bodyTop = size * 0.26f;
        var bodyBottom = size * 0.86f;
        var nibTop = size * 0.08f;

        switch (toolKind)
        {
            case ToolIconKind.Ballpoint:
            {
                var bodyRect = DrawTubeBody(canvas, centerX, bodyTop, size * 0.28f, bodyBottom - bodyTop, size * 0.10f, unit);
                DrawGripBand(canvas, bodyRect, 0.58f, SKColor.Parse("101010"), unit);
                DrawAccentWindow(canvas,
                    new SKRect(centerX - (size * 0.055f), bodyRect.Top + (size * 0.14f), centerX + (size * 0.055f), bodyRect.Top + (size * 0.42f)),
                    accentColor,
                    unit);
                DrawMetalNib(canvas, centerX, nibTop, bodyTop, unit);
                break;
            }
            case ToolIconKind.Fountain:
            {
                var bodyRect = DrawTubeBody(canvas, centerX, bodyTop - (size * 0.01f), size * 0.27f, bodyBottom - bodyTop + (size * 0.01f), size * 0.10f, unit);
                DrawGripBand(canvas, bodyRect, 0.60f, SKColor.Parse("101010"), unit);
                DrawAccentBand(canvas, bodyRect, 0.16f, accentColor, unit);
                DrawInkTip(canvas, centerX, nibTop, bodyTop, unit);
                break;
            }
            case ToolIconKind.Pencil:
            {
                var bodyRect = DrawTubeBody(canvas, centerX, bodyTop, size * 0.27f, bodyBottom - bodyTop, size * 0.10f, unit);
                DrawGripBand(canvas, bodyRect, 0.57f, SKColor.Parse("141414"), unit);
                DrawAccentBand(canvas, bodyRect, 0.20f, accentColor, unit);
                DrawPencilTip(canvas, centerX, nibTop, bodyTop, unit);
                break;
            }
            case ToolIconKind.Marker:
            {
                var bodyRect = DrawTubeBody(canvas, centerX, size * 0.32f, size * 0.32f, size * 0.54f, size * 0.11f, unit);
                DrawAccentCap(canvas, new SKRect(bodyRect.Left, size * 0.15f, bodyRect.Right, size * 0.32f), accentColor, unit);
                DrawGripBand(canvas, bodyRect, 0.49f, Darken(accentColor, 0.1f), unit);
                DrawChiselTip(canvas, centerX, size * 0.15f, size * 0.32f, accentColor, unit);
                break;
            }
            case ToolIconKind.Eraser:
            {
                DrawEraser(canvas, centerX, accentColor, size, unit);
                break;
            }
        }
    }

    private static void DrawEraser(SKCanvas canvas, float centerX, SKColor accentColor, int size, float unit)
    {
        var bodyRect = DrawTubeBody(canvas, centerX, size * 0.30f, size * 0.30f, size * 0.48f, size * 0.12f, unit);

        // Sleeve band
        var sleevePad = 2f * unit;
        var sleeveRect = new SKRect(bodyRect.Left + sleevePad, bodyRect.Top + (size * 0.16f), bodyRect.Right - sleevePad, bodyRect.Top + (size * 0.24f));
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
            var sleeveRadius = 2f * unit;
            canvas.DrawRoundRect(sleeveRect, sleeveRadius, sleeveRadius, sleevePaint);
        }

        // Rubber head (colored)
        var rubberRect = new SKRect(bodyRect.Left, size * 0.17f, bodyRect.Right, size * 0.30f);
        DrawAccentCap(canvas, rubberRect, accentColor, unit);
    }

    private static SKRect DrawTubeBody(SKCanvas canvas, float centerX, float top, float width, float height, float radius, float unit)
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
            StrokeWidth = Math.Max(1f, 1.15f * unit),
            Color = SKColor.Parse("CDD6E2")
        };
        canvas.DrawRoundRect(rect, radius, radius, strokePaint);

        var highlight = new SKRect(
            rect.Left + (width * 0.16f),
            rect.Top + (4f * unit),
            rect.Left + (width * 0.26f),
            rect.Bottom - (6f * unit));
        using var highlightPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, 138)
        };
        var highlightRadius = 2f * unit;
        canvas.DrawRoundRect(highlight, highlightRadius, highlightRadius, highlightPaint);
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

    private static void DrawGripBand(SKCanvas canvas, SKRect bodyRect, float verticalRatio, SKColor toneColor, float unit)
    {
        var y = bodyRect.Top + (bodyRect.Height * verticalRatio);
        var horizontalInset = 1.6f * unit;
        var rect = new SKRect(bodyRect.Left + horizontalInset, y, bodyRect.Right - horizontalInset, y + (5f * unit));
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
        var radius = 1.8f * unit;
        canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    private static void DrawAccentWindow(SKCanvas canvas, SKRect rect, SKColor accentColor, float unit)
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
        var radius = 2f * unit;
        canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    private static void DrawAccentBand(SKCanvas canvas, SKRect bodyRect, float verticalRatio, SKColor accentColor, float unit)
    {
        var y = bodyRect.Top + (bodyRect.Height * verticalRatio);
        var horizontalInset = 1.8f * unit;
        var rect = new SKRect(bodyRect.Left + horizontalInset, y, bodyRect.Right - horizontalInset, y + (5.5f * unit));
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
        var radius = 2f * unit;
        canvas.DrawRoundRect(rect, radius, radius, paint);
    }

    private static void DrawAccentCap(SKCanvas canvas, SKRect rect, SKColor accentColor, float unit)
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
        var radius = 6f * unit;
        canvas.DrawRoundRect(rect, radius, radius, paint);

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1f, 1f * unit),
            Color = Darken(accentColor, 0.35f)
        };
        canvas.DrawRoundRect(rect, radius, radius, strokePaint);
    }

    private static void DrawMetalNib(SKCanvas canvas, float centerX, float top, float bottom, float unit)
    {
        using var nibPath = new SKPath();
        nibPath.MoveTo(centerX, top);
        nibPath.LineTo(centerX - (8.2f * unit), bottom);
        nibPath.LineTo(centerX + (8.2f * unit), bottom);
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
            StrokeWidth = Math.Max(1f, 1f * unit),
            Color = SKColor.Parse("667386")
        };
        canvas.DrawPath(nibPath, strokePaint);

        using var splitPaint = new SKPaint
        {
            IsAntialias = true,
            Color = SKColor.Parse("5F6C7E"),
            StrokeWidth = Math.Max(1f, 0.9f * unit)
        };
        canvas.DrawLine(centerX, top + (3f * unit), centerX, bottom - (2f * unit), splitPaint);
    }

    private static void DrawInkTip(SKCanvas canvas, float centerX, float top, float bottom, float unit)
    {
        using var tipPath = new SKPath();
        tipPath.MoveTo(centerX, top);
        tipPath.LineTo(centerX - (7.2f * unit), bottom);
        tipPath.LineTo(centerX + (7.2f * unit), bottom);
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
            StrokeWidth = Math.Max(1f, 0.9f * unit)
        };
        canvas.DrawLine(
            centerX - (1.2f * unit),
            top + (3f * unit),
            centerX - (2.2f * unit),
            bottom - (4f * unit),
            highlightPaint);
    }

    private static void DrawPencilTip(SKCanvas canvas, float centerX, float top, float bottom, float unit)
    {
        using var woodPath = new SKPath();
        woodPath.MoveTo(centerX, top);
        woodPath.LineTo(centerX - (7.4f * unit), bottom);
        woodPath.LineTo(centerX + (7.4f * unit), bottom);
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
        canvas.DrawCircle(centerX, top + (0.9f * unit), 2.2f * unit, leadPaint);
    }

    private static void DrawChiselTip(SKCanvas canvas, float centerX, float top, float bottom, SKColor accentColor, float unit)
    {
        using var tipPath = new SKPath();
        tipPath.MoveTo(centerX - (9f * unit), bottom - (2f * unit));
        tipPath.LineTo(centerX + (8f * unit), bottom - (6f * unit));
        tipPath.LineTo(centerX + (8f * unit), top + (3f * unit));
        tipPath.LineTo(centerX - (9f * unit), top + (7f * unit));
        tipPath.Close();

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(centerX - (9f * unit), top + (3f * unit)),
                new SKPoint(centerX + (8f * unit), bottom - (2f * unit)),
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
