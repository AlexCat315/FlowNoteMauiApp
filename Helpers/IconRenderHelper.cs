using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using SkiaSharp;

namespace FlowNoteMauiApp.Helpers;

public enum IconTintMode
{
    Monochrome,
    AccentPreserveShading
}

public static class IconRenderHelper
{
    private const float RecolorMinSaturation = 0.18f;
    private const float RecolorMinValue = 0.08f;
    private const float RecolorHueMin = 140f;
    private const float RecolorHueMax = 260f;
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte[]> TemplateBytesCache = new(StringComparer.Ordinal);

    public static void ClearCache()
    {
        Cache.Clear();
        TemplateBytesCache.Clear();
    }

    public static async Task<ImageSource> CreateTintedImageSourceFromPackageAsync(
        string iconFile,
        SKColor tintColor,
        IconTintMode tintMode,
        CancellationToken token = default)
    {
        var fallbackFile = Path.GetFileName(iconFile);
        var cacheKey = $"{iconFile}:{tintMode}:{tintColor.Alpha:X2}{tintColor.Red:X2}{tintColor.Green:X2}{tintColor.Blue:X2}";
        if (Cache.TryGetValue(cacheKey, out var cachedSource))
            return cachedSource;

        try
        {
            var templateBytes = await GetTemplateBytesAsync(iconFile, token).ConfigureAwait(false);
            if (templateBytes is null || templateBytes.Length == 0)
                return ImageSource.FromFile(fallbackFile);

            using var baseBitmap = SKBitmap.Decode(templateBytes);
            if (baseBitmap is null)
                return ImageSource.FromFile(fallbackFile);

            token.ThrowIfCancellationRequested();

            using var tintedBitmap = tintMode == IconTintMode.Monochrome
                ? RenderMonochrome(baseBitmap, tintColor)
                : RenderAccentPreserveShading(baseBitmap, tintColor, token);

            using var outputImage = SKImage.FromBitmap(tintedBitmap);
            using var outputData = outputImage.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = outputData.ToArray();
            var source = ImageSource.FromStream(() => new MemoryStream(bytes));
            Cache[cacheKey] = source;
            return source;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[IconRender] Failed to tint '{iconFile}': {ex.Message}");
            return ImageSource.FromFile(fallbackFile);
        }
    }

    public static Task<ImageSource> CreateProcedural3DToolIconAsync(
        string toolKey,
        SKColor accentColor,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var normalizedTool = NormalizeToolKey(toolKey);
        var cacheKey = $"proc3d:{normalizedTool}:{accentColor.Alpha:X2}{accentColor.Red:X2}{accentColor.Green:X2}{accentColor.Blue:X2}";
        if (Cache.TryGetValue(cacheKey, out var cachedSource))
            return Task.FromResult(cachedSource);

        using var bitmap = RenderProcedural3DToolIcon(normalizedTool, accentColor);
        using var outputImage = SKImage.FromBitmap(bitmap);
        using var outputData = outputImage.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = outputData.ToArray();
        var source = ImageSource.FromStream(() => new MemoryStream(bytes));
        Cache[cacheKey] = source;
        return Task.FromResult(source);
    }

    private static string NormalizeToolKey(string toolKey)
    {
        if (string.IsNullOrWhiteSpace(toolKey))
            return "ballpoint";

        return toolKey.Trim().ToLowerInvariant() switch
        {
            "pen" => "ballpoint",
            "gelpen" => "fountain",
            "highlighter" => "marker",
            _ => toolKey.Trim().ToLowerInvariant()
        };
    }

    private static SKBitmap RenderProcedural3DToolIcon(string toolKey, SKColor accentColor)
    {
        const int size = 96;
        var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        DrawGroundShadow(canvas, new SKRect(24, 82, 72, 90));

        switch (toolKey)
        {
            case "ballpoint":
                DrawBallpointIcon(canvas, accentColor);
                break;
            case "fountain":
                DrawFountainIcon(canvas, accentColor);
                break;
            case "pencil":
                DrawPencilIcon(canvas, accentColor);
                break;
            case "marker":
                DrawMarkerIcon(canvas, accentColor);
                break;
            default:
                DrawBallpointIcon(canvas, accentColor);
                break;
        }

        canvas.Flush();
        return bitmap;
    }

    private static void DrawBallpointIcon(SKCanvas canvas, SKColor accentColor)
    {
        const float centerX = 48f;
        var bodyRect = DrawTubeBody(canvas, centerX, top: 24f, width: 28f, height: 60f, radius: 10f);
        DrawGripBand(canvas, bodyRect, 0.58f, SKColor.Parse("111111"));
        DrawAccentWindow(canvas, new SKRect(centerX - 5.5f, bodyRect.Top + 14f, centerX + 5.5f, bodyRect.Top + 40f), accentColor);
        DrawMetalNib(canvas, centerX, top: 7f, bottom: 24f);
    }

    private static void DrawFountainIcon(SKCanvas canvas, SKColor accentColor)
    {
        const float centerX = 48f;
        var bodyRect = DrawTubeBody(canvas, centerX, top: 23f, width: 27f, height: 61f, radius: 10f);
        DrawGripBand(canvas, bodyRect, 0.58f, SKColor.Parse("101010"));
        DrawAccentBand(canvas, bodyRect, 0.15f, accentColor);
        DrawInkTip(canvas, centerX, top: 8f, bottom: 23f);
    }

    private static void DrawPencilIcon(SKCanvas canvas, SKColor accentColor)
    {
        const float centerX = 48f;
        var bodyRect = DrawTubeBody(canvas, centerX, top: 24f, width: 27f, height: 60f, radius: 10f);
        DrawGripBand(canvas, bodyRect, 0.56f, SKColor.Parse("111111"));
        DrawAccentBand(canvas, bodyRect, 0.18f, accentColor);
        DrawPencilTip(canvas, centerX, top: 8f, bottom: 24f);
    }

    private static void DrawMarkerIcon(SKCanvas canvas, SKColor accentColor)
    {
        const float centerX = 48f;
        var bodyRect = DrawTubeBody(canvas, centerX, top: 30f, width: 32f, height: 54f, radius: 11f);
        DrawAccentCap(canvas, new SKRect(bodyRect.Left, 14f, bodyRect.Right, 30f), accentColor);
        DrawGripBand(canvas, bodyRect, 0.47f, accentColor);
        DrawChiselTip(canvas, centerX, top: 14f, bottom: 30f, accentColor);
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

    private static SKBitmap RenderMonochrome(SKBitmap baseBitmap, SKColor tintColor)
    {
        var tintedBitmap = new SKBitmap(baseBitmap.Width, baseBitmap.Height, baseBitmap.ColorType, baseBitmap.AlphaType);
        using var canvas = new SKCanvas(tintedBitmap);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            ColorFilter = SKColorFilter.CreateBlendMode(tintColor, SKBlendMode.SrcIn)
        };
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(baseBitmap, 0, 0, paint);
        canvas.Flush();
        return tintedBitmap;
    }

    private static SKBitmap RenderAccentPreserveShading(SKBitmap baseBitmap, SKColor tintColor, CancellationToken token)
    {
        var sourcePixels = baseBitmap.Pixels;
        var tintedPixels = new SKColor[sourcePixels.Length];
        RgbToHsv(tintColor, out var tintHue, out var tintSaturation, out var tintValue);

        for (var i = 0; i < sourcePixels.Length; i++)
        {
            if ((i & 255) == 0)
            {
                token.ThrowIfCancellationRequested();
            }

            var source = sourcePixels[i];
            if (source.Alpha == 0)
            {
                tintedPixels[i] = SKColors.Transparent;
                continue;
            }

            RgbToHsv(source, out var hue, out var saturation, out var value);
            if (!ShouldRecolorAccentPixel(hue, saturation, value))
            {
                tintedPixels[i] = source;
                continue;
            }

            var target = CreateTintedVariant(source, hue, saturation, value, tintHue, tintSaturation, tintValue);
            var blendStrength = CalculateBlendStrength(saturation);
            tintedPixels[i] = LerpColor(source, target, blendStrength);
        }

        var tintedBitmap = new SKBitmap(baseBitmap.Width, baseBitmap.Height, baseBitmap.ColorType, baseBitmap.AlphaType);
        tintedBitmap.Pixels = tintedPixels;
        return tintedBitmap;
    }

    private static bool ShouldRecolorAccentPixel(float hue, float saturation, float value)
    {
        if (saturation < RecolorMinSaturation || value < RecolorMinValue)
            return false;

        var normalizedHue = NormalizeHue(hue);
        return normalizedHue >= RecolorHueMin && normalizedHue <= RecolorHueMax;
    }

    private static SKColor CreateTintedVariant(
        SKColor source,
        float sourceHue,
        float sourceSaturation,
        float sourceValue,
        float tintHue,
        float tintSaturation,
        float tintValue)
    {
        var hue = tintSaturation < 0.08f ? sourceHue : tintHue;
        var saturation = tintSaturation < 0.08f
            ? Math.Clamp(sourceSaturation * 0.08f, 0f, 1f)
            : Math.Clamp((sourceSaturation * 0.3f) + (tintSaturation * 0.8f), 0f, 1f);
        var value = Math.Clamp(sourceValue * (0.58f + (tintValue * 0.42f)), 0f, 1f);
        return HsvToColor(hue, saturation, value, source.Alpha);
    }

    private static float CalculateBlendStrength(float saturation)
    {
        var normalized = (saturation - RecolorMinSaturation) / (1f - RecolorMinSaturation);
        normalized = Math.Clamp(normalized, 0f, 1f);
        return Math.Clamp(0.42f + (normalized * 0.46f), 0.35f, 0.9f);
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

    private static void RgbToHsv(SKColor color, out float hue, out float saturation, out float value)
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
            if (Math.Abs(max - r) < 0.0001f)
                hue = 60f * (((g - b) / delta) % 6f);
            else if (Math.Abs(max - g) < 0.0001f)
                hue = 60f * (((b - r) / delta) + 2f);
            else
                hue = 60f * (((r - g) / delta) + 4f);
        }

        hue = NormalizeHue(hue);
        saturation = max <= 0f ? 0f : delta / max;
        value = max;
    }

    private static SKColor HsvToColor(float hue, float saturation, float value, byte alpha)
    {
        var h = NormalizeHue(hue);
        var c = value * saturation;
        var x = c * (1f - Math.Abs(((h / 60f) % 2f) - 1f));
        var m = value - c;

        float rPrime;
        float gPrime;
        float bPrime;
        if (h < 60f)
        {
            rPrime = c;
            gPrime = x;
            bPrime = 0f;
        }
        else if (h < 120f)
        {
            rPrime = x;
            gPrime = c;
            bPrime = 0f;
        }
        else if (h < 180f)
        {
            rPrime = 0f;
            gPrime = c;
            bPrime = x;
        }
        else if (h < 240f)
        {
            rPrime = 0f;
            gPrime = x;
            bPrime = c;
        }
        else if (h < 300f)
        {
            rPrime = x;
            gPrime = 0f;
            bPrime = c;
        }
        else
        {
            rPrime = c;
            gPrime = 0f;
            bPrime = x;
        }

        var r = (byte)Math.Round((rPrime + m) * 255f);
        var g = (byte)Math.Round((gPrime + m) * 255f);
        var b = (byte)Math.Round((bPrime + m) * 255f);
        return new SKColor(r, g, b, alpha);
    }

    private static float NormalizeHue(float hue)
    {
        var normalized = hue % 360f;
        if (normalized < 0f)
            normalized += 360f;
        return normalized;
    }

    private static IEnumerable<string> BuildIconPathCandidates(string iconFile)
    {
        var normalized = iconFile.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        yield return normalized;

        var windowsNormalized = normalized.Replace('/', '\\');
        if (!string.Equals(windowsNormalized, normalized, StringComparison.Ordinal))
            yield return windowsNormalized;

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            yield return fileName;
            yield return $"toolicons/{fileName}";
            yield return $"toolicons\\{fileName}";
        }
    }

    private static async Task<byte[]?> GetTemplateBytesAsync(string iconFile, CancellationToken token)
    {
        if (TemplateBytesCache.TryGetValue(iconFile, out var bytes))
            return bytes;

        bytes = await LoadTemplateBytesAsync(iconFile, token).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
            return null;

        TemplateBytesCache.TryAdd(iconFile, bytes);
        return bytes;
    }

    private static async Task<byte[]?> LoadTemplateBytesAsync(string iconFile, CancellationToken token)
    {
        foreach (var candidate in BuildIconPathCandidates(iconFile).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync(candidate).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, token).ConfigureAwait(false);
                return memory.ToArray();
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        return null;
    }
}
