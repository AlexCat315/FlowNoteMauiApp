using SkiaSharp;
using Flow.PDFView;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Controls;
using FlowNoteMauiApp.Models;
using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Maui.Devices;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private enum DrawingInputMode
    {
        PenStylus,
        FingerCapacitive,
        TapRead
    }

    private enum InkToolKind
    {
        None,
        Ballpoint,
        Fountain,
        Pencil,
        Marker,
        Eraser
    }

    private enum EraserToolMode
    {
        Pixel,
        Stroke,
        Lasso
    }

    private sealed class InkToolState
    {
        public InkToolState(SKColor color, float width)
        {
            Color = color;
            Width = width;
        }

        public SKColor Color { get; set; }
        public float Width { get; set; }
    }

    private DateTime _lastPanUpdateLogUtc = DateTime.MinValue;
    private InkToolKind _activeInkTool = InkToolKind.Ballpoint;
    private EraserToolMode _eraserMode = EraserToolMode.Pixel;
    private bool _isUpdatingToolUi;
    private readonly Dictionary<string, ImageSource> _toolIconSourceCache = new();
    private CancellationTokenSource? _toolTintUpdateCts;
    private readonly Dictionary<string, ImageSource> _thumbnailSourceCache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<int, PdfPageBounds> _pageBoundsCache = new();
    private bool _pageBoundsQueryUnavailable;
    private CancellationTokenSource? _thumbnailLoadCts;
    private const int ThumbnailRequestWidth = 220;
    private const int ThumbnailRequestHeight = 320;
    private const float MinPressureSensitivity = 0.4f;
    private const float MaxPressureSensitivity = 2.0f;
    private bool _thumbnailIncludeInkOverlay = true;
    private float _pressureSensitivity = 1f;

    private sealed class ThumbnailStrokeSnapshot
    {
        public required DrawingStroke Stroke { get; init; }
        public required float LayerOpacity { get; init; }
    }
    private readonly Dictionary<InkToolKind, InkToolState> _inkToolStates = new()
    {
        [InkToolKind.Ballpoint] = new InkToolState(SKColors.Black, 3f),
        [InkToolKind.Fountain] = new InkToolState(SKColors.Blue, 3.5f),
        [InkToolKind.Pencil] = new InkToolState(SKColors.Black, 2.2f),
        [InkToolKind.Marker] = new InkToolState(SKColors.Green, 6f),
        [InkToolKind.Eraser] = new InkToolState(SKColors.Transparent, 10f)
    };

    private bool EnsureDrawingReady(bool showHint = false)
    {
        if (IsEditorInitialized)
            return true;

        if (showHint)
            ShowStatus(T("OpenPdfFirst", "Open a PDF first."));
        return false;
    }

    // Drawing related methods
    private void OnDrawingToolbarCloseClicked(object? sender, EventArgs e)
    {
        AnimatePopupOut(DrawingToolbarPanel, () => DrawingToolbarPanel.IsVisible = false);
    }

    private void OnPenModeClicked(object? sender, EventArgs e)
    {
        HandleInkToolClicked(InkToolKind.Ballpoint);
    }

    private void SetInputModePanelVisible(bool visible)
    {
        if (!visible)
        {
            AnimatePopupOut(InputModePanel, () => InputModePanel.IsVisible = false);
            UpdateModeButtonVisual();
            return;
        }

        InputModePanel.IsVisible = true;
        PositionInputModePanelUnderTopModeButton();
        AnimatePopupIn(InputModePanel);

        UpdateModeButtonVisual();
    }

    private void UpdateModeButtonVisual()
    {
        ApplyTopModeButtonVisual();
    }

    private void ApplyTopModeButtonVisual()
    {
        var palette = Palette;
        var collapsedBackground = IsDarkTheme
            ? Color.FromArgb("#EAF1FB")
            : Color.FromArgb("#F5F8FC");
        TopModePenButton.BackgroundColor = collapsedBackground;
        TopModePenButton.BorderColor = Colors.Transparent;
        TopModePenButton.BorderWidth = 0;
        TopModePenButton.Source = _drawingInputMode switch
        {
            DrawingInputMode.FingerCapacitive => "icon_hand_mode.png",
            DrawingInputMode.TapRead => "icon_read_mode.png",
            _ => "icon_stylus_mode.png"
        };
    }

    private void SetFingerDrawSwitchState(bool isFingerMode)
    {
        if (EnableFingerDrawSwitch.IsToggled == isFingerMode)
            return;

        _isUpdatingFingerDrawSwitch = true;
        EnableFingerDrawSwitch.IsToggled = isFingerMode;
        _isUpdatingFingerDrawSwitch = false;
    }

    private void UpdateInputModeSelectionVisual()
    {
        var isPen = _drawingInputMode == DrawingInputMode.PenStylus;
        var isFinger = _drawingInputMode == DrawingInputMode.FingerCapacitive;
        var isRead = _drawingInputMode == DrawingInputMode.TapRead;

        InputModePenCheck.IsVisible = isPen;
        InputModeFingerCheck.IsVisible = isFinger;
        InputModeReadCheck.IsVisible = isRead;

        UpdateInputModeButtonVisual(InputModePenButton, isPen);
        UpdateInputModeButtonVisual(InputModeFingerButton, isFinger);
        UpdateInputModeButtonVisual(InputModeReadButton, isRead);
        UpdateModeButtonVisual();
    }

    private void UpdateInputModeButtonVisual(Button button, bool isSelected)
    {
        var palette = Palette;
        button.BackgroundColor = isSelected
            ? palette.ModeSelectionBackground
            : palette.ModeButtonCollapsedBackground;
        button.BorderColor = isSelected
            ? palette.ModeButtonExpandedBorder
            : palette.ModeButtonCollapsedBorder;
        button.BorderWidth = 1;
        button.TextColor = isSelected
            ? palette.ModeSelectionText
            : ThemePrimaryText;
    }

    private static bool IsNativeGesturePassthroughPlatform()
    {
        return DeviceInfo.Platform == DevicePlatform.MacCatalyst
            || DeviceInfo.Platform == DevicePlatform.iOS;
    }

    private static BrushType ToBrushType(InkToolKind tool)
    {
        return tool switch
        {
            InkToolKind.Ballpoint => BrushType.Pen,
            InkToolKind.Fountain => BrushType.Watercolor,
            InkToolKind.Pencil => BrushType.Pencil,
            InkToolKind.Marker => BrushType.Marker,
            _ => BrushType.Eraser
        };
    }

    private static DrawingCanvas.EraserMode ToCanvasEraserMode(EraserToolMode mode)
    {
        return mode switch
        {
            EraserToolMode.Stroke => DrawingCanvas.EraserMode.Stroke,
            EraserToolMode.Lasso => DrawingCanvas.EraserMode.Lasso,
            _ => DrawingCanvas.EraserMode.Pixel
        };
    }

    private InkToolState EnsureInkState(InkToolKind tool)
    {
        if (_inkToolStates.TryGetValue(tool, out var state))
            return state;

        state = new InkToolState(SKColors.Black, 3f);
        _inkToolStates[tool] = state;
        return state;
    }

    private string GetInkToolTitle(InkToolKind tool)
    {
        return tool switch
        {
            InkToolKind.Ballpoint => T("InkToolBallpoint", "Ballpoint Pen"),
            InkToolKind.Fountain => T("InkToolFountain", "Fountain Pen"),
            InkToolKind.Pencil => T("InkToolPencil", "Pencil"),
            InkToolKind.Marker => T("InkToolMarker", "Marker"),
            InkToolKind.Eraser => T("InkToolEraser", "Eraser"),
            _ => T("InkToolGeneric", "Tool")
        };
    }

    private string GetInkToolSelectedStatus(InkToolKind tool)
    {
        return tool switch
        {
            InkToolKind.Ballpoint => T("StatusBallpointSelected", "Ballpoint pen selected."),
            InkToolKind.Fountain => T("StatusFountainSelected", "Fountain pen selected."),
            InkToolKind.Pencil => T("StatusPencilSelected", "Pencil selected."),
            InkToolKind.Marker => T("StatusMarkerSelected", "Marker selected."),
            InkToolKind.Eraser => T("StatusEraserSelected", "Eraser selected."),
            _ => T("StatusPenArmed", "Pen selected.")
        };
    }

    private static string? GetColorKey(SKColor color)
    {
        if (color == SKColors.Black)
            return "Black";
        if (color == SKColors.Blue)
            return "Blue";
        if (color == SKColors.Red)
            return "Red";
        if (color == SKColors.Green)
            return "Green";
        if (color == SKColors.Orange)
            return "Orange";
        if (color == SKColors.White)
            return "White";
        return null;
    }

    private void ApplyActiveInkToolVisual()
    {
        if (_activeInkTool == InkToolKind.None)
        {
            UpdateToolSelection(_activeInkTool);
            UpdateToolSettingsPanelState();
            return;
        }

        var state = EnsureInkState(_activeInkTool);
        if (IsEditorInitialized)
        {
            var isEraser = _activeInkTool == InkToolKind.Eraser;
            DrawingCanvas.IsErasing = isEraser;
            DrawingCanvas.IsHighlighter = _activeInkTool == InkToolKind.Marker;
            DrawingCanvas.ActiveBrushType = ToBrushType(_activeInkTool);
            DrawingCanvas.EraserBehavior = ToCanvasEraserMode(_eraserMode);
            DrawingCanvas.UsePressureSensitivity = !isEraser;
            DrawingCanvas.PressureSensitivity = _pressureSensitivity;
            DrawingCanvas.StrokeWidth = state.Width;
            if (!isEraser)
            {
                DrawingCanvas.StrokeColor = state.Color;
            }
        }

        UpdateToolSelection(_activeInkTool);
        UpdateToolSettingsPanelState();
    }

    private void ArmInkTool(InkToolKind tool, bool showStatus = false, bool showToolbar = false)
    {
        _activeInkTool = tool;
        SetInputModePanelVisible(false);
        ApplyActiveInkToolVisual();

        if (!IsEditorInitialized)
            return;

        var forceNativeInputPassthrough = IsNativeGesturePassthroughPlatform();
        if (!forceNativeInputPassthrough && _drawingInputMode != DrawingInputMode.TapRead)
        {
            DrawingCanvas.EnableDrawing = true;
        }
        var shouldShowToolbar = showToolbar && _drawingInputMode != DrawingInputMode.TapRead;
        if (shouldShowToolbar)
        {
            DrawingToolbarPanel.IsVisible = true;
            PositionDrawingToolbarPanelUnderTool(_activeInkTool);
            AnimatePopupIn(DrawingToolbarPanel);
        }
        else if (DrawingToolbarPanel.IsVisible)
        {
            AnimatePopupOut(DrawingToolbarPanel, () => DrawingToolbarPanel.IsVisible = false);
        }

        if (showStatus)
        {
            ShowStatus(GetInkToolSelectedStatus(_activeInkTool));
        }
    }

    private void HandleInkToolClicked(InkToolKind tool)
    {
        if (!EnsureDrawingReady())
            return;

        var isSameTool = _activeInkTool == tool;
        if (!isSameTool)
        {
            // First click after switching tool: only select tool, keep panel hidden.
            ArmInkTool(tool, showToolbar: false);
            return;
        }

        if (_drawingInputMode == DrawingInputMode.TapRead)
            return;

        var shouldShow = !DrawingToolbarPanel.IsVisible;
        if (shouldShow)
        {
            DrawingToolbarPanel.IsVisible = true;
            PositionDrawingToolbarPanelUnderTool(_activeInkTool);
            AnimatePopupIn(DrawingToolbarPanel);
        }
        else
        {
            AnimatePopupOut(DrawingToolbarPanel, () => DrawingToolbarPanel.IsVisible = false);
        }
    }

    private void ClearArmedInkTool(bool hideDrawingToolbar)
    {
        if (!IsEditorInitialized)
            return;

        DrawingCanvas.EnableDrawing = false;
        if (hideDrawingToolbar)
        {
            DrawingToolbarPanel.IsVisible = false;
        }
    }

    private void ApplyInputMode(DrawingInputMode mode, bool showStatus = false, bool activateDrawing = true)
    {
        StopTwoFingerInertia();
        _drawingInputMode = mode;
        UpdateInputModeSelectionVisual();
        SetFingerDrawSwitchState(mode == DrawingInputMode.FingerCapacitive);

        if (!IsEditorInitialized)
            return;

        var wasDrawingEnabled = DrawingCanvas.EnableDrawing;
        var forceNativeInputPassthrough = IsNativeGesturePassthroughPlatform();
        var hasInkTool = _activeInkTool != InkToolKind.None;
        var targetDrawingEnabled = mode != DrawingInputMode.TapRead
            && hasInkTool
            && (activateDrawing || DrawingCanvas.EnableDrawing);
        if (forceNativeInputPassthrough)
        {
            targetDrawingEnabled = false;
        }

        ApplyActiveInkToolVisual();
        DrawingCanvas.ForceInputTransparent = forceNativeInputPassthrough || mode == DrawingInputMode.TapRead;
        DrawingCanvas.IsEnabled = !DrawingCanvas.ForceInputTransparent;
        LogInputGesture(
            $"mode-switch={mode} platform={DeviceInfo.Platform} activate={activateDrawing} drawing={targetDrawingEnabled} " +
            $"tool={_activeInkTool} native-pass-through={forceNativeInputPassthrough}");

        switch (mode)
        {
            case DrawingInputMode.PenStylus:
                DrawingCanvas.IsPenMode = true;
                DrawingCanvas.EnableDrawing = targetDrawingEnabled;
                DrawingCanvas.IsVisible = true;
                DrawingToolbarPanel.IsVisible = DrawingToolbarPanel.IsVisible && targetDrawingEnabled;
                if (DrawingToolbarPanel.IsVisible)
                {
                    PositionDrawingToolbarPanelUnderTool(_activeInkTool);
                }
                if (showStatus)
                {
                    ShowStatus(forceNativeInputPassthrough
                        ? T("StatusNativePdfInput", "Native PDF gestures are enabled on this platform. Handwriting capture is currently limited.")
                        : !hasInkTool
                            ? T("StatusSelectBrushFirst", "Please select a pen/highlighter/eraser before writing.")
                        : T("StatusPenMode", "Handwriting mode: stylus writes, finger/mouse pans and zooms."));
                }
                break;
            case DrawingInputMode.FingerCapacitive:
                DrawingCanvas.IsPenMode = false;
                DrawingCanvas.EnableDrawing = targetDrawingEnabled;
                DrawingCanvas.IsVisible = true;
                DrawingToolbarPanel.IsVisible = DrawingToolbarPanel.IsVisible && targetDrawingEnabled;
                if (DrawingToolbarPanel.IsVisible)
                {
                    PositionDrawingToolbarPanelUnderTool(_activeInkTool);
                }
                if (showStatus)
                {
                    ShowStatus(forceNativeInputPassthrough
                        ? T("StatusNativePdfInput", "Native PDF gestures are enabled on this platform. Handwriting capture is currently limited.")
                        : !hasInkTool
                            ? T("StatusSelectBrushFirst", "Please select a pen/highlighter/eraser before writing.")
                        : T("StatusFingerMode", "Touch mode: one finger writes, two fingers pan/zoom; in single-page mode two-finger swipe flips page."));
                }
                break;
            case DrawingInputMode.TapRead:
                DrawingCanvas.IsPenMode = false;
                DrawingCanvas.EnableDrawing = false;
                DrawingCanvas.IsVisible = true;
                DrawingToolbarPanel.IsVisible = false;
                ThumbnailPanel.IsVisible = false;
                LayerPanel.IsVisible = false;
                if (wasDrawingEnabled)
                    QueueInkSave();
                if (showStatus)
                    ShowStatus(T("StatusReadMode", "Read mode: PDF and notes visible, writing disabled."));
                break;
        }

        UpdateModeButtonVisual();
    }

    private void OnInputModePenClicked(object? sender, EventArgs e)
    {
        ApplyInputMode(DrawingInputMode.PenStylus, showStatus: true);
        SetInputModePanelVisible(false);
    }

    private void OnInputModeFingerClicked(object? sender, EventArgs e)
    {
        ApplyInputMode(DrawingInputMode.FingerCapacitive, showStatus: true);
        SetInputModePanelVisible(false);
    }

    private void OnInputModeReadClicked(object? sender, EventArgs e)
    {
        ApplyInputMode(DrawingInputMode.TapRead, showStatus: true);
        SetInputModePanelVisible(false);
    }

    private void OnTopModeToggleClicked(object? sender, EventArgs e)
    {
        SetInputModePanelVisible(!InputModePanel.IsVisible);
    }

    private void PositionInputModePanelUnderTopModeButton()
    {
        if (!InputModePanel.IsVisible)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            void ApplyPosition(int attempt)
            {
                if (!InputModePanel.IsVisible)
                    return;

                var panelWidth = InputModePanel.Width > 1
                    ? InputModePanel.Width
                    : (InputModePanel.WidthRequest > 1 ? InputModePanel.WidthRequest : 248d);
                var panelHeight = InputModePanel.Height > 1 ? InputModePanel.Height : 0d;

                var anchorX = GetVisualOffsetX(TopModePenButton, TopBarPanel) + TopBarPanel.X;
                var anchorY = GetVisualOffsetY(TopModePenButton, TopBarPanel) + TopBarPanel.Y;
                var anchorWidth = TopModePenButton.Width > 1 ? TopModePenButton.Width : TopModePenButton.WidthRequest;
                var anchorHeight = TopModePenButton.Height > 1 ? TopModePenButton.Height : TopModePenButton.HeightRequest;

                var hasValidLayout = anchorWidth > 1
                    && anchorHeight > 1
                    && TopBarPanel.Height > 1
                    && EditorChromeView.Width > 1
                    && anchorX > 1
                    && anchorY > 1;
                if (!hasValidLayout && attempt < 10)
                {
                    InputModePanel.Dispatcher.DispatchDelayed(
                        TimeSpan.FromMilliseconds(16),
                        () => ApplyPosition(attempt + 1));
                    return;
                }

                var targetX = anchorX + (anchorWidth / 2d) - (panelWidth / 2d);
                var maxX = Math.Max(8d, EditorChromeView.Width - panelWidth - 8d);
                targetX = Math.Clamp(targetX, 8d, maxX);

                var targetY = anchorY + anchorHeight + 4d;
                var minY = Math.Max(TopBarPanel.Height + 2d, 6d);
                if (panelHeight > 1 && EditorChromeView.Height > 1)
                {
                    var maxY = Math.Max(minY, EditorChromeView.Height - panelHeight - 6d);
                    targetY = Math.Clamp(targetY, minY, maxY);
                }
                else
                {
                    targetY = Math.Max(targetY, minY);
                }

                InputModePanel.TranslationX = 0;
                InputModePanel.TranslationY = 0;
                InputModePanel.Margin = new Thickness(targetX, targetY, 0, 0);
            }

            ApplyPosition(0);
        });
    }

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
        if (InputModePanel.IsVisible)
        {
            PositionInputModePanelUnderTopModeButton();
        }

        if (DrawingToolbarPanel.IsVisible)
        {
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

    private void OnHighlighterClicked(object? sender, EventArgs e)
    {
        HandleInkToolClicked(InkToolKind.Fountain);
    }

    private void OnPencilClicked(object? sender, EventArgs e)
    {
        HandleInkToolClicked(InkToolKind.Pencil);
    }

    private void OnMarkerClicked(object? sender, EventArgs e)
    {
        HandleInkToolClicked(InkToolKind.Marker);
    }

    private void OnEraserClicked(object? sender, EventArgs e)
    {
        HandleInkToolClicked(InkToolKind.Eraser);
    }

    private void UpdateToolSelection(InkToolKind selectedTool)
    {
        var palette = Palette;
        var selectedColor = palette.ToolSelectedBackground;
        var normalColor = palette.ToolNormalBackground;
        var selectedBorder = palette.ToolSelectedBorder;
        var normalBorder = palette.ToolNormalBorder;

        ApplyToolButtonSelection(PenModeButton, selectedTool == InkToolKind.Ballpoint, selectedColor, normalColor, selectedBorder, normalBorder);
        ApplyToolButtonSelection(HighlighterButton, selectedTool == InkToolKind.Fountain, selectedColor, normalColor, selectedBorder, normalBorder);
        ApplyToolButtonSelection(PencilButton, selectedTool == InkToolKind.Pencil, selectedColor, normalColor, selectedBorder, normalBorder);
        ApplyToolButtonSelection(MarkerButton, selectedTool == InkToolKind.Marker, selectedColor, normalColor, selectedBorder, normalBorder);
        ApplyToolButtonSelection(EraserButton, selectedTool == InkToolKind.Eraser, selectedColor, normalColor, selectedBorder, normalBorder);
    }

    private static void ApplyToolButtonSelection(
        ImageButton button,
        bool selected,
        Color selectedColor,
        Color normalColor,
        Color selectedBorder,
        Color normalBorder)
    {
        button.BackgroundColor = selected ? selectedColor : normalColor;
        button.BorderColor = selected ? selectedBorder : normalBorder;
        button.BorderWidth = 1;
    }

    private void OnRedoClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.Redo();
        QueueInkSave();
    }

    private void OnUndoClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.Undo();
        QueueInkSave();
    }

    private async void OnClearClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        if (!EnsurePdfLoaded(showHint: true))
            return;

        var confirmed = await DisplayAlertAsync(
            T("ClearCurrentPageTitle", "Clear Current Page"),
            T("ClearCurrentPageMessage", "Clear all handwriting on the current page?"),
            T("ClearCurrentPageConfirm", "Clear"),
            T("CancelAction", "Cancel"));
        if (!confirmed)
            return;

        var removedCount = await ClearCurrentPageInkAsync(_currentPageIndex);
        if (removedCount <= 0)
        {
            ShowStatus(T("ClearCurrentPageNone", "No handwriting found on the current page."));
            return;
        }

        DrawingCanvas.InvalidateSurface();
        RefreshLayerList();
        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
        ShowStatus(TF("ClearCurrentPageDoneFormat", "Cleared {0} strokes on current page.", removedCount));
        QueueInkSave();
    }

    private void OnStrokeWidthChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isUpdatingToolUi)
            return;

        if (!EnsureDrawingReady())
            return;

        var state = EnsureInkState(_activeInkTool);
        state.Width = (float)e.NewValue;
        if (IsEditorInitialized)
        {
            DrawingCanvas.StrokeWidth = state.Width;
        }

        StrokeWidthLabel.Text = $"{state.Width:0.00}mm";
    }

    private void OnPressureSensitivityChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isUpdatingToolUi)
            return;

        _pressureSensitivity = Math.Clamp((float)e.NewValue, MinPressureSensitivity, MaxPressureSensitivity);
        if (IsEditorInitialized)
        {
            DrawingCanvas.PressureSensitivity = _pressureSensitivity;
        }

        PressureValueLabel.Text = $"{Math.Round(_pressureSensitivity * 100f):0}%";
    }

    private void OnColorBlackClicked(object? sender, EventArgs e)
    {
        SetActiveToolColor(SKColors.Black);
    }

    private void OnColorRedClicked(object? sender, EventArgs e)
    {
        SetActiveToolColor(SKColors.Red);
    }

    private void OnColorBlueClicked(object? sender, EventArgs e)
    {
        SetActiveToolColor(SKColors.Blue);
    }

    private void OnColorGreenClicked(object? sender, EventArgs e)
    {
        SetActiveToolColor(SKColors.Green);
    }

    private void OnColorOrangeClicked(object? sender, EventArgs e)
    {
        SetActiveToolColor(SKColors.Orange);
    }

    private void OnColorWhiteClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        SetActiveToolColor(SKColors.White);
    }

    private void SetActiveToolColor(SKColor color)
    {
        if (!EnsureDrawingReady())
            return;

        if (_activeInkTool == InkToolKind.Eraser || _activeInkTool == InkToolKind.None)
            return;

        var state = EnsureInkState(_activeInkTool);
        state.Color = color;
        if (IsEditorInitialized)
        {
            DrawingCanvas.StrokeColor = color;
        }

        UpdateColorSelection(GetColorKey(color));
        UpdateToolButtonTintColors();
    }

    private void UpdateToolButtonTintColors()
    {
        _toolTintUpdateCts?.Cancel();
        _toolTintUpdateCts?.Dispose();
        _toolTintUpdateCts = new CancellationTokenSource();
        var token = _toolTintUpdateCts.Token;
        _ = UpdateToolButtonTintColorsAsync(token);
    }

    private async Task UpdateToolButtonTintColorsAsync(CancellationToken token)
    {
        try
        {
            var penSource = await CreateTintedToolIconSourceAsync("toolicons/icon_ballpoint_pen.png", EnsureInkState(InkToolKind.Ballpoint).Color, token);
            var highlighterSource = await CreateTintedToolIconSourceAsync("toolicons/icon_gelpen.png", EnsureInkState(InkToolKind.Fountain).Color, token);
            var pencilSource = await CreateTintedToolIconSourceAsync("toolicons/icon_pencil.png", EnsureInkState(InkToolKind.Pencil).Color, token);
            var markerSource = await CreateTintedToolIconSourceAsync("toolicons/icon_markpen.png", EnsureInkState(InkToolKind.Marker).Color, token);
            if (token.IsCancellationRequested)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                PenModeButton.Source = penSource;
                HighlighterButton.Source = highlighterSource;
                PencilButton.Source = pencilSource;
                MarkerButton.Source = markerSource;
                EraserButton.Source = ImageSource.FromFile("icon_eraser.png");
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<ImageSource> CreateTintedToolIconSourceAsync(string iconFile, SKColor tintColor, CancellationToken token)
    {
        var cacheKey = $"{iconFile}:{tintColor.Alpha:X2}{tintColor.Red:X2}{tintColor.Green:X2}{tintColor.Blue:X2}";
        if (_toolIconSourceCache.TryGetValue(cacheKey, out var cachedSource))
            return cachedSource;

        var fallbackFile = Path.GetFileName(iconFile);
        try
        {
            await using var rawStream = await OpenToolIconTemplateStreamAsync(iconFile);
            if (rawStream is null)
                return ImageSource.FromFile(fallbackFile);

            using var baseBitmap = SKBitmap.Decode(rawStream);
            if (baseBitmap is null)
                return ImageSource.FromFile(fallbackFile);

            using var tintedBitmap = new SKBitmap(baseBitmap.Width, baseBitmap.Height, baseBitmap.ColorType, baseBitmap.AlphaType);
            using (var canvas = new SKCanvas(tintedBitmap))
            using (var paint = new SKPaint
            {
                IsAntialias = true,
                ColorFilter = SKColorFilter.CreateBlendMode(tintColor, SKBlendMode.SrcIn)
            })
            {
                canvas.Clear(SKColors.Transparent);
                canvas.DrawBitmap(baseBitmap, 0, 0, paint);
                canvas.Flush();
            }

            token.ThrowIfCancellationRequested();
            using var outputImage = SKImage.FromBitmap(tintedBitmap);
            using var outputData = outputImage.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = outputData.ToArray();
            var source = ImageSource.FromStream(() => new MemoryStream(bytes));
            _toolIconSourceCache[cacheKey] = source;
            return source;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ToolTint] Failed to tint '{iconFile}': {ex.Message}");
            return ImageSource.FromFile(fallbackFile);
        }
    }

    private static IEnumerable<string> BuildToolIconPathCandidates(string iconFile)
    {
        var normalized = iconFile.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        yield return normalized;

        var windowsNormalized = normalized.Replace('/', '\\');
        if (!string.Equals(windowsNormalized, normalized, StringComparison.Ordinal))
            yield return windowsNormalized;

        if (!string.IsNullOrWhiteSpace(fileName))
            yield return fileName;
    }

    private static async Task<Stream?> OpenToolIconTemplateStreamAsync(string iconFile)
    {
        foreach (var candidate in BuildToolIconPathCandidates(iconFile).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                return await FileSystem.OpenAppPackageFileAsync(candidate);
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        return null;
    }

    private void UpdateToolSettingsPanelState()
    {
        if (!IsEditorInitialized)
            return;

        var state = EnsureInkState(_activeInkTool);
        _isUpdatingToolUi = true;
        try
        {
            var min = (float)StrokeWidthSlider.Minimum;
            var max = (float)StrokeWidthSlider.Maximum;
            var clamped = Math.Clamp(state.Width, min, max);
            if (Math.Abs(StrokeWidthSlider.Value - clamped) > 0.001f)
            {
                StrokeWidthSlider.Value = clamped;
            }

            var pressureClamped = Math.Clamp(_pressureSensitivity, MinPressureSensitivity, MaxPressureSensitivity);
            if (Math.Abs(PressureSensitivitySlider.Value - pressureClamped) > 0.001f)
            {
                PressureSensitivitySlider.Value = pressureClamped;
            }
        }
        finally
        {
            _isUpdatingToolUi = false;
        }

        ToolSettingsTitleLabel.Text = GetInkToolTitle(_activeInkTool);
        DrawingPenWidthLabel.Text = _activeInkTool == InkToolKind.Eraser
            ? T("DrawingEraserSize", "Size")
            : T("DrawingPenWidth", "Stroke");
        StrokeWidthLabel.Text = $"{state.Width:0.00}mm";
        PressureSensitivityTitleLabel.Text = T("PressureSensitivity", "Pressure");
        PressureValueLabel.Text = $"{Math.Round(_pressureSensitivity * 100f):0}%";
        EraserModePanel.IsVisible = _activeInkTool == InkToolKind.Eraser;
        var supportsInkSettings = _activeInkTool != InkToolKind.Eraser && _activeInkTool != InkToolKind.None;
        PressurePanel.IsVisible = supportsInkSettings;
        ToolColorPanel.IsVisible = supportsInkSettings;
        UpdateEraserModeSelectionVisual();
        UpdateColorSelection(_activeInkTool == InkToolKind.Eraser ? null : GetColorKey(state.Color));
        if (DrawingToolbarPanel.IsVisible)
        {
            PositionDrawingToolbarPanelUnderTool(_activeInkTool);
        }
    }

    private void OnPixelEraserModeClicked(object? sender, EventArgs e)
    {
        _eraserMode = EraserToolMode.Pixel;
        ApplyActiveInkToolVisual();
    }

    private void OnStrokeEraserModeClicked(object? sender, EventArgs e)
    {
        _eraserMode = EraserToolMode.Stroke;
        ApplyActiveInkToolVisual();
    }

    private void OnLassoEraserModeClicked(object? sender, EventArgs e)
    {
        _eraserMode = EraserToolMode.Lasso;
        ApplyActiveInkToolVisual();
    }

    private void UpdateEraserModeSelectionVisual()
    {
        var palette = Palette;
        ApplyEraserModeButtonVisual(PixelEraserModeButton, _eraserMode == EraserToolMode.Pixel, palette);
        ApplyEraserModeButtonVisual(StrokeEraserModeButton, _eraserMode == EraserToolMode.Stroke, palette);
        ApplyEraserModeButtonVisual(LassoEraserModeButton, _eraserMode == EraserToolMode.Lasso, palette);
    }

    private static void ApplyEraserModeButtonVisual(Button button, bool selected, ThemePalette palette)
    {
        button.BackgroundColor = selected ? palette.ModeSelectionBackground : palette.ModeButtonCollapsedBackground;
        button.BorderColor = selected ? palette.ModeButtonExpandedBorder : palette.ModeButtonCollapsedBorder;
        button.BorderWidth = 1;
        button.TextColor = selected ? palette.ModeSelectionText : palette.TabInactiveText;
    }

    private void UpdateColorSelection(string? selectedColor)
    {
        var palette = Palette;
        var selectedBorderColor = palette.ModeButtonExpandedBorder;
        var normalBorderColor = palette.ColorSwatchNormalBorder;

        ColorBlack.BorderColor = selectedColor == "Black" ? selectedBorderColor : normalBorderColor;
        ColorRed.BorderColor = selectedColor == "Red" ? selectedBorderColor : normalBorderColor;
        ColorBlue.BorderColor = selectedColor == "Blue" ? selectedBorderColor : normalBorderColor;
        ColorGreen.BorderColor = selectedColor == "Green" ? selectedBorderColor : normalBorderColor;
        ColorOrange.BorderColor = selectedColor == "Orange" ? selectedBorderColor : normalBorderColor;
        ColorWhite.BorderColor = selectedColor == "White" ? selectedBorderColor : palette.ColorSwatchWhiteBorder;
    }

    private void InvalidateThumbnailCache()
    {
        _thumbnailLoadCts?.Cancel();
        _thumbnailLoadCts?.Dispose();
        _thumbnailLoadCts = null;
        _thumbnailSourceCache.Clear();
    }

    private void RefreshThumbnailList()
    {
        ThumbnailList.Children.Clear();
        if (!IsEditorInitialized || _totalPageCount <= 0)
            return;

        _thumbnailLoadCts?.Cancel();
        _thumbnailLoadCts?.Dispose();
        _thumbnailLoadCts = new CancellationTokenSource();
        var token = _thumbnailLoadCts.Token;
        var overlaySnapshots = _thumbnailIncludeInkOverlay
            ? CaptureThumbnailStrokeSnapshots()
            : null;

        const int maxVisibleItems = 80;
        var startIndex = 0;
        var endIndex = _totalPageCount - 1;
        if (_totalPageCount > maxVisibleItems)
        {
            startIndex = Math.Clamp(_currentPageIndex - (maxVisibleItems / 2), 0, _totalPageCount - maxVisibleItems);
            endIndex = startIndex + maxVisibleItems - 1;
        }

        if (startIndex > 0)
        {
            ThumbnailList.Children.Add(CreateThumbnailListItem(
                0,
                false,
                token,
                overlaySnapshots,
                TF("ThumbnailPageHeadFormat", "Page {0} ...", 1)));
        }

        for (int i = startIndex; i <= endIndex; i++)
        {
            ThumbnailList.Children.Add(CreateThumbnailListItem(i, i == _currentPageIndex, token, overlaySnapshots));
        }

        if (endIndex < _totalPageCount - 1)
        {
            ThumbnailList.Children.Add(CreateThumbnailListItem(
                _totalPageCount - 1,
                false,
                token,
                overlaySnapshots,
                TF("ThumbnailPageTailFormat", "... Page {0}", _totalPageCount)));
        }
    }

    private string BuildThumbnailCacheKey(int pageIndex)
    {
        var noteId = string.IsNullOrWhiteSpace(_currentNoteId) ? "none" : _currentNoteId!;
        var overlayKey = _thumbnailIncludeInkOverlay ? "overlay" : "pdf";
        return $"{noteId}:{pageIndex}:{ThumbnailRequestWidth}x{ThumbnailRequestHeight}:{overlayKey}";
    }

    private Border CreateThumbnailListItem(
        int pageIndex,
        bool isCurrent,
        CancellationToken token,
        IReadOnlyList<ThumbnailStrokeSnapshot>? overlaySnapshots = null,
        string? titleOverride = null)
    {
        var palette = Palette;
        var previewImage = new Image
        {
            Aspect = Aspect.AspectFit,
            Source = "icon_file.png",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var cacheKey = BuildThumbnailCacheKey(pageIndex);
        if (_thumbnailSourceCache.TryGetValue(cacheKey, out var cachedSource))
        {
            previewImage.Source = cachedSource;
        }
        else
        {
            _ = LoadThumbnailForPageAsync(pageIndex, cacheKey, previewImage, token, overlaySnapshots);
        }

        var item = new Border
        {
            BackgroundColor = isCurrent ? palette.LayerSelectedBackground : Colors.Transparent,
            Stroke = isCurrent ? palette.LayerSelectedBorder : palette.LayerNormalBorder,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(8, 8, 8, 6),
            Content = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Border
                    {
                        WidthRequest = 122,
                        HeightRequest = 172,
                        HorizontalOptions = LayoutOptions.Center,
                        Stroke = isCurrent ? palette.ModeButtonExpandedBorder : palette.LayerNormalBorder,
                        StrokeThickness = 1,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                        BackgroundColor = isCurrent ? palette.ModeButtonExpandedBackground : palette.ModeButtonCollapsedBackground,
                        Padding = 2,
                        Content = previewImage
                    },
                    new Label
                    {
                        Text = titleOverride ?? TF("ThumbnailPageFormat", "Page {0}", pageIndex + 1),
                        FontSize = 11,
                        HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = ThemePrimaryText
                    }
                }
            }
        };

        item.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                if (!EnsurePdfLoaded())
                    return;

                PdfViewer.GoToPage(pageIndex);
                _currentPageIndex = pageIndex;
                RefreshThumbnailList();
            })
        });

        return item;
    }

    private async Task LoadThumbnailForPageAsync(
        int pageIndex,
        string cacheKey,
        Image target,
        CancellationToken token,
        IReadOnlyList<ThumbnailStrokeSnapshot>? overlaySnapshots)
    {
        if (!IsEditorInitialized || token.IsCancellationRequested)
            return;

        try
        {
            var thumbnailStream = await PdfViewer
                .GetThumbnailAsync(pageIndex, ThumbnailRequestWidth, ThumbnailRequestHeight)
                .ConfigureAwait(false);
            if (thumbnailStream is null || token.IsCancellationRequested)
                return;

            byte[] bytes;
            using (thumbnailStream)
            using (var memory = new MemoryStream())
            {
                await thumbnailStream.CopyToAsync(memory, token).ConfigureAwait(false);
                if (memory.Length == 0 || token.IsCancellationRequested)
                    return;

                bytes = memory.ToArray();
            }

            if (_thumbnailIncludeInkOverlay
                && overlaySnapshots is { Count: > 0 }
                && !token.IsCancellationRequested)
            {
                var pageBounds = await GetCachedPageBoundsAsync(pageIndex).ConfigureAwait(false);
                if (pageBounds is PdfPageBounds bounds)
                {
                    var composedBytes = await Task
                        .Run(() => ComposeThumbnailWithInkOverlay(bytes, bounds, overlaySnapshots, token), token)
                        .ConfigureAwait(false);
                    if (composedBytes is { Length: > 0 })
                    {
                        bytes = composedBytes;
                    }
                }
            }

            if (token.IsCancellationRequested)
                return;

            var source = ImageSource.FromStream(() => new MemoryStream(bytes));
            _thumbnailSourceCache[cacheKey] = source;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    target.Source = source;
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    private IReadOnlyList<ThumbnailStrokeSnapshot> CaptureThumbnailStrokeSnapshots()
    {
        if (!IsEditorInitialized)
            return Array.Empty<ThumbnailStrokeSnapshot>();

        var snapshots = new List<ThumbnailStrokeSnapshot>();
        foreach (var layer in DrawingCanvas.Layers)
        {
            if (!layer.IsVisible || layer.Opacity <= 0.001f)
                continue;

            var layerOpacity = Math.Clamp(layer.Opacity, 0f, 1f);
            foreach (var stroke in layer.Strokes)
            {
                if (stroke.Points.Count < 2)
                    continue;

                snapshots.Add(new ThumbnailStrokeSnapshot
                {
                    Stroke = CloneStrokeForThumbnail(stroke),
                    LayerOpacity = layerOpacity
                });
            }
        }

        return snapshots;
    }

    private static DrawingStroke CloneStrokeForThumbnail(DrawingStroke stroke)
    {
        return new DrawingStroke
        {
            Id = stroke.Id,
            Color = stroke.Color,
            StrokeWidth = stroke.StrokeWidth,
            Opacity = stroke.Opacity,
            IsEraser = stroke.IsEraser,
            BrushType = stroke.BrushType,
            Options = new StrokeOptions
            {
                PressureEnabled = stroke.Options.PressureEnabled,
                SmoothingEnabled = stroke.Options.SmoothingEnabled,
                SmoothingFactor = stroke.Options.SmoothingFactor,
                MinPressure = stroke.Options.MinPressure,
                MaxPressure = stroke.Options.MaxPressure,
                TaperEnabled = stroke.Options.TaperEnabled,
                TaperStart = stroke.Options.TaperStart,
                TaperEnd = stroke.Options.TaperEnd,
                Streamline = stroke.Options.Streamline
            },
            Points = stroke.Points
                .Select(point => new DrawingPoint(point.X, point.Y, point.Pressure, point.Timestamp))
                .ToList()
        };
    }

    private static byte[]? ComposeThumbnailWithInkOverlay(
        byte[] baseThumbnailBytes,
        PdfPageBounds pageBounds,
        IReadOnlyList<ThumbnailStrokeSnapshot> snapshots,
        CancellationToken token)
    {
        using var baseBitmap = SKBitmap.Decode(baseThumbnailBytes);
        if (baseBitmap is null || baseBitmap.Width <= 0 || baseBitmap.Height <= 0)
            return null;

        var imageInfo = new SKImageInfo(baseBitmap.Width, baseBitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(imageInfo);
        if (surface is null)
            return null;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(baseBitmap, 0, 0);
        using var overlaySurface = SKSurface.Create(imageInfo);
        if (overlaySurface is null)
            return null;

        var overlayCanvas = overlaySurface.Canvas;
        overlayCanvas.Clear(SKColors.Transparent);

        var safePageWidth = Math.Max(1d, pageBounds.Width);
        var safePageHeight = Math.Max(1d, pageBounds.Height);
        var scaleX = (float)(imageInfo.Width / safePageWidth);
        var scaleY = (float)(imageInfo.Height / safePageHeight);
        var translateX = (float)(-pageBounds.X * scaleX);
        var translateY = (float)(-pageBounds.Y * scaleY);
        var strokeScale = Math.Max(0.12f, (Math.Abs(scaleX) + Math.Abs(scaleY)) * 0.5f);
        var transform = SKMatrix.CreateScaleTranslation(scaleX, scaleY, translateX, translateY);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        foreach (var snapshot in snapshots)
        {
            token.ThrowIfCancellationRequested();

            var stroke = snapshot.Stroke;
            if (stroke.Points.Count < 2 || !DoesStrokeIntersectPage(stroke, pageBounds))
                continue;

            using var path = new SKPath(stroke.CreatePath());
            path.Transform(transform);

            var alpha = stroke.IsEraser
                ? (byte)0
                : (byte)Math.Clamp((int)Math.Round(stroke.Color.Alpha * snapshot.LayerOpacity * stroke.Opacity), 0, 255);
            paint.Color = stroke.IsEraser ? SKColors.Transparent : stroke.Color.WithAlpha(alpha);
            paint.StrokeWidth = Math.Max(0.4f, stroke.StrokeWidth * strokeScale);
            paint.BlendMode = GetThumbnailStrokeBlendMode(stroke);

            overlayCanvas.DrawPath(path, paint);
        }

        overlayCanvas.Flush();
        using var overlayImage = overlaySurface.Snapshot();
        canvas.DrawImage(overlayImage, 0, 0);
        canvas.Flush();
        using var composedImage = surface.Snapshot();
        using var encoded = composedImage.Encode(SKEncodedImageFormat.Png, 96);
        return encoded?.ToArray();
    }

    private static SKBlendMode GetThumbnailStrokeBlendMode(DrawingStroke stroke)
    {
        if (stroke.IsEraser)
            return SKBlendMode.Clear;

        return SKBlendMode.SrcOver;
    }

    private static bool DoesStrokeIntersectPage(DrawingStroke stroke, PdfPageBounds pageBounds)
    {
        if (stroke.Points.Count == 0)
            return false;

        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        foreach (var point in stroke.Points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        var pageLeft = pageBounds.X;
        var pageTop = pageBounds.Y;
        var pageRight = pageBounds.X + pageBounds.Width;
        var pageBottom = pageBounds.Y + pageBounds.Height;
        return maxX >= pageLeft
            && minX <= pageRight
            && maxY >= pageTop
            && minY <= pageBottom;
    }

    private void ResetPdfPageBoundsCache()
    {
        _pageBoundsCache.Clear();
        _pageBoundsQueryUnavailable = false;
    }

    private async Task<PdfPageBounds?> GetCachedPageBoundsAsync(int pageIndex)
    {
        if (!IsEditorInitialized || pageIndex < 0 || _pageBoundsQueryUnavailable)
            return null;

        if (_pageBoundsCache.TryGetValue(pageIndex, out var cachedBounds))
            return cachedBounds;

        var pageBounds = await PdfViewer.GetPageBoundsAsync(pageIndex).ConfigureAwait(false);
        if (pageBounds is PdfPageBounds bounds)
        {
            _pageBoundsCache[pageIndex] = bounds;
            return bounds;
        }

        _pageBoundsQueryUnavailable = true;
        return null;
    }

    private async Task<bool> DoesStrokeIntersectAnyPageAsync(DrawingStroke stroke)
    {
        if (!IsEditorInitialized || stroke.Points.Count == 0 || _totalPageCount <= 0)
            return true;

        var checkedAnyPageBounds = false;
        for (var pageIndex = 0; pageIndex < _totalPageCount; pageIndex++)
        {
            var bounds = await GetCachedPageBoundsAsync(pageIndex);
            if (bounds is not PdfPageBounds pageBounds)
                continue;

            checkedAnyPageBounds = true;
            if (DoesStrokeIntersectPage(stroke, pageBounds))
                return true;
        }

        // If page bounds are unavailable on this platform/document, keep the stroke.
        return !checkedAnyPageBounds;
    }

    private async Task<int> ClearCurrentPageInkAsync(int pageIndex)
    {
        if (!IsEditorInitialized || pageIndex < 0)
            return 0;

        var bounds = await GetCachedPageBoundsAsync(pageIndex);
        if (bounds is not PdfPageBounds pageBounds)
            return 0;

        var removedCount = 0;
        foreach (var layer in DrawingCanvas.Layers)
        {
            removedCount += layer.Strokes.RemoveAll(stroke => DoesStrokeIntersectPage(stroke, pageBounds));
        }

        return removedCount;
    }

    private void OnAddLayerClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.AddLayer();
        RefreshLayerList();
        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
        QueueInkSave();
    }

    private void OnDeleteLayerClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        if (DrawingCanvas.Layers.Count > 1)
        {
            DrawingCanvas.RemoveLayer(DrawingCanvas.CurrentLayerIndex);
            RefreshLayerList();
            InvalidateThumbnailCache();
            if (ThumbnailPanel.IsVisible)
            {
                RefreshThumbnailList();
            }
            QueueInkSave();
        }
    }

    private void RefreshLayerList()
    {
        LayerList.Children.Clear();
        if (!IsEditorInitialized)
            return;

        for (int i = 0; i < DrawingCanvas.Layers.Count; i++)
        {
            var layer = DrawingCanvas.Layers[i];
            var isSelected = i == DrawingCanvas.CurrentLayerIndex;
            var layerIndex = i;
            var palette = Palette;

            var bgColor = isSelected
                ? palette.LayerSelectedBackground
                : Colors.Transparent;

            var layerItem = new Border
            {
                BackgroundColor = bgColor,
                Padding = new Thickness(10, 8),
                Stroke = isSelected
                    ? palette.LayerSelectedBorder
                    : palette.LayerNormalBorder,
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }
            };

            var stack = new HorizontalStackLayout
            {
                Spacing = 8
            };

            var visibilityIcon = new ImageButton
            {
                Source = layer.IsVisible ? "icon_eye.png" : "icon_eye_off.png",
                WidthRequest = 14,
                HeightRequest = 14,
                Padding = 2,
                CornerRadius = 9,
                BackgroundColor = Colors.Transparent,
                Command = new Command(() =>
                {
                    layer.IsVisible = !layer.IsVisible;
                    DrawingCanvas.InvalidateSurface();
                    RefreshLayerList();
                    InvalidateThumbnailCache();
                    if (ThumbnailPanel.IsVisible)
                    {
                        RefreshThumbnailList();
                    }
                    QueueInkSave();
                })
            };
            RegisterMicroInteraction(visibilityIcon);

            var label = new Label
            {
                Text = layer.Name,
                VerticalOptions = LayoutOptions.Center,
                FontFamily = "OpenSansSemibold",
                FontSize = 13,
                TextColor = ThemePrimaryText
            };

            stack.Children.Add(visibilityIcon);
            stack.Children.Add(label);

            layerItem.Content = stack;
            layerItem.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    DrawingCanvas.CurrentLayerIndex = layerIndex;
                    RefreshLayerList();
                })
            });

            LayerList.Children.Add(layerItem);
        }
    }

    private void OnFingerDrawToggled(object? sender, ToggledEventArgs e)
    {
        if (_isUpdatingFingerDrawSwitch)
            return;

        ApplyInputMode(
            e.Value ? DrawingInputMode.FingerCapacitive : DrawingInputMode.PenStylus,
            showStatus: IsEditorInitialized,
            activateDrawing: false);
    }

    private void OnTextToolClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("StatusTextToolSoon", "Text tool is coming soon."));
    }

    private void OnImageToolClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("StatusImageToolSoon", "Image insert is coming soon."));
    }

    private void OnShapeToolClicked(object? sender, EventArgs e)
    {
        ShowStatus(T("StatusShapeToolSoon", "Shape tool is coming soon."));
    }

    private void OnDrawingCanvasTwoFingerSwipe(object? sender, DrawingCanvas.TwoFingerSwipeEventArgs e)
    {
        if (!EnsurePdfLoaded())
            return;

        if (PdfViewer.DisplayMode != Flow.PDFView.Abstractions.PdfDisplayMode.SinglePage)
            return;

        LogInputGesture($"two-finger-swipe direction={e.Direction} page={_currentPageIndex + 1}/{Math.Max(1, _totalPageCount)}");

        if (e.Direction == DrawingCanvas.TwoFingerSwipeDirection.NextPage)
        {
            if (_currentPageIndex + 1 < _totalPageCount)
            {
                PdfViewer.GoToPage(_currentPageIndex + 1);
            }
            return;
        }

        if (_currentPageIndex > 0)
        {
            PdfViewer.GoToPage(_currentPageIndex - 1);
        }
    }

    private void OnDrawingCanvasTwoFingerPan(object? sender, DrawingCanvas.TwoFingerPanEventArgs e)
    {
        if (!EnsurePdfLoaded())
            return;

        if (_drawingInputMode == DrawingInputMode.TapRead)
            return;

        if (e.Phase == DrawingCanvas.TwoFingerPanPhase.Begin)
        {
            LogInputGesture($"pan-begin wheel={e.IsWheelInput} mode={_drawingInputMode}");
        }

        if (e.Phase == DrawingCanvas.TwoFingerPanPhase.End)
        {
            LogInputGesture($"pan-end wheel={e.IsWheelInput}");
            return;
        }

        if (_drawingInputMode == DrawingInputMode.PenStylus && e.HasZoom && !e.IsWheelInput)
        {
            PdfViewer.ZoomBy(e.ScaleFactor, e.CenterX, e.CenterY);
        }

        if (!e.HasPan)
            return;

        var adjustedX = e.DeltaX;
        var adjustedY = e.DeltaY;
        PdfViewer.PanBy(adjustedX, adjustedY);

        var now = DateTime.UtcNow;
        var shouldLogPanUpdate = (now - _lastPanUpdateLogUtc).TotalMilliseconds >= 90
            || Math.Abs(adjustedX) >= 90
            || Math.Abs(adjustedY) >= 90
            || e.HasZoom;
        if (shouldLogPanUpdate && (Math.Abs(adjustedX) > 0.1 || Math.Abs(adjustedY) > 0.1 || e.HasZoom))
        {
            _lastPanUpdateLogUtc = now;
            LogInputGesture($"pan-update wheel={e.IsWheelInput} dx={adjustedX:0.0} dy={adjustedY:0.0} zoom={e.ScaleFactor:0.000}");
        }
    }

    private static void StopTwoFingerInertia() { }

    [Conditional("DEBUG")]
    private static void LogInputGesture(string message)
    {
        Debug.WriteLine($"[FlowNote Gesture] {message}");
    }

    private void OnDrawingStrokeStarted(object? sender, EventArgs e)
    {
        StopTwoFingerInertia();
    }

    private async void OnDrawingStrokeFinalized(object? sender, DrawingCanvas.StrokeFinalizedEventArgs e)
    {
        if (!IsEditorInitialized || e.Stroke.Points.Count == 0)
            return;

        var intersectsAnyPage = await DoesStrokeIntersectAnyPageAsync(e.Stroke);
        if (intersectsAnyPage)
            return;

        if (!DrawingCanvas.RemoveStroke(e.Stroke))
            return;

        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
        QueueInkSave();
    }

    private void OnDrawingStrokeCommitted(object? sender, EventArgs e)
    {
        InvalidateThumbnailCache();
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
        QueueInkSave();
    }

    private void QueueInkSave()
    {
        if (string.IsNullOrWhiteSpace(_currentNoteId))
            return;

        _inkSaveDebounce?.Cancel();
        _inkSaveDebounce?.Dispose();
        _inkSaveDebounce = new CancellationTokenSource();
        var token = _inkSaveDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(450, token);
                if (token.IsCancellationRequested)
                    return;

                await SaveCurrentDrawingStateAsync();
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task SaveCurrentDrawingStateAsync()
    {
        if (!IsEditorInitialized || string.IsNullOrWhiteSpace(_currentNoteId))
            return;

        try
        {
            var state = DrawingCanvas.ExportState();
            await _drawingPersistenceService.SaveAsync(_currentNoteId, state);
        }
        catch
        {
        }
    }

    private async Task LoadDrawingForCurrentNoteAsync()
    {
        if (!EnsureDrawingReady())
            return;

        if (string.IsNullOrWhiteSpace(_currentNoteId))
        {
            DrawingCanvas.ImportState(null);
            RefreshLayerList();
            return;
        }

        var state = await _drawingPersistenceService.LoadAsync(_currentNoteId);
        DrawingCanvas.ImportState(state);
        RefreshLayerList();
    }
}
