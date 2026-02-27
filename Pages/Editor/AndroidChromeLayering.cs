using Microsoft.Maui.ApplicationModel;

#if ANDROID
using Android.Views;
using Android.Widget;
using AView = Android.Views.View;
#endif

namespace FlowNoteMauiApp;

public partial class MainPage
{
#if ANDROID
    private bool _androidChromeLayeringWired;
    private bool _androidLayeringReapplyScheduled;
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
        WireAndroidLayeringHooks(DrawerOverlayView);
        WireAndroidLayeringHooks(SettingsOverlayView);
        WireAndroidLayeringHooks(StatusToastView);
        WireAndroidLayeringHooks(ImportProgressOverlay);

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
            SetNativeLayer(EditorHost, 0f, 0f, bringToFront: false);
            SetNativeLayer(EditorChromeView, 200f, 200f, bringToFront: true);
            SetNativeLayer(TopBarPanel, 300f, 300f, bringToFront: true);
            SetNativeLayer(PinnedInkToolsOverlay, 900f, 900f, bringToFront: true);
            SetNativeLayer(InkToolsStripContainer, 940f, 940f, bringToFront: true);
            SetNativeLayer(InkToolsHost, 960f, 960f, bringToFront: true);
            SetNativeLayer(DrawerOverlayView, 2000f, 2000f, bringToFront: true);
            SetNativeLayer(SettingsOverlayView, 2200f, 2200f, bringToFront: true);
            SetNativeLayer(StatusToastView, 2400f, 2400f, bringToFront: true);
            SetNativeLayer(ImportProgressOverlay, 2600f, 2600f, bringToFront: true);

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
                DisableSurfaceViewTopMost(PdfViewer);
                ScheduleAndroidLayeringReapply();
            }
        });
    }

    private void ScheduleAndroidLayeringReapply()
    {
        if (_androidLayeringReapplyScheduled)
            return;

        _androidLayeringReapplyScheduled = true;
        var delays = new[] { 20, 80, 180, 360, 720 };
        foreach (var delay in delays)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(delay), () =>
            {
                if (!IsEditorInitialized)
                    return;

                try
                {
                    DisableSurfaceViewTopMost(PdfViewer);
                    SetNativeLayer(PdfViewer, 0f, 0f, bringToFront: false);
                    SetNativeLayer(EditorChromeView, 200f, 200f, bringToFront: true);
                    SetNativeLayer(SettingsOverlayView, 2200f, 2200f, bringToFront: true);
                    SetNativeLayer(ImportProgressOverlay, 2600f, 2600f, bringToFront: true);
                }
                finally
                {
                    if (delay == delays[^1])
                    {
                        _androidLayeringReapplyScheduled = false;
                    }
                }
            });
        }
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

    private static void DisableSurfaceViewTopMost(VisualElement element)
    {
        if (element.Handler?.PlatformView is not AView view)
            return;

        WalkNativeTree(view, native =>
        {
            if (native is SurfaceView surfaceView)
            {
                surfaceView.SetZOrderOnTop(false);
                surfaceView.SetZOrderMediaOverlay(false);
            }
        });
    }

    private static void WalkNativeTree(AView root, Action<AView> visitor)
    {
        visitor(root);
        if (root is not ViewGroup group)
            return;

        for (var i = 0; i < group.ChildCount; i++)
        {
            var child = group.GetChildAt(i);
            if (child is not null)
            {
                WalkNativeTree(child, visitor);
            }
        }
    }
#endif
}
