using SkiaSharp;
using Flow.PDFView;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Controls;
using FlowNoteMauiApp.Helpers;
using FlowNoteMauiApp.Models;
using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Maui.Devices;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private void PositionDrawingToolbarPanelUnderTool(InkToolKind tool)
    {
        var anchorButton = tool switch
        {
            InkToolKind.Fountain => HighlighterButton,
            InkToolKind.Pencil => PencilButton,
            InkToolKind.Marker => MarkerButton,
            InkToolKind.Eraser => EraserButton,
            _ => PenModeButton
        };

        PositionDrawingToolbarPanel(anchorButton);
    }

    private void OnEditorChromeLayoutChanged(object? sender, EventArgs e)
    {
        PositionPinnedInkToolsOverlay();

        if (InputModePanel.IsVisible)
        {
            PositionInputModePanelUnderTopModeButton();
        }

        if (DrawingToolbarPanel.IsVisible)
        {
            ApplyDrawingToolbarResponsiveLayout();
            PositionDrawingToolbarPanelUnderTool(_activeInkTool);
        }

        if (ThumbnailPanel.IsVisible)
        {
            PositionThumbnailPanelUnderTopButton();
        }

        if (LayerPanel.IsVisible)
        {
            PositionLayerPanelUnderLayerButton();
        }
    }

    private void OnTopBarScrolled(object? sender, ScrolledEventArgs e)
    {
        if (TopBarPanel.IsVisible)
        {
            PositionPinnedInkToolsOverlay();
        }

        if (!InputModePanel.IsVisible
            && !DrawingToolbarPanel.IsVisible
            && !ThumbnailPanel.IsVisible
            && !LayerPanel.IsVisible)
        {
            return;
        }

        ScheduleFloatingPanelReposition();
    }

    private void PositionPinnedInkToolsOverlay()
    {
        if (!EditorChromeView.IsVisible || !TopBarPanel.IsVisible)
        {
            PinnedInkToolsOverlay.IsVisible = false;
            return;
        }

        PinnedInkToolsOverlay.IsVisible = true;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            void ApplyPosition(int attempt)
            {
                if (!EditorChromeView.IsVisible || !TopBarPanel.IsVisible)
                {
                    PinnedInkToolsOverlay.IsVisible = false;
                    return;
                }

                var hostX = GetVisualOffsetX(TopToolsScrollView, EditorChromeView);
                var hostY = GetVisualOffsetY(TopToolsScrollView, EditorChromeView);
                var hostHeight = TopToolsScrollView.Height > 1 ? TopToolsScrollView.Height : TopToolsScrollView.HeightRequest;
                var overlayHeight = PinnedInkToolsOverlay.Height > 1 ? PinnedInkToolsOverlay.Height : 32d;

                var hasValidLayout = hostHeight > 1 && EditorChromeView.Width > 1;
                if (!hasValidLayout && attempt < 12)
                {
                    PinnedInkToolsOverlay.Dispatcher.DispatchDelayed(
                        TimeSpan.FromMilliseconds(16),
                        () => ApplyPosition(attempt + 1));
                    return;
                }

                var overlayX = Math.Max(0d, hostX);
                var overlayY = hostY + Math.Max(0d, (hostHeight - overlayHeight) / 2d);
                PinnedInkToolsOverlay.Margin = new Thickness(
                    Math.Round(overlayX),
                    Math.Round(overlayY),
                    0,
                    0);
                PinnedInkToolsOverlay.TranslationX = 0;
                PinnedInkToolsOverlay.TranslationY = 0;

                var overlayWidth = PinnedInkToolsOverlay.Width > 1
                    ? PinnedInkToolsOverlay.Width
                    : PinnedInkToolsOverlay.WidthRequest;
                if (overlayWidth > 1)
                {
                    var targetSpacerWidth = Math.Ceiling(overlayWidth + 10d);
                    if (Math.Abs(TopToolsLeadingSpacer.WidthRequest - targetSpacerWidth) > 0.5d)
                    {
                        TopToolsLeadingSpacer.WidthRequest = targetSpacerWidth;
                    }
                }
            }

            ApplyPosition(0);
        });
    }

    private void PositionThumbnailPanelUnderTopButton()
    {
        PositionFloatingPanelUnderTopButton(ThumbnailPanel, TopThumbnailButton, 268d);
    }

    private void PositionLayerPanelUnderLayerButton()
    {
        PositionFloatingPanelUnderTopButton(LayerPanel, TopInlineLayerButton, 236d);
    }

    private void PositionFloatingPanelUnderTopButton(
        Border panel,
        VisualElement anchorButton,
        double fallbackWidth)
    {
        if (!panel.IsVisible)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            void ApplyPosition(int attempt)
            {
                if (!panel.IsVisible)
                    return;

                var panelWidth = panel.Width > 1
                    ? panel.Width
                    : (panel.WidthRequest > 1 ? panel.WidthRequest : fallbackWidth);
                var panelHeight = panel.Height > 1 ? panel.Height : 0d;

                var anchorX = GetVisualOffsetX(anchorButton, TopBarPanel) + TopBarPanel.X;
                var anchorY = GetVisualOffsetY(anchorButton, TopBarPanel) + TopBarPanel.Y;
                var anchorWidth = anchorButton.Width > 1 ? anchorButton.Width : anchorButton.WidthRequest;
                var anchorHeight = anchorButton.Height > 1 ? anchorButton.Height : anchorButton.HeightRequest;

                var hasValidLayout = anchorWidth > 1
                    && anchorHeight > 1
                    && TopBarPanel.Height > 1
                    && EditorChromeView.Width > 1;
                if (!hasValidLayout)
                {
                    if (attempt < 14)
                    {
                        panel.Dispatcher.DispatchDelayed(
                            TimeSpan.FromMilliseconds(16),
                            () => ApplyPosition(attempt + 1));
                    }
                    return;
                }

                var targetX = anchorX + (anchorWidth / 2d) - (panelWidth / 2d);
                var maxX = Math.Max(8d, EditorChromeView.Width - panelWidth - 8d);
                targetX = Math.Clamp(targetX, 8d, maxX);

                var targetY = anchorY + anchorHeight + 4d;
                var minY = Math.Max(TopBarPanel.Height + 2d, 6d);
                if (panelHeight > 1 && EditorChromeView.Height > 1)
                {
                    var maxY = Math.Max(minY, EditorChromeView.Height - panelHeight - 8d);
                    targetY = Math.Clamp(targetY, minY, maxY);
                }
                else
                {
                    targetY = Math.Max(targetY, minY);
                }

                panel.TranslationX = 0;
                panel.TranslationY = 0;
                panel.Margin = new Thickness(targetX, targetY, 0, 0);
            }

            ApplyPosition(0);
        });
    }

    private void PositionDrawingToolbarPanel(VisualElement anchorButton)
    {
        if (!DrawingToolbarPanel.IsVisible)
            return;

        ApplyDrawingToolbarResponsiveLayout();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            void ApplyPosition(int attempt)
            {
                if (!DrawingToolbarPanel.IsVisible)
                    return;

                var panelWidth = DrawingToolbarPanel.Width > 1
                    ? DrawingToolbarPanel.Width
                    : (DrawingToolbarPanel.WidthRequest > 1 ? DrawingToolbarPanel.WidthRequest : 236d);
                var panelHeight = DrawingToolbarPanel.Height > 1 ? DrawingToolbarPanel.Height : 0d;

                var anchorX = GetVisualOffsetX(anchorButton, TopBarPanel) + TopBarPanel.X;
                var anchorY = GetVisualOffsetY(anchorButton, TopBarPanel) + TopBarPanel.Y;
                var anchorWidth = anchorButton.Width > 1 ? anchorButton.Width : anchorButton.WidthRequest;
                var anchorHeight = anchorButton.Height > 1 ? anchorButton.Height : anchorButton.HeightRequest;

                var hasValidLayout = anchorWidth > 1
                    && anchorHeight > 1
                    && TopBarPanel.Height > 1
                    && EditorChromeView.Width > 1
                    && anchorX > 1
                    && anchorY > 1;
                if (!hasValidLayout && attempt < 10)
                {
                    DrawingToolbarPanel.Dispatcher.DispatchDelayed(
                        TimeSpan.FromMilliseconds(16),
                        () => ApplyPosition(attempt + 1));
                    return;
                }

                var targetX = anchorX + (anchorWidth / 2d) - (panelWidth / 2d);
                var maxX = Math.Max(12d, EditorChromeView.Width - panelWidth - 12d);
                targetX = Math.Clamp(targetX, 12d, maxX);

                var targetY = anchorY + anchorHeight + 10d;
                var minY = Math.Max(TopBarPanel.Height + 6d, 12d);
                if (panelHeight > 1 && EditorChromeView.Height > 1)
                {
                    var maxY = Math.Max(minY, EditorChromeView.Height - panelHeight - 12d);
                    targetY = Math.Clamp(targetY, minY, maxY);
                }
                else
                {
                    targetY = Math.Max(targetY, minY);
                }

                DrawingToolbarPanel.TranslationX = 0;
                DrawingToolbarPanel.TranslationY = 0;
                DrawingToolbarPanel.Margin = new Thickness(targetX, targetY, 0, 0);
            }

            ApplyPosition(0);
        });
    }

    private void ApplyDrawingToolbarResponsiveLayout()
    {
        if (EditorChromeView.Height <= 1)
            return;

        var viewportHeight = EditorChromeView.Height;
        var topReserved = Math.Max(TopBarPanel.Height + 14d, 56d);
        var preferredPanelHeight = viewportHeight - topReserved - 10d;
        var minPanelHeight = Math.Min(180d, Math.Max(128d, viewportHeight - 16d));
        var maxPanelHeight = Math.Max(minPanelHeight, viewportHeight - 8d);
        var availablePanelHeight = Math.Min(maxPanelHeight, Math.Max(minPanelHeight, preferredPanelHeight));
        if (Math.Abs(DrawingToolbarPanel.MaximumHeightRequest - availablePanelHeight) > 0.5d)
        {
            DrawingToolbarPanel.MaximumHeightRequest = availablePanelHeight;
        }

        var preferredScrollHeight = availablePanelHeight - 56d;
        var maxScrollHeight = Math.Max(72d, availablePanelHeight - 24d);
        var scrollMax = Math.Min(maxScrollHeight, Math.Max(72d, preferredScrollHeight));
        if (Math.Abs(DrawingToolbarContentScroll.MaximumHeightRequest - scrollMax) > 0.5d)
        {
            DrawingToolbarContentScroll.MaximumHeightRequest = scrollMax;
        }
    }

    private static double GetVisualOffsetX(VisualElement element, VisualElement ancestor)
    {
        var x = 0d;
        Element? current = element;
        while (current is VisualElement visual && current != ancestor)
        {
            x += visual.X + visual.TranslationX;
            if (visual is ScrollView scrollView)
            {
                x -= scrollView.ScrollX;
            }
            current = visual.Parent;
        }

        return x;
    }

    private static double GetVisualOffsetY(VisualElement element, VisualElement ancestor)
    {
        var y = 0d;
        Element? current = element;
        while (current is VisualElement visual && current != ancestor)
        {
            y += visual.Y + visual.TranslationY;
            if (visual is ScrollView scrollView)
            {
                y -= scrollView.ScrollY;
            }
            current = visual.Parent;
        }

        return y;
    }

    private void OnLayerToggleClicked(object? sender, EventArgs e)
    {
        SetInputModePanelVisible(false);
        if (ThumbnailPanel.IsVisible)
        {
            AnimatePopupOut(ThumbnailPanel, () => ThumbnailPanel.IsVisible = false);
        }

        if (LayerPanel.IsVisible)
        {
            AnimatePopupOut(LayerPanel, () => LayerPanel.IsVisible = false);
            return;
        }

        LayerPanel.IsVisible = true;
        PositionLayerPanelUnderLayerButton();
        AnimatePopupIn(LayerPanel);
        LayerPanel.Dispatcher.DispatchDelayed(
            TimeSpan.FromMilliseconds(24),
            PositionLayerPanelUnderLayerButton);
    }

    private void OnThumbnailToggleClicked(object? sender, EventArgs e)
    {
        if (!EnsurePdfLoaded(showHint: true))
            return;

        SetInputModePanelVisible(false);
        if (LayerPanel.IsVisible)
        {
            AnimatePopupOut(LayerPanel, () => LayerPanel.IsVisible = false);
        }

        if (ThumbnailPanel.IsVisible)
        {
            AnimatePopupOut(ThumbnailPanel, () => ThumbnailPanel.IsVisible = false);
            return;
        }

        ThumbnailOverlaySwitch.IsToggled = _thumbnailIncludeInkOverlay;
        ThumbnailPanel.IsVisible = true;
        PositionThumbnailPanelUnderTopButton();
        AnimatePopupIn(ThumbnailPanel);
        ThumbnailPanel.Dispatcher.DispatchDelayed(
            TimeSpan.FromMilliseconds(24),
            PositionThumbnailPanelUnderTopButton);
        RefreshThumbnailList();
    }

    private void OnThumbnailCloseClicked(object? sender, EventArgs e)
    {
        if (!ThumbnailPanel.IsVisible)
            return;

        AnimatePopupOut(ThumbnailPanel, () => ThumbnailPanel.IsVisible = false);
    }

    private void OnThumbnailOverlayToggled(object? sender, ToggledEventArgs e)
    {
        _thumbnailIncludeInkOverlay = e.Value;
        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
    }

}
