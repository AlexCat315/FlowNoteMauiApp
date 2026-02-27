using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
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
    // 颜色范围配置（蓝紫色调）
    private const float RecolorMinSaturation = 0.18f;
    private const float RecolorMinValue = 0.08f;
    private const float RecolorHueMin = 140f;
    private const float RecolorHueMax = 260f;

    // 并行处理阈值
    private const int ParallelProcessingThreshold = 10000;

    // 使用 ValueTuple 作为复合键，避免字符串拼接开销
    private static readonly ConcurrentDictionary<(string File, IconTintMode Mode, uint Color), ImageSource> ImageCache = new();
    private static readonly ConcurrentDictionary<string, byte[]> TemplateBytesCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(string File, IconTintMode Mode, uint Color), byte[]> ProcessedBytesCache = new();

    // 预计算的 HSV 转换查找表（可选优化）
    private static readonly float[] HueLookup = Enumerable.Range(0, 256).Select(i => i / 255f * 360f).ToArray();

    public static void ClearCache()
    {
        ImageCache.Clear();
        TemplateBytesCache.Clear();
        ProcessedBytesCache.Clear();
    }

    public static (int ImageCacheCount, int TemplateCacheCount, int ProcessedBytesCount) GetCacheStats()
        => (ImageCache.Count, TemplateBytesCache.Count, ProcessedBytesCache.Count);

    public static async Task<ImageSource> CreateTintedImageSourceFromPackageAsync(
        string iconFile,
        SKColor tintColor,
        IconTintMode tintMode,
        CancellationToken token = default)
    {
        var fallbackFile = Path.GetFileName(iconFile);
        var cacheKey = BuildCacheKey(iconFile, tintMode, tintColor);

        if (ImageCache.TryGetValue(cacheKey, out var cachedSource))
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

            // 检查是否已有处理后的字节缓存
            if (ProcessedBytesCache.TryGetValue(cacheKey, out var processedBytes))
            {
                var cachedImageSource = CreateImageSourceFromBytes(processedBytes);
                ImageCache[cacheKey] = cachedImageSource;
                return cachedImageSource;
            }

            using var tintedBitmap = tintMode == IconTintMode.Monochrome
                ? RenderMonochrome(baseBitmap, tintColor)
                : RenderAccentPreserveShading(baseBitmap, tintColor, token);

            using var outputImage = SKImage.FromBitmap(tintedBitmap);
            using var outputData = outputImage.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = outputData.ToArray();

            // 缓存处理后的字节数组，确保流可以安全读取
            ProcessedBytesCache[cacheKey] = bytes;
            var source = CreateImageSourceFromBytes(bytes);
            ImageCache[cacheKey] = source;

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

    public static async Task<ImageSource> CreateProcedural3DToolIconAsync(
        ToolIconKind toolKind,
        SKColor accentColor,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var renderSize = ResolveProceduralIconRenderSize();
        var cacheKey = ($"proc3d:{toolKind}:{renderSize}", IconTintMode.Monochrome, PackColor(accentColor));

        if (ImageCache.TryGetValue(cacheKey, out var cachedSource))
            return cachedSource;

        if (ProcessedBytesCache.TryGetValue(cacheKey, out var cachedBytes))
        {
            var cachedImageSource = CreateImageSourceFromBytes(cachedBytes);
            ImageCache[cacheKey] = cachedImageSource;
            return cachedImageSource;
        }

        var bytes = await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            using var bitmap = ProceduralToolIconRenderer.Render(toolKind, accentColor, renderSize);
            using var outputImage = SKImage.FromBitmap(bitmap);
            using var outputData = outputImage.Encode(SKEncodedImageFormat.Png, 100);
            return outputData?.ToArray() ?? Array.Empty<byte>();
        }, token).ConfigureAwait(false);

        if (bytes.Length == 0)
            return ImageSource.FromFile("icon_brush.png");

        ProcessedBytesCache[cacheKey] = bytes;
        var source = CreateImageSourceFromBytes(bytes);
        ImageCache[cacheKey] = source;

        return source;
    }

    public static async Task PreloadCommonIconsAsync(
        IEnumerable<string> iconFiles,
        SKColor tintColor,
        IconTintMode mode,
        CancellationToken token = default)
    {
        var tasks = iconFiles.Select(f => CreateTintedImageSourceFromPackageAsync(f, tintColor, mode, token));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (string, IconTintMode, uint) BuildCacheKey(string file, IconTintMode mode, SKColor color)
        => (file, mode, PackColor(color));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint PackColor(SKColor color, byte extra = 0)
        => (uint)((color.Alpha << 24) | (color.Red << 16) | (color.Green << 8) | color.Blue | (extra << 0));

    private static ImageSource CreateImageSourceFromBytes(byte[] bytes)
        => ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));

    private static int ResolveProceduralIconRenderSize()
    {
        try
        {
            var density = Math.Max(1d, DeviceDisplay.Current.MainDisplayInfo.Density);
            var target = (int)Math.Ceiling(96d * density);
            return Math.Clamp(target, 192, 384);
        }
        catch
        {
            return 256;
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
        var width = baseBitmap.Width;
        var height = baseBitmap.Height;
        var pixelCount = width * height;
        var sourcePixels = baseBitmap.Pixels;
        var tintedPixels = new SKColor[pixelCount];

        // 预计算 tint 的 HSV
        RgbToHsv(tintColor, out var tintHue, out var tintSaturation, out var tintValue);

        // 小图像使用单线程，大图像使用并行处理
        if (pixelCount < ParallelProcessingThreshold)
        {
            ProcessPixelRange(sourcePixels, tintedPixels, 0, pixelCount,
                tintHue, tintSaturation, tintValue, token);
        }
        else
        {
            var options = new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            // 按行分区，避免伪共享
            var partitionSize = Math.Max(1, height / (Environment.ProcessorCount * 2));

            Parallel.For(0, (height + partitionSize - 1) / partitionSize, options, partitionIndex =>
            {
                var startRow = partitionIndex * partitionSize;
                var endRow = Math.Min(startRow + partitionSize, height);
                var startIndex = startRow * width;
                var endIndex = endRow * width;

                ProcessPixelRange(sourcePixels, tintedPixels, startIndex, endIndex,
                    tintHue, tintSaturation, tintValue, token);
            });
        }

        var tintedBitmap = new SKBitmap(width, height, baseBitmap.ColorType, baseBitmap.AlphaType);
        tintedBitmap.Pixels = tintedPixels;
        return tintedBitmap;
    }

    private static void ProcessPixelRange(
        SKColor[] sourcePixels,
        SKColor[] tintedPixels,
        int startIndex,
        int endIndex,
        float tintHue,
        float tintSaturation,
        float tintValue,
        CancellationToken token)
    {
        // 每处理 256 个像素检查一次取消标记
        const int cancellationCheckInterval = 256;

        for (var i = startIndex; i < endIndex; i++)
        {
            if (((i - startIndex) & (cancellationCheckInterval - 1)) == 0)
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldRecolorAccentPixel(float hue, float saturation, float value)
    {
        if (saturation < RecolorMinSaturation || value < RecolorMinValue)
            return false;

        var normalizedHue = NormalizeHue(hue);
        return normalizedHue >= RecolorHueMin && normalizedHue <= RecolorHueMax;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CalculateBlendStrength(float saturation)
    {
        var normalized = (saturation - RecolorMinSaturation) / (1f - RecolorMinSaturation);
        normalized = Math.Clamp(normalized, 0f, 1f);
        return Math.Clamp(0.42f + (normalized * 0.46f), 0.35f, 0.9f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKColor LerpColor(SKColor from, SKColor to, float amount)
    {
        var clamped = Math.Clamp(amount, 0f, 1f);
        // 使用位运算优化插值
        var r = (byte)((from.Red * (1f - clamped)) + (to.Red * clamped));
        var g = (byte)((from.Green * (1f - clamped)) + (to.Green * clamped));
        var b = (byte)((from.Blue * (1f - clamped)) + (to.Blue * clamped));
        var a = (byte)((from.Alpha * (1f - clamped)) + (to.Alpha * clamped));
        return new SKColor(r, g, b, a);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RgbToHsv(SKColor color, out float hue, out float saturation, out float value)
    {
        var r = color.Red * 0.00392156863f; // 1/255
        var g = color.Green * 0.00392156863f;
        var b = color.Blue * 0.00392156863f;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        hue = 0f;
        if (delta > 0.0001f)
        {
            if (Math.Abs(max - r) < 0.0001f)
                hue = ((g - b) / delta) % 6f;
            else if (Math.Abs(max - g) < 0.0001f)
                hue = ((b - r) / delta) + 2f;
            else
                hue = ((r - g) / delta) + 4f;

            hue *= 60f;
        }

        hue = NormalizeHue(hue);
        saturation = max <= 0f ? 0f : delta / max;
        value = max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKColor HsvToColor(float hue, float saturation, float value, byte alpha)
    {
        var h = NormalizeHue(hue);
        var c = value * saturation;
        var x = c * (1f - Math.Abs(((h / 60f) % 2f) - 1f));
        var m = value - c;

        float rPrime, gPrime, bPrime;

        // 使用 switch 表达式优化分支
        var sector = (int)(h / 60f) % 6;
        (rPrime, gPrime, bPrime) = sector switch
        {
            0 => (c, x, 0f),
            1 => (x, c, 0f),
            2 => (0f, c, x),
            3 => (0f, x, c),
            4 => (x, 0f, c),
            _ => (c, 0f, x)
        };

        var r = (byte)((rPrime + m) * 255f);
        var g = (byte)((gPrime + m) * 255f);
        var b = (byte)((bPrime + m) * 255f);
        return new SKColor(r, g, b, alpha);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float NormalizeHue(float hue)
    {
        var normalized = hue % 360f;
        return normalized < 0f ? normalized + 360f : normalized;
    }

    private static IEnumerable<string> BuildIconPathCandidates(string iconFile)
    {
        var normalized = iconFile.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);

        yield return normalized;

        var windowsNormalized = normalized.Replace('/', '\\');
        if (!string.Equals(windowsNormalized, normalized, StringComparison.Ordinal))
            yield return windowsNormalized;

        if (string.IsNullOrWhiteSpace(fileName))
            yield break;

        yield return fileName;
        yield return $"toolicons/{fileName}";
        yield return $"toolicons\\{fileName}";
    }

    private static async Task<byte[]?> GetTemplateBytesAsync(string iconFile, CancellationToken token)
    {
        if (TemplateBytesCache.TryGetValue(iconFile, out var bytes))
            return bytes.Length == 0 ? null : bytes;

        bytes = await LoadTemplateBytesAsync(iconFile, token).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
        {
            TemplateBytesCache.TryAdd(iconFile, Array.Empty<byte>());
            return null;
        }

        TemplateBytesCache.TryAdd(iconFile, bytes);
        return bytes;
    }

    private static async Task<byte[]?> LoadTemplateBytesAsync(string iconFile, CancellationToken token)
    {
        var candidates = BuildIconPathCandidates(iconFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var candidate in candidates)
        {
            try
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync(candidate).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                // 预分配内存，避免扩容
                var buffer = new byte[stream.Length];
                var totalRead = 0;

                while (totalRead < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(totalRead), token).ConfigureAwait(false);
                    if (read == 0) break;
                    totalRead += read;
                }

                // 如果实际读取长度与预期不符，调整数组大小
                if (totalRead < buffer.Length)
                {
                    Array.Resize(ref buffer, totalRead);
                }

                return buffer;
            }
            catch (FileNotFoundException)
            {
                Debug.WriteLine($"[IconRender] Template not found: {candidate}");
            }
            catch (DirectoryNotFoundException)
            {
                Debug.WriteLine($"[IconRender] Directory not found for: {candidate}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IconRender] Error loading template '{candidate}': {ex.Message}");
            }
        }

        return null;
    }
}
