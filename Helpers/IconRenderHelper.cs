using System.Collections.Concurrent;
using System.Diagnostics;
using FlowNoteMauiApp.Core.Rendering.ToolIcons;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
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
        ToolIconKind toolKind,
        SKColor accentColor,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var renderSize = ResolveProceduralIconRenderSize();
        var cacheKey = $"proc3d:{toolKind}:{renderSize}:{accentColor.Alpha:X2}{accentColor.Red:X2}{accentColor.Green:X2}{accentColor.Blue:X2}";
        if (Cache.TryGetValue(cacheKey, out var cachedSource))
            return Task.FromResult(cachedSource);

        using var bitmap = ProceduralToolIconRenderer.Render(toolKind, accentColor, renderSize);
        using var outputImage = SKImage.FromBitmap(bitmap);
        using var outputData = outputImage.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = outputData.ToArray();
        var source = ImageSource.FromStream(() => new MemoryStream(bytes));
        Cache[cacheKey] = source;
        return Task.FromResult(source);
    }

    private static int ResolveProceduralIconRenderSize()
    {
        try
        {
            var density = Math.Max(1d, DeviceDisplay.Current.MainDisplayInfo.Density);
            var target = (int)Math.Ceiling(256d * density);
            return Math.Clamp(target, 512, 1024);
        }
        catch
        {
            return 768;
        }
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
