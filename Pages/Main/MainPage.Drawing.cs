using SkiaSharp;

namespace FlowNoteMauiApp;

public partial class MainPage
{
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

        DrawingCanvas.EnableDrawing = !DrawingCanvas.EnableDrawing;
        DrawingCanvas.IsVisible = DrawingCanvas.EnableDrawing;
        DrawingToolbarPanel.IsVisible = DrawingCanvas.EnableDrawing;
        
        if (DrawingCanvas.EnableDrawing)
        {
            UpdateToolSelection("Pen");
            DrawingToggleButton.BackgroundColor = IsDarkTheme ? Color.FromArgb("#324A6B") : Color.FromArgb("#DBE7FF");
        }
        else
        {
            DrawingToggleButton.BackgroundColor = IsDarkTheme ? Color.FromArgb("#26344A") : Color.FromArgb("#F7FAFF");
            QueueInkSave();
        }
    }

    private void OnDrawingToolbarCloseClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.EnableDrawing = false;
        DrawingCanvas.IsVisible = false;
        DrawingToolbarPanel.IsVisible = false;
        LayerPanel.IsVisible = false;
        QueueInkSave();
    }

    private void OnPenModeClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.IsPenMode = !DrawingCanvas.IsPenMode;
        DrawingCanvas.IsErasing = false;
        DrawingCanvas.IsHighlighter = false;
        UpdateToolSelection(DrawingCanvas.IsPenMode ? "Pen" : "Finger");
        EnableFingerDrawSwitch.IsToggled = !DrawingCanvas.IsPenMode;
    }

    private void OnLayerToggleClicked(object? sender, EventArgs e)
    {
        LayerPanel.IsVisible = !LayerPanel.IsVisible;
    }

    private void OnHighlighterClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.IsErasing = false;
        DrawingCanvas.IsHighlighter = !DrawingCanvas.IsHighlighter;
        UpdateToolSelection(DrawingCanvas.IsHighlighter ? "Highlighter" : "Pen");
    }

    private void OnEraserClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.IsErasing = !DrawingCanvas.IsErasing;
        DrawingCanvas.IsHighlighter = false;
        UpdateToolSelection(DrawingCanvas.IsErasing ? "Eraser" : "Pen");
    }

    private void UpdateToolSelection(string selectedTool)
    {
        var selectedColor = IsDarkTheme ? Color.FromArgb("#324A6B") : Color.FromArgb("#DBE7FF");
        var normalColor = IsDarkTheme ? Color.FromArgb("#26344A") : Color.FromArgb("#F7FAFF");
        PenModeButton.BackgroundColor = (selectedTool == "Pen" || selectedTool == "Finger") ? selectedColor : normalColor;
        HighlighterButton.BackgroundColor = selectedTool == "Highlighter" ? selectedColor : normalColor;
        EraserButton.BackgroundColor = selectedTool == "Eraser" ? selectedColor : normalColor;
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
        var selectedBorderColor = Color.FromRgb(37, 99, 235);
        
        ColorBlack.BorderColor = selectedColor == "Black" ? selectedBorderColor : Colors.Transparent;
        ColorRed.BorderColor = selectedColor == "Red" ? selectedBorderColor : Colors.Transparent;
        ColorBlue.BorderColor = selectedColor == "Blue" ? selectedBorderColor : Colors.Transparent;
        ColorGreen.BorderColor = selectedColor == "Green" ? selectedBorderColor : Colors.Transparent;
        ColorOrange.BorderColor = selectedColor == "Orange" ? selectedBorderColor : Colors.Transparent;
        ColorWhite.BorderColor = selectedColor == "White" ? selectedBorderColor : Colors.Transparent;
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
            
            var bgColor = isSelected ? ThemeSelectedBackground : Colors.Transparent;
            
            var layerItem = new Border
            {
                BackgroundColor = bgColor,
                Padding = new Thickness(8, 8),
                Stroke = IsDarkTheme ? Color.FromArgb("#334155") : Color.FromArgb("#E2E8F0"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 }
            };
            
            var stack = new HorizontalStackLayout();
            
            var visibilityIcon = new ImageButton
            {
                Source = layer.IsVisible ? "icon_eye.svg" : "icon_eye_off.svg",
                WidthRequest = 24,
                HeightRequest = 24,
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
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 14,
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
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.IsPenMode = !e.Value;
        UpdateToolSelection(DrawingCanvas.IsPenMode ? "Pen" : "Finger");
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
