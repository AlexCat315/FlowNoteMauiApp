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
        button.BorderWidth = selected ? 1 : 0;
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
        if (state.Color == color)
        {
            UpdateColorSelection(GetColorKey(color));
            return;
        }

        state.Color = color;
        if (IsEditorInitialized)
        {
            DrawingCanvas.StrokeColor = color;
        }

        UpdateColorSelection(GetColorKey(color));
        UpdateToolButtonTintColors(_activeInkTool);
    }

    private void UpdateToolButtonTintColors()
    {
        UpdateToolButtonTintColors(
            InkToolKind.Ballpoint,
            InkToolKind.Fountain,
            InkToolKind.Pencil,
            InkToolKind.Marker);
    }

    private void UpdateToolButtonTintColors(params InkToolKind[] tools)
    {
        if (tools.Length == 0)
            return;

        _toolTintUpdateCts?.Cancel();
        _toolTintUpdateCts?.Dispose();
        _toolTintUpdateCts = new CancellationTokenSource();
        var token = _toolTintUpdateCts.Token;
        _ = UpdateToolButtonTintColorsAsync(tools, token);
    }

    private async Task UpdateToolButtonTintColorsAsync(IReadOnlyList<InkToolKind> tools, CancellationToken token)
    {
        try
        {
            var renderTasks = new List<Task<(InkToolKind Tool, ImageSource Source)>>(tools.Count);
            foreach (var tool in tools)
            {
                if (!TryGetProceduralToolKey(tool, out var toolKey))
                    continue;

                var tintColor = EnsureInkState(tool).Color;
                renderTasks.Add(RenderToolIconAsync(tool, toolKey, tintColor, token));
            }

            if (renderTasks.Count == 0)
                return;

            var updates = await Task.WhenAll(renderTasks).ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (token.IsCancellationRequested)
                    return;

                foreach (var update in updates)
                {
                    var button = GetToolButton(update.Tool);
                    if (button is not null)
                    {
                        button.Source = update.Source;
                    }
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<(InkToolKind Tool, ImageSource Source)> RenderToolIconAsync(
        InkToolKind tool,
        string toolKey,
        SKColor tintColor,
        CancellationToken token)
    {
        var source = await CreateTintedToolIconSourceAsync(toolKey, tintColor, token).ConfigureAwait(false);
        return (tool, source);
    }

    private static bool TryGetProceduralToolKey(InkToolKind tool, out string toolKey)
    {
        switch (tool)
        {
            case InkToolKind.Ballpoint:
                toolKey = "ballpoint";
                return true;
            case InkToolKind.Fountain:
                toolKey = "fountain";
                return true;
            case InkToolKind.Pencil:
                toolKey = "pencil";
                return true;
            case InkToolKind.Marker:
                toolKey = "marker";
                return true;
            default:
                toolKey = string.Empty;
                return false;
        }
    }

    private ImageButton? GetToolButton(InkToolKind tool)
    {
        return tool switch
        {
            InkToolKind.Ballpoint => PenModeButton,
            InkToolKind.Fountain => HighlighterButton,
            InkToolKind.Pencil => PencilButton,
            InkToolKind.Marker => MarkerButton,
            _ => null
        };
    }

    private async Task<ImageSource> CreateTintedToolIconSourceAsync(string toolKey, SKColor tintColor, CancellationToken token)
    {
        return await IconRenderHelper
            .CreateProcedural3DToolIconAsync(toolKey, tintColor, token)
            .ConfigureAwait(false);
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

}
