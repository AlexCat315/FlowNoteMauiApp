using SkiaSharp;
using FlowNoteMauiApp.Controls;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private enum DrawingInputMode
    {
        PenStylus,
        FingerCapacitive,
        TapRead
    }

    private bool EnsureDrawingReady(bool showHint = false)
    {
        if (IsEditorInitialized)
            return true;

        if (showHint)
            ShowStatus("Open a PDF first.");
        return false;
    }

    // Drawing related methods
    private void OnDrawingToggleClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady(showHint: true))
            return;

        SetInputModePanelVisible(false);

        if (_drawingInputMode == DrawingInputMode.TapRead)
        {
            ApplyInputMode(DrawingInputMode.PenStylus, showStatus: true);
            return;
        }

        ApplyInputMode(DrawingInputMode.TapRead, showStatus: true, activateDrawing: false);
    }

    private void OnDrawingToolbarCloseClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        SetInputModePanelVisible(false);
        DrawingCanvas.EnableDrawing = false;
        DrawingCanvas.IsVisible = false;
        DrawingToolbarPanel.IsVisible = false;
        LayerPanel.IsVisible = false;
        UpdateDrawingToggleVisual(false);
        QueueInkSave();
    }

    private void OnPenModeClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        if (_drawingInputMode != DrawingInputMode.PenStylus)
        {
            ApplyInputMode(DrawingInputMode.PenStylus, showStatus: true);
            SetInputModePanelVisible(false);
            return;
        }

        SetInputModePanelVisible(!InputModePanel.IsVisible);
    }

    private void SetInputModePanelVisible(bool visible)
    {
        InputModePanel.IsVisible = visible;
    }

    private void UpdateDrawingToggleVisual(bool isEnabled)
    {
        DrawingToggleButton.BackgroundColor = isEnabled
            ? (IsDarkTheme ? Color.FromArgb("#33527A") : Color.FromArgb("#E8F4FD"))
            : (IsDarkTheme ? Color.FromArgb("#2B3D57") : Color.FromArgb("#FFFFFF"));
        DrawingToggleButton.BorderColor = isEnabled
            ? Color.FromArgb("#4A90E2")
            : (IsDarkTheme ? Color.FromArgb("#4A607C") : Color.FromArgb("#D1D1D6"));
        DrawingToggleButton.BorderWidth = 1;
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
        InputModePenCheck.IsVisible = _drawingInputMode == DrawingInputMode.PenStylus;
        InputModeFingerCheck.IsVisible = _drawingInputMode == DrawingInputMode.FingerCapacitive;
        InputModeReadCheck.IsVisible = _drawingInputMode == DrawingInputMode.TapRead;
    }

    private void ApplyInputMode(DrawingInputMode mode, bool showStatus = false, bool activateDrawing = true)
    {
        _drawingInputMode = mode;
        UpdateInputModeSelectionVisual();
        SetFingerDrawSwitchState(mode == DrawingInputMode.FingerCapacitive);

        if (!IsEditorInitialized)
            return;

        var wasDrawingEnabled = DrawingCanvas.EnableDrawing;
        var targetDrawingEnabled = mode != DrawingInputMode.TapRead && (activateDrawing || DrawingCanvas.EnableDrawing);
        DrawingCanvas.IsErasing = false;
        DrawingCanvas.IsHighlighter = false;

        switch (mode)
        {
            case DrawingInputMode.PenStylus:
                DrawingCanvas.IsPenMode = true;
                DrawingCanvas.EnableDrawing = targetDrawingEnabled;
                DrawingCanvas.IsVisible = targetDrawingEnabled;
                DrawingToolbarPanel.IsVisible = targetDrawingEnabled;
                UpdateToolSelection("Pen");
                if (showStatus)
                    ShowStatus("已切换到手写笔模式");
                break;
            case DrawingInputMode.FingerCapacitive:
                DrawingCanvas.IsPenMode = false;
                DrawingCanvas.EnableDrawing = targetDrawingEnabled;
                DrawingCanvas.IsVisible = targetDrawingEnabled;
                DrawingToolbarPanel.IsVisible = targetDrawingEnabled;
                UpdateToolSelection("Finger");
                if (showStatus)
                    ShowStatus("手指/电容笔模式：单指书写，双指滚动；仅单页模式双指可翻页");
                break;
            case DrawingInputMode.TapRead:
                DrawingCanvas.EnableDrawing = false;
                DrawingCanvas.IsVisible = false;
                DrawingToolbarPanel.IsVisible = false;
                LayerPanel.IsVisible = false;
                if (wasDrawingEnabled)
                    QueueInkSave();
                UpdateToolSelection("Read");
                if (showStatus)
                    ShowStatus("已切换到点读模式");
                break;
        }

        UpdateDrawingToggleVisual(DrawingCanvas.EnableDrawing);
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

    private void OnLayerToggleClicked(object? sender, EventArgs e)
    {
        SetInputModePanelVisible(false);
        LayerPanel.IsVisible = !LayerPanel.IsVisible;
    }

    private void OnHighlighterClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        SetInputModePanelVisible(false);
        DrawingCanvas.IsErasing = false;
        DrawingCanvas.IsHighlighter = !DrawingCanvas.IsHighlighter;
        UpdateToolSelection(DrawingCanvas.IsHighlighter ? "Highlighter" : "Pen");
    }

    private void OnEraserClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        SetInputModePanelVisible(false);
        DrawingCanvas.IsErasing = !DrawingCanvas.IsErasing;
        DrawingCanvas.IsHighlighter = false;
        UpdateToolSelection(DrawingCanvas.IsErasing ? "Eraser" : "Pen");
    }

    private void UpdateToolSelection(string selectedTool)
    {
        var selectedColor = IsDarkTheme ? Color.FromArgb("#33527A") : Color.FromArgb("#E8F4FD");
        var normalColor = IsDarkTheme ? Color.FromArgb("#2B3D57") : Color.FromArgb("#FFFFFF");
        var selectedBorder = Color.FromArgb("#4A90E2");
        var normalBorder = IsDarkTheme ? Color.FromArgb("#4A607C") : Color.FromArgb("#D1D1D6");
        var isPenSelected = (selectedTool == "Pen" || selectedTool == "Finger") && _drawingInputMode != DrawingInputMode.TapRead;

        PenModeButton.BackgroundColor = isPenSelected ? selectedColor : normalColor;
        HighlighterButton.BackgroundColor = selectedTool == "Highlighter" ? selectedColor : normalColor;
        EraserButton.BackgroundColor = selectedTool == "Eraser" ? selectedColor : normalColor;

        PenModeButton.BorderColor = isPenSelected ? selectedBorder : normalBorder;
        HighlighterButton.BorderColor = selectedTool == "Highlighter" ? selectedBorder : normalBorder;
        EraserButton.BorderColor = selectedTool == "Eraser" ? selectedBorder : normalBorder;
        PenModeButton.BorderWidth = 1;
        HighlighterButton.BorderWidth = 1;
        EraserButton.BorderWidth = 1;
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

    private void OnClearClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.ClearCurrentLayer();
        QueueInkSave();
    }

    private void OnStrokeWidthChanged(object? sender, ValueChangedEventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeWidth = (float)e.NewValue;
        StrokeWidthLabel.Text = $"{(int)e.NewValue}";
    }

    private void OnColorBlackClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.Black;
        UpdateColorSelection("Black");
    }

    private void OnColorRedClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.Red;
        UpdateColorSelection("Red");
    }

    private void OnColorBlueClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.Blue;
        UpdateColorSelection("Blue");
    }

    private void OnColorGreenClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.Green;
        UpdateColorSelection("Green");
    }

    private void OnColorOrangeClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.Orange;
        UpdateColorSelection("Orange");
    }

    private void OnColorWhiteClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.White;
        UpdateColorSelection("White");
    }

    private void UpdateColorSelection(string selectedColor)
    {
        var selectedBorderColor = Color.FromArgb("#4A90E2");
        var normalBorderColor = IsDarkTheme ? Color.FromArgb("#526883") : Colors.Transparent;
        
        ColorBlack.BorderColor = selectedColor == "Black" ? selectedBorderColor : normalBorderColor;
        ColorRed.BorderColor = selectedColor == "Red" ? selectedBorderColor : normalBorderColor;
        ColorBlue.BorderColor = selectedColor == "Blue" ? selectedBorderColor : normalBorderColor;
        ColorGreen.BorderColor = selectedColor == "Green" ? selectedBorderColor : normalBorderColor;
        ColorOrange.BorderColor = selectedColor == "Orange" ? selectedBorderColor : normalBorderColor;
        ColorWhite.BorderColor = selectedColor == "White" ? selectedBorderColor : Color.FromArgb("#CBD5E1");
    }

    private void OnAddLayerClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.AddLayer();
        RefreshLayerList();
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
            
            var bgColor = isSelected
                ? (IsDarkTheme ? Color.FromArgb("#33527A") : Color.FromArgb("#E8F4FD"))
                : Colors.Transparent;
            
            var layerItem = new Border
            {
                BackgroundColor = bgColor,
                Padding = new Thickness(10, 8),
                Stroke = isSelected
                    ? Color.FromArgb("#4A90E2")
                    : (IsDarkTheme ? Color.FromArgb("#415B79") : Color.FromArgb("#D8E4F5")),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }
            };
            
            var stack = new HorizontalStackLayout
            {
                Spacing = 8
            };
            
            var visibilityIcon = new ImageButton
            {
                Source = layer.IsVisible ? "icon_eye.svg" : "icon_eye_off.svg",
                WidthRequest = 28,
                HeightRequest = 28,
                CornerRadius = 14,
                BackgroundColor = Colors.Transparent,
                Command = new Command(() => 
                {
                    layer.IsVisible = !layer.IsVisible;
                    DrawingCanvas.InvalidateSurface();
                    RefreshLayerList();
                    QueueInkSave();
                })
            };
            
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
        ShowStatus("文本工具即将支持");
    }

    private void OnImageToolClicked(object? sender, EventArgs e)
    {
        ShowStatus("图片插入即将支持");
    }

    private void OnShapeToolClicked(object? sender, EventArgs e)
    {
        ShowStatus("图形工具即将支持");
    }

    private void OnDrawingCanvasTwoFingerSwipe(object? sender, DrawingCanvas.TwoFingerSwipeEventArgs e)
    {
        if (!EnsurePdfLoaded())
            return;

        if (PdfViewer.DisplayMode != Flow.PDFView.Abstractions.PdfDisplayMode.SinglePage)
            return;

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

    private void OnDrawingStrokeCommitted(object? sender, EventArgs e)
    {
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
