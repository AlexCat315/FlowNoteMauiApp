using Microsoft.Maui.ApplicationModel;

#if ANDROID
using Android.Views;
using AView = Android.Views.View;
#endif

namespace FlowNoteMauiApp;

public partial class MainPage
{
#if ANDROID
    private bool _androidChromeLayeringWired;
#endif

    private void EnsureAndroidChromeLayering()
    {
#if ANDROID
        if (_androidChromeLayeringWired)
            return;

        _androidChromeLayeringWired = true;

        WireAndroidLayeringHooks(EditorChromeView);
        WireAndroidLayeringHooks(TopBarPanel);
        WireAndroidLayeringHooks(TopToolsScrollView);
        WireAndroidLayeringHooks(PinnedInkToolsOverlay);
        WireAndroidLayeringHooks(InkToolsStripContainer);
        WireAndroidLayeringHooks(InkToolsHost);
        WireAndroidLayeringHooks(PenModeButton);
        WireAndroidLayeringHooks(HighlighterButton);
        WireAndroidLayeringHooks(PencilButton);
        WireAndroidLayeringHooks(MarkerButton);
        WireAndroidLayeringHooks(EraserButton);
        WireAndroidLayeringHooks(ClearButton2);
        WireAndroidLayeringHooks(TopInlineLayerButton);

        ApplyAndroidChromeLayering();
#endif
    }

    private void RefreshAndroidChromeLayering()
    {
#if ANDROID
        ApplyAndroidChromeLayering();
#endif
    }

#if ANDROID
    private void WireAndroidLayeringHooks(VisualElement element)
    {
        element.HandlerChanged += OnAndroidLayeringElementChanged;
        element.SizeChanged += OnAndroidLayeringElementChanged;
    }

    private void OnAndroidLayeringElementChanged(object? sender, EventArgs e)
    {
        ApplyAndroidChromeLayering();
    }

    private void ApplyAndroidChromeLayering()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SetNativeLayer(EditorChromeView, 200f, 200f, bringToFront: true);
            SetNativeLayer(TopBarPanel, 300f, 300f, bringToFront: true);
            SetNativeLayer(PinnedInkToolsOverlay, 900f, 900f, bringToFront: true);
            SetNativeLayer(InkToolsStripContainer, 940f, 940f, bringToFront: true);
            SetNativeLayer(InkToolsHost, 960f, 960f, bringToFront: true);

            SetNativeLayer(PenModeButton, 1000f, 1000f, bringToFront: true);
            SetNativeLayer(HighlighterButton, 1000f, 1000f, bringToFront: true);
            SetNativeLayer(PencilButton, 1000f, 1000f, bringToFront: true);
            SetNativeLayer(MarkerButton, 1000f, 1000f, bringToFront: true);
            SetNativeLayer(EraserButton, 1000f, 1000f, bringToFront: true);
            SetNativeLayer(ClearButton2, 1000f, 1000f, bringToFront: true);
            SetNativeLayer(TopInlineLayerButton, 1000f, 1000f, bringToFront: true);

            DisableClipChain(EditorChromeView);
            DisableClipChain(TopBarPanel);
            DisableClipChain(PinnedInkToolsOverlay);
            DisableClipChain(InkToolsStripContainer);
            DisableClipChain(InkToolsHost);
            DisableClipChain(PenModeButton);

            if (IsEditorInitialized)
            {
                SetNativeLayer(PdfViewer, 0f, 0f, bringToFront: false);
                SetNativeLayer(DrawingCanvas, 8f, 8f, bringToFront: false);
                DisableClipChain(DrawingCanvas);
            }
        });
    }

    private static void SetNativeLayer(VisualElement element, float elevation, float translationZ, bool bringToFront)
    {
        if (element.Handler?.PlatformView is not AView view)
            return;

        view.Elevation = elevation;
        view.TranslationZ = translationZ;
        if (bringToFront)
        {
            view.BringToFront();
        }
    }

    private static void DisableClipChain(VisualElement element)
    {
        if (element.Handler?.PlatformView is not AView view)
            return;

        if (view is ViewGroup nativeGroup)
        {
            nativeGroup.SetClipChildren(false);
            nativeGroup.SetClipToPadding(false);
        }

        var parent = view.Parent;
        var depth = 0;
        while (parent is ViewGroup group && depth < 18)
        {
            group.SetClipChildren(false);
            group.SetClipToPadding(false);
            parent = group.Parent;
            depth++;
        }
    }
#endif
}
