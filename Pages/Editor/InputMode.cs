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
        var modeIcon = _drawingInputMode switch
        {
            DrawingInputMode.FingerCapacitive => "icon_hand_mode.svg",
            DrawingInputMode.TapRead => "icon_read_mode.svg",
            _ => "icon_stylus_mode.svg"
        };

        var iconTint = IsDarkTheme ? Colors.White : Colors.Black;
        _ = TopModePenButton.SetIconAsync(modeIcon, iconTint, IconTintMode.Monochrome);
    }

    private void ApplyImageButtonIconSizing()
    {
        HomeSortMenuButton.SetIconDrawSize(14);
        TopImportButton.SetIconDrawSize(14);
        TopSearchButton.SetIconDrawSize(14);
        TopModePenButton.SetIconDrawSize(15);
        TopSettingsButton.SetIconDrawSize(14);
        TopThumbnailButton.SetIconDrawSize(14);

        UndoButton.SetIconDrawSize(15);
        RedoButton.SetIconDrawSize(15);
        PenModeButton.SetIconDrawSize(27);
        HighlighterButton.SetIconDrawSize(27);
        PencilButton.SetIconDrawSize(30);
        MarkerButton.SetIconDrawSize(30);
        EraserButton.SetIconDrawSize(30);
        ClearButton2.SetIconDrawSize(19);
        TopInlineLayerButton.SetIconDrawSize(19);
        TextToolButton.SetIconDrawSize(15);
        ImageToolButton.SetIconDrawSize(15);
        ShapeToolButton.SetIconDrawSize(15);
        PrevPageButton.SetIconDrawSize(14);
        NextPageButton.SetIconDrawSize(14);
        AddLayerButton.SetIconDrawSize(15);
        DeleteLayerButton.SetIconDrawSize(15);
        ThumbnailCloseButton.SetIconDrawSize(14);
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

}
