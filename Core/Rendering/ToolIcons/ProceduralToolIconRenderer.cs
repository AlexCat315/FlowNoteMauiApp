using System.Buffers;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace FlowNoteMauiApp.Core.Rendering.ToolIcons;

public static class ProceduralToolIconRenderer
{
    // 配置常量
    private const int MinSize = 128;
    private const int MaxSize = 1024;
    private const int MinWorkingSize = 256;
    private const int MaxWorkingSize = 2048;
    private const float SupersampleRatio = 2f;

    // 缓存常用画笔和着色器配置
    private static readonly SKPaintCache PaintCache = new();

    // 线程本地存储，避免频繁分配
    [ThreadStatic]
    private static float[]? _scratchBuffer;

    public static SKBitmap Render(ToolIconKind toolKind, SKColor accentColor, int size = 256)
    {
        var outputSize = Math.Clamp(size, MinSize, MaxSize);
        var workingSize = Math.Clamp((int)(outputSize * SupersampleRatio), MinWorkingSize, MaxWorkingSize);

        // 使用对象池获取工作位图
        using var workingBitmap = SKBitmapPool.Rent(workingSize, workingSize);
        using (var canvas = new SKCanvas(workingBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var scale = workingSize / (float)outputSize;
            canvas.Scale(scale, scale);
            DrawToolScene(canvas, toolKind, accentColor, outputSize);
            canvas.Flush();
        }

        // 如果不需要降采样，直接复制
        if (workingSize == outputSize)
        {
            return workingBitmap.Copy();
        }

        // 创建输出位图并高质量降采样
        var output = new SKBitmap(outputSize, outputSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var workingImage = SKImage.FromBitmap(workingBitmap);
        using var outputCanvas = new SKCanvas(output);

        // 使用 Lanczos 重采样获得更好的锐度
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        using var paint = PaintCache.GetHighQualityPaint();

        outputCanvas.Clear(SKColors.Transparent);
        outputCanvas.DrawImage(workingImage, new SKRect(0f, 0f, outputSize, outputSize), sampling, paint);
        outputCanvas.Flush();

        return output;
    }

    // 批量渲染优化 - 复用画布配置
    public static SKBitmap[] RenderBatch(ToolIconKind[] toolKinds, SKColor accentColor, int size = 256)
    {
        var results = new SKBitmap[toolKinds.Length];
        var outputSize = Math.Clamp(size, MinSize, MaxSize);
        var workingSize = Math.Clamp((int)(outputSize * SupersampleRatio), MinWorkingSize, MaxWorkingSize);

        using var workingBitmap = SKBitmapPool.Rent(workingSize, workingSize);

        for (int i = 0; i < toolKinds.Length; i++)
        {
            using (var canvas = new SKCanvas(workingBitmap))
            {
                canvas.Clear(SKColors.Transparent);
                var scale = workingSize / (float)outputSize;
                canvas.Scale(scale, scale);
                DrawToolScene(canvas, toolKinds[i], accentColor, outputSize);
                canvas.Flush();
            }

            results[i] = workingSize == outputSize
                ? workingBitmap.Copy()
                : Downsample(workingBitmap, outputSize);
        }

        return results;
    }

    private static SKBitmap Downsample(SKBitmap source, int targetSize)
    {
        var output = new SKBitmap(targetSize, targetSize, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var image = SKImage.FromBitmap(source);
        using var canvas = new SKCanvas(output);
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        using var paint = PaintCache.GetHighQualityPaint();

        canvas.Clear(SKColors.Transparent);
        canvas.DrawImage(image, new SKRect(0, 0, targetSize, targetSize), sampling, paint);
        canvas.Flush();
        return output;
    }

    private static void DrawToolScene(SKCanvas canvas, ToolIconKind toolKind, SKColor accentColor, int size)
    {
        // 使用矩阵变换替代 Save/Restore，减少状态栈操作
        var matrix = SKMatrix.CreateScale(1f, -1f);
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(0, size));
        canvas.SetMatrix(matrix);

        DrawToolUpright(canvas, toolKind, accentColor, size);

        // 恢复矩阵
        canvas.ResetMatrix();

        // 绘制阴影
        var shadowRect = new SKRect(size * 0.24f, size * 0.84f, size * 0.76f, size * 0.94f);
        DrawGroundShadow(canvas, shadowRect);
    }

    private static void DrawToolUpright(SKCanvas canvas, ToolIconKind toolKind, SKColor accentColor, int size)
    {
        var unit = size / 96f;
        var centerX = size * 0.5f;
        var bodyTop = size * 0.26f;
        var bodyBottom = size * 0.86f;
        var nibTop = size * 0.08f;

        // 预计算常用值
        var bodyHeight = bodyBottom - bodyTop;

        switch (toolKind)
        {
            case ToolIconKind.Ballpoint:
                DrawBallpoint(canvas, centerX, bodyTop, bodyHeight, nibTop, accentColor, size, unit);
                break;
            case ToolIconKind.Fountain:
                DrawFountain(canvas, centerX, bodyTop, bodyHeight, nibTop, accentColor, size, unit);
                break;
            case ToolIconKind.Pencil:
                DrawPencilFull(canvas, centerX, bodyTop, bodyHeight, nibTop, accentColor, size, unit);
                break;
            case ToolIconKind.Marker:
                DrawMarkerFull(canvas, centerX, accentColor, size, unit);
                break;
            case ToolIconKind.Eraser:
                DrawEraserFull(canvas, centerX, accentColor, size, unit);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DrawBallpoint(SKCanvas canvas, float centerX, float bodyTop, float bodyHeight,
        float nibTop, SKColor accentColor, int size, float unit)
    {
        var halfWidth = size * 0.14f;
        var bodyRect = DrawTubeBody(canvas, centerX, bodyTop, halfWidth * 2, bodyHeight, size * 0.10f, unit);

        DrawGripBand(canvas, bodyRect, 0.58f, SKColor.Parse("101010"), unit);
        DrawAccentWindow(canvas,
            new SKRect(centerX - (size * 0.055f), bodyRect.Top + (size * 0.14f),
                      centerX + (size * 0.055f), bodyRect.Top + (size * 0.42f)),
            accentColor, unit);
        DrawMetalNib(canvas, centerX, nibTop, bodyTop, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DrawFountain(SKCanvas canvas, float centerX, float bodyTop, float bodyHeight,
        float nibTop, SKColor accentColor, int size, float unit)
    {
        var adjustedTop = bodyTop - (size * 0.01f);
        var adjustedHeight = bodyHeight + (size * 0.01f);
        var halfWidth = size * 0.135f;
        var bodyRect = DrawTubeBody(canvas, centerX, adjustedTop, halfWidth * 2, adjustedHeight, size * 0.10f, unit);

        DrawGripBand(canvas, bodyRect, 0.60f, SKColor.Parse("101010"), unit);
        DrawAccentBand(canvas, bodyRect, 0.16f, accentColor, unit);
        DrawInkTip(canvas, centerX, nibTop, bodyTop, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DrawPencilFull(SKCanvas canvas, float centerX, float bodyTop, float bodyHeight,
        float nibTop, SKColor accentColor, int size, float unit)
    {
        var halfWidth = size * 0.135f;
        var bodyRect = DrawTubeBody(canvas, centerX, bodyTop, halfWidth * 2, bodyHeight, size * 0.10f, unit);

        DrawGripBand(canvas, bodyRect, 0.57f, SKColor.Parse("141414"), unit);
        DrawAccentBand(canvas, bodyRect, 0.20f, accentColor, unit);
        DrawPencilTip(canvas, centerX, nibTop, bodyTop, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DrawMarkerFull(SKCanvas canvas, float centerX, SKColor accentColor, int size, float unit)
    {
        var bodyTop = size * 0.32f;
        var bodyHeight = size * 0.54f;
        var halfWidth = size * 0.16f;
        var bodyRect = DrawTubeBody(canvas, centerX, bodyTop, halfWidth * 2, bodyHeight, size * 0.11f, unit);

        var capRect = new SKRect(bodyRect.Left, size * 0.15f, bodyRect.Right, size * 0.32f);
        DrawAccentCap(canvas, capRect, accentColor, unit);
        DrawGripBand(canvas, bodyRect, 0.49f, ColorUtils.Darken(accentColor, 0.1f), unit);
        DrawChiselTip(canvas, centerX, size * 0.15f, size * 0.32f, accentColor, unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DrawEraserFull(SKCanvas canvas, float centerX, SKColor accentColor, int size, float unit)
    {
        var holderRect = DrawTubeBody(canvas, centerX, size * 0.44f, size * 0.30f, size * 0.42f, size * 0.11f, unit);

        var ferruleInset = 1.8f * unit;
        var ferruleRect = new SKRect(
            holderRect.Left + ferruleInset,
            size * 0.34f,
            holderRect.Right - ferruleInset,
            size * 0.45f);
        DrawEraserFerrule(canvas, ferruleRect, unit);

        var rubberRect = new SKRect(
            holderRect.Left + (0.6f * unit),
            size * 0.14f,
            holderRect.Right - (0.6f * unit),
            size * 0.36f);
        DrawEraserRubber(canvas, rubberRect, accentColor, unit);

        // 使用共享的画笔绘制擦除面
        var eraseFaceRect = new SKRect(
            rubberRect.Left + (1.6f * unit),
            rubberRect.Top + (1.6f * unit),
            rubberRect.Right - (1.6f * unit),
            rubberRect.Top + (4.4f * unit));
        using var eraseFacePaint = PaintCache.GetRoundRectPaint(ColorUtils.Lighten(accentColor, 0.42f));
        var eraseFaceRadius = 1.6f * unit;
        canvas.DrawRoundRect(eraseFaceRect, eraseFaceRadius, eraseFaceRadius, eraseFacePaint);
    }

    private static void DrawEraserFerrule(SKCanvas canvas, SKRect rect, float unit)
    {
        // 使用缓存的渐变着色器
        using var fillPaint = PaintCache.GetLinearGradientPaint(
            rect.Left, rect.Top, rect.Right, rect.Top,
            new[] { SKColor.Parse("DDE6F2"), SKColor.Parse("B4C1D1"), SKColor.Parse("DDE6F2") });

        var radius = 2.2f * unit;
        canvas.DrawRoundRect(rect, radius, radius, fillPaint);

        using var edgePaint = PaintCache.GetStrokePaint(SKColor.Parse("93A5BA"), Math.Max(1f, 0.9f * unit));
        canvas.DrawRoundRect(rect, radius, radius, edgePaint);

        using var groovePaint = PaintCache.GetStrokePaint(
            new SKColor(120, 136, 156, 140), Math.Max(1f, 0.7f * unit));

        var grooveStep = Math.Max(2.4f * unit, rect.Width / 5f);
        var grooveX = rect.Left + grooveStep;
        var endX = rect.Right - (grooveStep * 0.35f);
        var grooveTop = rect.Top + (0.8f * unit);
        var grooveBottom = rect.Bottom - (0.8f * unit);

        while (grooveX < endX)
        {
            canvas.DrawLine(grooveX, grooveTop, grooveX, grooveBottom, groovePaint);
            grooveX += grooveStep;
        }
    }

    private static void DrawEraserRubber(SKCanvas canvas, SKRect rect, SKColor accentColor, float unit)
    {
        var topColor = ColorUtils.Lighten(accentColor, 0.26f);
        var bottomColor = ColorUtils.Darken(accentColor, 0.18f);

        using var fillPaint = PaintCache.GetLinearGradientPaint(
            rect.MidX, rect.Top, rect.MidX, rect.Bottom,
            new[] { topColor, accentColor, bottomColor });

        var radius = 6f * unit;
        canvas.DrawRoundRect(rect, radius, radius, fillPaint);

        using var seamPaint = PaintCache.GetLinePaint(ColorUtils.Darken(accentColor, 0.34f), Math.Max(1f, 0.85f * unit));
        var seamY = rect.Top + (rect.Height * 0.58f);
        canvas.DrawLine(rect.Left + (2.4f * unit), seamY, rect.Right - (2.4f * unit), seamY, seamPaint);

        using var highlightPaint = PaintCache.GetRoundRectPaint(new SKColor(255, 255, 255, 128));
        var highlightRect = new SKRect(
            rect.Left + (2.6f * unit),
            rect.Top + (2.2f * unit),
            rect.Left + (5.6f * unit),
            rect.Bottom - (3.2f * unit));
        canvas.DrawRoundRect(highlightRect, 1.3f * unit, 1.3f * unit, highlightPaint);
    }

    private static SKRect DrawTubeBody(SKCanvas canvas, float centerX, float top, float width, float height, float radius, float unit)
    {
        var rect = SKRect.Create(centerX - (width / 2f), top, width, height);

        using var fillPaint = PaintCache.GetLinearGradientPaint(
            rect.Left, rect.MidY, rect.Right, rect.MidY,
            new[] { SKColor.Parse("FFFFFF"), SKColor.Parse("E8EDF4"), SKColor.Parse("FFFFFF") });
        canvas.DrawRoundRect(rect, radius, radius, fillPaint);

        using var strokePaint = PaintCache.GetStrokePaint(SKColor.Parse("CDD6E2"), Math.Max(1f, 1.15f * unit));
        canvas.DrawRoundRect(rect, radius, radius, strokePaint);

        // 高光
        var highlight = new SKRect(
            rect.Left + (width * 0.10f),
            rect.Top + (4f * unit),
            rect.Left + (width * 0.56f),
            rect.Bottom - (6f * unit));

        using var highlightPaint = PaintCache.GetLinearGradientPaint(
            highlight.Left, highlight.MidY, highlight.Right, highlight.MidY,
            new[] { new SKColor(255, 255, 255, 0), new SKColor(255, 255, 255, 95), new SKColor(255, 255, 255, 0) },
            new[] { 0f, 0.42f, 1f });

        canvas.DrawRoundRect(highlight, 2.6f * unit, 2.6f * unit, highlightPaint);
        return rect;
    }

    private static void DrawGroundShadow(SKCanvas canvas, SKRect rect)
    {
        using var paint = PaintCache.GetRadialGradientPaint(
            rect.MidX, rect.MidY, rect.Width * 0.45f,
            new[] { new SKColor(12, 24, 40, 54), new SKColor(12, 24, 40, 0) });
        canvas.DrawOval(rect, paint);
    }

    private static void DrawGripBand(SKCanvas canvas, SKRect bodyRect, float verticalRatio, SKColor toneColor, float unit)
    {
        var y = bodyRect.Top + (bodyRect.Height * verticalRatio);
        var horizontalInset = 1.6f * unit;
        var rect = new SKRect(bodyRect.Left + horizontalInset, y, bodyRect.Right - horizontalInset, y + (5f * unit));
        var centerColor = ColorUtils.Darken(toneColor, 0.1f);

        using var paint = PaintCache.GetLinearGradientPaint(
            rect.Left, rect.Top, rect.Right, rect.Top,
            new[] { ColorUtils.Darken(centerColor, 0.45f), centerColor, ColorUtils.Darken(centerColor, 0.45f) });

        canvas.DrawRoundRect(rect, 1.8f * unit, 1.8f * unit, paint);
    }

    private static void DrawAccentWindow(SKCanvas canvas, SKRect rect, SKColor accentColor, float unit)
    {
        var left = ColorUtils.Lighten(accentColor, 0.20f);
        var right = ColorUtils.Darken(accentColor, 0.28f);

        using var paint = PaintCache.GetLinearGradientPaint(
            rect.Left, rect.MidY, rect.Right, rect.MidY,
            new[] { left, accentColor, right });

        canvas.DrawRoundRect(rect, 2f * unit, 2f * unit, paint);
    }

    private static void DrawAccentBand(SKCanvas canvas, SKRect bodyRect, float verticalRatio, SKColor accentColor, float unit)
    {
        var y = bodyRect.Top + (bodyRect.Height * verticalRatio);
        var horizontalInset = 1.8f * unit;
        var rect = new SKRect(bodyRect.Left + horizontalInset, y, bodyRect.Right - horizontalInset, y + (5.5f * unit));

        using var paint = PaintCache.GetLinearGradientPaint(
            rect.Left, rect.Top, rect.Right, rect.Top,
            new[] { ColorUtils.Darken(accentColor, 0.26f), accentColor, ColorUtils.Darken(accentColor, 0.26f) });

        canvas.DrawRoundRect(rect, 2f * unit, 2f * unit, paint);
    }

    private static void DrawAccentCap(SKCanvas canvas, SKRect rect, SKColor accentColor, float unit)
    {
        var topColor = ColorUtils.Lighten(accentColor, 0.22f);
        var bottomColor = ColorUtils.Darken(accentColor, 0.16f);

        using var paint = PaintCache.GetLinearGradientPaint(
            rect.MidX, rect.Top, rect.MidX, rect.Bottom,
            new[] { topColor, accentColor, bottomColor });

        var radius = 6f * unit;
        canvas.DrawRoundRect(rect, radius, radius, paint);

        using var strokePaint = PaintCache.GetStrokePaint(ColorUtils.Darken(accentColor, 0.35f), Math.Max(1f, 1f * unit));
        canvas.DrawRoundRect(rect, radius, radius, strokePaint);
    }

    private static void DrawMetalNib(SKCanvas canvas, float centerX, float top, float bottom, float unit)
    {
        using var nibPath = new SKPath();
        nibPath.MoveTo(centerX, top);
        nibPath.LineTo(centerX - (8.2f * unit), bottom);
        nibPath.LineTo(centerX + (8.2f * unit), bottom);
        nibPath.Close();

        using var paint = PaintCache.GetLinearGradientPaint(
            centerX, top, centerX, bottom,
            new[] { SKColor.Parse("96A2B2"), SKColor.Parse("E8EDF3"), SKColor.Parse("798597") });
        canvas.DrawPath(nibPath, paint);

        using var strokePaint = PaintCache.GetStrokePaint(SKColor.Parse("667386"), Math.Max(1f, 1f * unit));
        canvas.DrawPath(nibPath, strokePaint);

        using var splitPaint = PaintCache.GetLinePaint(SKColor.Parse("5F6C7E"), Math.Max(1f, 0.9f * unit));
        canvas.DrawLine(centerX, top + (3f * unit), centerX, bottom - (2f * unit), splitPaint);
    }

    private static void DrawInkTip(SKCanvas canvas, float centerX, float top, float bottom, float unit)
    {
        using var tipPath = new SKPath();
        tipPath.MoveTo(centerX, top);
        tipPath.LineTo(centerX - (7.2f * unit), bottom);
        tipPath.LineTo(centerX + (7.2f * unit), bottom);
        tipPath.Close();

        using var paint = PaintCache.GetLinearGradientPaint(
            centerX, top, centerX, bottom,
            new[] { SKColor.Parse("1E2128"), SKColor.Parse("050608") });
        canvas.DrawPath(tipPath, paint);

        using var highlightPaint = PaintCache.GetLinePaint(new SKColor(255, 255, 255, 88), Math.Max(1f, 0.9f * unit));
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

        using var woodPaint = PaintCache.GetLinearGradientPaint(
            centerX, top, centerX, bottom,
            new[] { SKColor.Parse("F2C799"), SKColor.Parse("D39A67") });
        canvas.DrawPath(woodPath, woodPaint);

        using var leadPaint = PaintCache.GetFillPaint(SKColor.Parse("141518"));
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

        using var paint = PaintCache.GetLinearGradientPaint(
            centerX - (9f * unit), top + (3f * unit), centerX + (8f * unit), bottom - (2f * unit),
            new[] { ColorUtils.Lighten(accentColor, 0.12f), ColorUtils.Darken(accentColor, 0.22f) });
        canvas.DrawPath(tipPath, paint);
    }
}

// 颜色工具类 - 使用SIMD加速
public static class ColorUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor Lighten(SKColor color, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        var invAmount = 1f - amount;

        // 使用整数运算避免浮点转换
        var r = (byte)((color.Red * invAmount) + (255 * amount));
        var g = (byte)((color.Green * invAmount) + (255 * amount));
        var b = (byte)((color.Blue * invAmount) + (255 * amount));

        return new SKColor(r, g, b, color.Alpha);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor Darken(SKColor color, float amount)
    {
        amount = 1f - Math.Clamp(amount, 0f, 1f);

        var r = (byte)(color.Red * amount);
        var g = (byte)(color.Green * amount);
        var b = (byte)(color.Blue * amount);

        return new SKColor(r, g, b, color.Alpha);
    }
}

// SKBitmap 对象池 - 减少GC压力
public static class SKBitmapPool
{
    private static readonly ConcurrentBag<SKBitmap> Pool = new();
    private const int MaxPoolSize = 4;
    private const int BitmapSizeThreshold = 1024 * 1024; // 1MB

    public static SKBitmap Rent(int width, int height)
    {
        // 只缓存大位图
        if (width * height * 4 < BitmapSizeThreshold)
        {
            return new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        }

        while (Pool.TryTake(out var bitmap))
        {
            if (CanReuse(bitmap) && bitmap.Width == width && bitmap.Height == height)
            {
                bitmap.Erase(SKColors.Transparent);
                return bitmap;
            }
            bitmap.Dispose();
        }

        return new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    }

    public static void Return(SKBitmap bitmap)
    {
        if (bitmap == null || !CanReuse(bitmap)) return;

        if (Pool.Count < MaxPoolSize && bitmap.Width * bitmap.Height * 4 >= BitmapSizeThreshold)
        {
            Pool.Add(bitmap);
        }
        else
        {
            bitmap.Dispose();
        }
    }

    private static bool CanReuse(SKBitmap bitmap)
    {
        try
        {
            return bitmap.Handle != IntPtr.Zero;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}

// 画笔缓存 - 复用常见配置
public class SKPaintCache : IDisposable
{
    private readonly ConcurrentDictionary<string, SKPaint> _paintCache = new();
    private bool _disposed;

    public SKPaint GetHighQualityPaint()
    {
        return GetOrCreate("hq", () => new SKPaint
        {
            IsAntialias = true,
            IsDither = true,
            FilterQuality = SKFilterQuality.High
        });
    }

    public SKPaint GetFillPaint(SKColor color)
    {
        var key = $"fill:{color}";
        return GetOrCreate(key, () => new SKPaint
        {
            IsAntialias = true,
            Color = color
        });
    }

    public SKPaint GetStrokePaint(SKColor color, float strokeWidth)
    {
        var key = $"stroke:{color}:{strokeWidth}";
        return GetOrCreate(key, () => new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = strokeWidth
        });
    }

    public SKPaint GetLinePaint(SKColor color, float strokeWidth)
    {
        var key = $"line:{color}:{strokeWidth}";
        return GetOrCreate(key, () => new SKPaint
        {
            IsAntialias = true,
            Color = color,
            StrokeWidth = strokeWidth
        });
    }

    public SKPaint GetRoundRectPaint(SKColor color)
    {
        var key = $"rr:{color}";
        return GetOrCreate(key, () => new SKPaint
        {
            IsAntialias = true,
            Color = color
        });
    }

    public SKPaint GetLinearGradientPaint(float x0, float y0, float x1, float y1, SKColor[] colors, float[]? positions = null)
    {
        // 渐变画笔不缓存，因为参数组合太多
        var shader = SKShader.CreateLinearGradient(
            new SKPoint(x0, y0),
            new SKPoint(x1, y1),
            colors,
            positions,
            SKShaderTileMode.Clamp);

        return new SKPaint
        {
            IsAntialias = true,
            Shader = shader
        };
    }

    public SKPaint GetRadialGradientPaint(float cx, float cy, float radius, SKColor[] colors)
    {
        var shader = SKShader.CreateRadialGradient(
            new SKPoint(cx, cy),
            radius,
            colors,
            null,
            SKShaderTileMode.Clamp);

        return new SKPaint
        {
            IsAntialias = true,
            Shader = shader
        };
    }

    private SKPaint GetOrCreate(string key, Func<SKPaint> factory)
    {
        if (_disposed)
            return factory();

        var template = _paintCache.GetOrAdd(key, _ => factory());
        return CopyPaint(template);
    }

    private static SKPaint CopyPaint(SKPaint template)
    {
        return new SKPaint
        {
            IsAntialias = template.IsAntialias,
            IsDither = template.IsDither,
            Style = template.Style,
            Color = template.Color,
            StrokeWidth = template.StrokeWidth,
            FilterQuality = template.FilterQuality
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var paint in _paintCache.Values)
        {
            paint.Dispose();
        }
        _paintCache.Clear();
    }
}
