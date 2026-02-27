using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using System.Runtime.CompilerServices;

namespace FlowNoteMauiApp.Helpers;

public static class ImageButtonIconExtensions
{
    private static readonly ConditionalWeakTable<ImageButton, IconRenderState> RenderStateMap = new();

    public static async Task SetIconAsync(
        this ImageButton button,
        string iconFile,
        Color tintColor,
        IconTintMode tintMode = IconTintMode.AccentPreserveShading,
        double? iconSize = null,
        CancellationToken token = default)
    {
        if (button is null)
            throw new ArgumentNullException(nameof(button));

        if (iconSize is > 0d)
            button.SetIconDrawSize(iconSize.Value);

        var skTintColor = ToSkColor(tintColor);
        var renderKey = BuildRenderKey(iconFile, tintMode, skTintColor);
        var state = RenderStateMap.GetOrCreateValue(button);
        if (string.Equals(state.LastRenderKey, renderKey, StringComparison.Ordinal))
            return;

        var source = await IconRenderHelper
            .CreateTintedImageSourceFromPackageAsync(iconFile, skTintColor, tintMode, token)
            .ConfigureAwait(false);

        if (token.IsCancellationRequested)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!token.IsCancellationRequested)
            {
                button.Source = source;
                state.LastRenderKey = renderKey;
            }
        });
    }

    public static void SetIconDrawSize(this ImageButton button, double iconWidth, double? iconHeight = null)
    {
        if (button is null)
            throw new ArgumentNullException(nameof(button));
        if (iconWidth <= 0)
            return;

        var targetHeight = iconHeight.GetValueOrDefault(iconWidth);

        bool ApplyPadding()
        {
            var buttonWidth = button.Width > 0 ? button.Width : button.WidthRequest;
            var buttonHeight = button.Height > 0 ? button.Height : button.HeightRequest;
            if (buttonWidth <= 0 || buttonHeight <= 0)
                return false;

            var horizontal = Math.Max(0d, (buttonWidth - iconWidth) / 2d);
            var vertical = Math.Max(0d, (buttonHeight - targetHeight) / 2d);
            button.Padding = new Thickness(horizontal, vertical, horizontal, vertical);
            return true;
        }

        if (ApplyPadding())
            return;

        void HandleSizeChanged(object? sender, EventArgs e)
        {
            if (ApplyPadding())
            {
                button.SizeChanged -= HandleSizeChanged;
            }
        }

        button.SizeChanged += HandleSizeChanged;
    }

    private static SKColor ToSkColor(Color color)
    {
        var red = (byte)Math.Clamp((int)Math.Round(color.Red * 255d), 0, 255);
        var green = (byte)Math.Clamp((int)Math.Round(color.Green * 255d), 0, 255);
        var blue = (byte)Math.Clamp((int)Math.Round(color.Blue * 255d), 0, 255);
        var alpha = (byte)Math.Clamp((int)Math.Round(color.Alpha * 255d), 0, 255);
        return new SKColor(red, green, blue, alpha);
    }

    private static string BuildRenderKey(string iconFile, IconTintMode tintMode, SKColor color)
    {
        return $"{iconFile}:{tintMode}:{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    }

    private sealed class IconRenderState
    {
        public string LastRenderKey { get; set; } = string.Empty;
    }
}
