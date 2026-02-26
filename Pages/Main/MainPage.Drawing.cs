using SkiaSharp;
using FlowNoteMauiApp.Controls;
using System.Diagnostics;
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
        Pen,
        Highlighter,
        Eraser
    }

    private const double PanInertiaFrameIntervalMs = 20d;
    private const double PanInertiaVelocityStopThreshold = 8d;
    private const double PanInertiaStartSpeedThreshold = 34d;
    private const double PanInertiaMaxStartSpeed = 6200d;
    private const string PenInputModeIcon = "icon_pencil.png";
    private const string FingerInputModeIcon = "icon_hand.png";
    private const string ReadInputModeIcon = "icon_read_mode.png";
    private CancellationTokenSource? _panInertiaCts;
    private DateTime _lastPanSampleUtc = DateTime.UtcNow;
    private DateTime _lastPanUpdateLogUtc = DateTime.MinValue;
    private double _panVelocityX;
    private double _panVelocityY;
    private bool _panInertiaFrameQueued;
    private InkToolKind _activeInkTool = InkToolKind.None;

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
        if (!EnsureDrawingReady())
            return;

        StopTwoFingerInertia();
        SetInputModePanelVisible(false);
        ApplyInputMode(DrawingInputMode.TapRead, showStatus: true, activateDrawing: false);
    }

    private void OnPenModeClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        SetInputModePanelVisible(!InputModePanel.IsVisible);
    }

    private void SetInputModePanelVisible(bool visible)
    {
        InputModePanel.IsVisible = visible;
        UpdateModeButtonVisual();
    }

    private void UpdateModeButtonVisual()
    {
        var palette = Palette;
        var isExpanded = InputModePanel.IsVisible;
        PenModeButton.BackgroundColor = isExpanded
            ? palette.ModeButtonExpandedBackground
            : palette.ModeButtonCollapsedBackground;
        PenModeButton.BorderColor = isExpanded
            ? palette.ModeButtonExpandedBorder
            : palette.ModeButtonCollapsedBorder;
        PenModeButton.BorderWidth = 1;
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

        PenModeButton.Source = _drawingInputMode switch
        {
            DrawingInputMode.FingerCapacitive => FingerInputModeIcon,
            DrawingInputMode.TapRead => ReadInputModeIcon,
            _ => PenInputModeIcon
        };
        UpdateModeButtonVisual();
    }

    private void UpdateInputModeButtonVisual(Button button, bool isSelected)
    {
        var palette = Palette;
        button.BackgroundColor = isSelected
            ? palette.ModeSelectionBackground
            : Colors.Transparent;
        button.TextColor = isSelected
            ? palette.ModeSelectionText
            : ThemePrimaryText;
    }

    private static bool IsNativeGesturePassthroughPlatform()
    {
        return DeviceInfo.Platform == DevicePlatform.MacCatalyst
            || DeviceInfo.Platform == DevicePlatform.iOS;
    }

    private void ApplyActiveInkToolVisual()
    {
        switch (_activeInkTool)
        {
            case InkToolKind.Highlighter:
                DrawingCanvas.IsErasing = false;
                DrawingCanvas.IsHighlighter = true;
                UpdateToolSelection("Highlighter");
                break;
            case InkToolKind.Eraser:
                DrawingCanvas.IsErasing = true;
                DrawingCanvas.IsHighlighter = false;
                UpdateToolSelection("Eraser");
                break;
            case InkToolKind.Pen:
                DrawingCanvas.IsErasing = false;
                DrawingCanvas.IsHighlighter = false;
                UpdateToolSelection("Pen");
                break;
            default:
                DrawingCanvas.IsErasing = false;
                DrawingCanvas.IsHighlighter = false;
                UpdateToolSelection("None");
                break;
        }
    }

    private void ArmInkTool(InkToolKind tool, bool showStatus = false)
    {
        _activeInkTool = tool;
        if (!IsEditorInitialized)
            return;

        ApplyActiveInkToolVisual();
        var forceNativeInputPassthrough = IsNativeGesturePassthroughPlatform();
        if (!forceNativeInputPassthrough && _drawingInputMode != DrawingInputMode.TapRead)
        {
            DrawingCanvas.EnableDrawing = true;
            DrawingToolbarPanel.IsVisible = true;
        }

        if (showStatus)
        {
            ShowStatus(tool switch
            {
                InkToolKind.Highlighter => T("StatusHighlighterArmed", "Highlighter selected."),
                InkToolKind.Eraser => T("StatusEraserArmed", "Eraser selected."),
                _ => T("StatusPenArmed", "Pen selected.")
            });
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
                DrawingToolbarPanel.IsVisible = targetDrawingEnabled;
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
                DrawingToolbarPanel.IsVisible = targetDrawingEnabled;
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
                LayerPanel.IsVisible = false;
                if (wasDrawingEnabled)
                    QueueInkSave();
                UpdateToolSelection("Read");
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
        var nextTool = _activeInkTool == InkToolKind.Highlighter
            ? InkToolKind.Pen
            : InkToolKind.Highlighter;
        ArmInkTool(nextTool);
    }

    private void OnEraserClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        SetInputModePanelVisible(false);
        var nextTool = _activeInkTool == InkToolKind.Eraser
            ? InkToolKind.Pen
            : InkToolKind.Eraser;
        ArmInkTool(nextTool);
    }

    private void UpdateToolSelection(string selectedTool)
    {
        var palette = Palette;
        var selectedColor = palette.ToolSelectedBackground;
        var normalColor = palette.ToolNormalBackground;
        var selectedBorder = palette.ToolSelectedBorder;
        var normalBorder = palette.ToolNormalBorder;

        HighlighterButton.BackgroundColor = selectedTool == "Highlighter" ? selectedColor : normalColor;
        EraserButton.BackgroundColor = selectedTool == "Eraser" ? selectedColor : normalColor;

        HighlighterButton.BorderColor = selectedTool == "Highlighter" ? selectedBorder : normalBorder;
        EraserButton.BorderColor = selectedTool == "Eraser" ? selectedBorder : normalBorder;
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
        ArmInkTool(InkToolKind.Pen);
    }

    private void OnColorRedClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.Red;
        UpdateColorSelection("Red");
        ArmInkTool(InkToolKind.Pen);
    }

    private void OnColorBlueClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.Blue;
        UpdateColorSelection("Blue");
        ArmInkTool(InkToolKind.Pen);
    }

    private void OnColorGreenClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.Green;
        UpdateColorSelection("Green");
        ArmInkTool(InkToolKind.Pen);
    }

    private void OnColorOrangeClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.Orange;
        UpdateColorSelection("Orange");
        ArmInkTool(InkToolKind.Pen);
    }

    private void OnColorWhiteClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.StrokeColor = SKColors.White;
        UpdateColorSelection("White");
        ArmInkTool(InkToolKind.Pen);
    }

    private void UpdateColorSelection(string selectedColor)
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
                WidthRequest = 24,
                HeightRequest = 24,
                Padding = 5,
                CornerRadius = 12,
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

        var allowInertia = !e.IsWheelInput;

        if (e.Phase == DrawingCanvas.TwoFingerPanPhase.Begin)
        {
            StopTwoFingerInertia();
            _panVelocityX = 0d;
            _panVelocityY = 0d;
            _lastPanSampleUtc = DateTime.UtcNow;
            LogInputGesture($"pan-begin wheel={e.IsWheelInput} mode={_drawingInputMode}");
        }

        if (e.Phase == DrawingCanvas.TwoFingerPanPhase.End)
        {
            var releaseVelocityX = _panVelocityX;
            var releaseVelocityY = _panVelocityY;
            if (allowInertia)
            {
                StartTwoFingerInertiaIfNeeded();
            }

            LogInputGesture($"pan-end wheel={e.IsWheelInput} velocity=({releaseVelocityX:0.0},{releaseVelocityY:0.0})");
            return;
        }

        if (e.HasZoom && !e.IsWheelInput)
        {
            PdfViewer.ZoomBy(e.ScaleFactor, e.CenterX, e.CenterY);
        }

        if (!e.HasPan)
            return;

        var adjustedX = ApplyPanResistance(e.DeltaX);
        var adjustedY = ApplyPanResistance(e.DeltaY);
        PdfViewer.PanBy(adjustedX, adjustedY);

        if (allowInertia)
        {
            UpdatePanVelocity(adjustedX, adjustedY);
        }

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

    private double ApplyPanResistance(double delta)
    {
        if (!_pageFreeMoveEnabled)
            return delta;

        var normalizedResistance = Math.Clamp(_pageMoveResistancePercent / 100d, 0d, 1d);
        var factor = Math.Clamp(1d - (normalizedResistance * 0.62d), 0.28d, 1d);
        return delta * factor;
    }

    private void UpdatePanVelocity(double deltaX, double deltaY)
    {
        var now = DateTime.UtcNow;
        var seconds = Math.Max(0.001d, (now - _lastPanSampleUtc).TotalSeconds);
        _lastPanSampleUtc = now;

        var sampleVx = deltaX / seconds;
        var sampleVy = deltaY / seconds;
        const double smoothing = 0.24d;
        _panVelocityX = (_panVelocityX * (1d - smoothing)) + (sampleVx * smoothing);
        _panVelocityY = (_panVelocityY * (1d - smoothing)) + (sampleVy * smoothing);
    }

    private void StartTwoFingerInertiaIfNeeded()
    {
        if (!_pageFreeMoveEnabled)
        {
            _panVelocityX = 0d;
            _panVelocityY = 0d;
            return;
        }

        var initialVelocityX = _panVelocityX;
        var initialVelocityY = _panVelocityY;
        var speed = Math.Sqrt((initialVelocityX * initialVelocityX) + (initialVelocityY * initialVelocityY));
        if (speed < PanInertiaStartSpeedThreshold)
        {
            _panVelocityX = 0d;
            _panVelocityY = 0d;
            return;
        }

        if (speed > PanInertiaMaxStartSpeed)
        {
            var normalize = PanInertiaMaxStartSpeed / speed;
            initialVelocityX *= normalize;
            initialVelocityY *= normalize;
            speed = PanInertiaMaxStartSpeed;
        }

        StopTwoFingerInertia(resetVelocity: false);
        _panInertiaCts = new CancellationTokenSource();
        var token = _panInertiaCts.Token;
        var resistance = Math.Clamp(_pageMoveResistancePercent / 100d, 0d, 1d);
        var damping = Math.Clamp(0.90d - (resistance * 0.16d) - Math.Min(0.04d, speed / 24000d), 0.72d, 0.90d);
        var velocityX = initialVelocityX;
        var velocityY = initialVelocityY;

        _ = Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            var last = stopwatch.Elapsed;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(PanInertiaFrameIntervalMs), token);
                    var current = stopwatch.Elapsed;
                    var deltaSeconds = Math.Clamp((current - last).TotalSeconds, 0.008d, 0.04d);
                    last = current;

                    velocityX *= damping;
                    velocityY *= damping;
                    var currentSpeed = Math.Sqrt((velocityX * velocityX) + (velocityY * velocityY));
                    if (currentSpeed < PanInertiaVelocityStopThreshold)
                        break;

                    var deltaX = velocityX * deltaSeconds;
                    var deltaY = velocityY * deltaSeconds;

                    if (_panInertiaFrameQueued)
                    {
                        continue;
                    }

                    _panInertiaFrameQueued = true;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            if (token.IsCancellationRequested)
                                return;

                            _panVelocityX = velocityX;
                            _panVelocityY = velocityY;
                            if (HasLoadedDocument)
                            {
                                PdfViewer.PanBy(deltaX, deltaY);
                            }
                        }
                        finally
                        {
                            _panInertiaFrameQueued = false;
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _panInertiaFrameQueued = false;
                    _panVelocityX = 0d;
                    _panVelocityY = 0d;
                });
            }
        }, token);

        LogInputGesture($"inertia-start speed={speed:0.0} resistance={resistance:0.00} damping={damping:0.00}");
    }

    private void StopTwoFingerInertia(bool resetVelocity = true)
    {
        _panInertiaCts?.Cancel();
        _panInertiaCts?.Dispose();
        _panInertiaCts = null;
        _panInertiaFrameQueued = false;
        if (resetVelocity)
        {
            _panVelocityX = 0d;
            _panVelocityY = 0d;
        }
    }

    [Conditional("DEBUG")]
    private static void LogInputGesture(string message)
    {
        Debug.WriteLine($"[FlowNote Gesture] {message}");
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
