using SkiaSharp;
using FlowNoteMauiApp.Controls;
using FlowNoteMauiApp.Models;
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
        DrawingToolbarPanel.IsVisible = false;
    }

    private void OnPenModeClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        if (_activeInkTool == InkToolKind.Ballpoint && DrawingToolbarPanel.IsVisible)
        {
            DrawingToolbarPanel.IsVisible = false;
            return;
        }

        ArmInkTool(InkToolKind.Ballpoint);
    }

    private void SetInputModePanelVisible(bool visible)
    {
        InputModePanel.IsVisible = visible;
        UpdateModeButtonVisual();
    }

    private void UpdateModeButtonVisual()
    {
        ApplyTopModeButtonVisual();
    }

    private void ApplyTopModeButtonVisual()
    {
        ApplyTopModeButtonVisual(TopModePenButton, _drawingInputMode == DrawingInputMode.PenStylus);
        ApplyTopModeButtonVisual(TopModeFingerButton, _drawingInputMode == DrawingInputMode.FingerCapacitive);
        ApplyTopModeButtonVisual(TopModeReadButton, _drawingInputMode == DrawingInputMode.TapRead);
    }

    private void ApplyTopModeButtonVisual(ImageButton button, bool selected)
    {
        var palette = Palette;
        button.BackgroundColor = selected
            ? palette.ModeButtonExpandedBackground
            : palette.ModeButtonCollapsedBackground;
        button.BorderColor = selected
            ? palette.ModeButtonExpandedBorder
            : palette.ModeButtonCollapsedBorder;
        button.BorderWidth = 1;
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

    private static string GetInkToolTitle(InkToolKind tool)
    {
        return tool switch
        {
            InkToolKind.Ballpoint => "圆珠笔",
            InkToolKind.Fountain => "钢笔",
            InkToolKind.Pencil => "铅笔",
            InkToolKind.Marker => "马克笔",
            InkToolKind.Eraser => "橡皮擦",
            _ => "工具"
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
            UpdateToolButtonTintColors();
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
            DrawingCanvas.StrokeWidth = state.Width;
            if (!isEraser)
            {
                DrawingCanvas.StrokeColor = state.Color;
            }
        }

        UpdateToolSelection(_activeInkTool);
        UpdateToolButtonTintColors();
        UpdateToolSettingsPanelState();
    }

    private void ArmInkTool(InkToolKind tool, bool showStatus = false)
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
        DrawingToolbarPanel.IsVisible = _drawingInputMode != DrawingInputMode.TapRead;

        if (showStatus)
        {
            ShowStatus(_activeInkTool switch
            {
                InkToolKind.Ballpoint => "圆珠笔已选中",
                InkToolKind.Fountain => "钢笔已选中",
                InkToolKind.Pencil => "铅笔已选中",
                InkToolKind.Marker => "马克笔已选中",
                InkToolKind.Eraser => "橡皮擦已选中",
                _ => T("StatusPenArmed", "Pen selected.")
            });
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

    private void OnTopModePenClicked(object? sender, EventArgs e)
    {
        ApplyInputMode(DrawingInputMode.PenStylus, showStatus: true);
        SetInputModePanelVisible(false);
    }

    private void OnTopModeFingerClicked(object? sender, EventArgs e)
    {
        ApplyInputMode(DrawingInputMode.FingerCapacitive, showStatus: true);
        SetInputModePanelVisible(false);
    }

    private void OnTopModeReadClicked(object? sender, EventArgs e)
    {
        ApplyInputMode(DrawingInputMode.TapRead, showStatus: true);
        SetInputModePanelVisible(false);
    }

    private void OnLayerToggleClicked(object? sender, EventArgs e)
    {
        SetInputModePanelVisible(false);
        ThumbnailPanel.IsVisible = false;
        LayerPanel.IsVisible = !LayerPanel.IsVisible;
    }

    private void OnThumbnailToggleClicked(object? sender, EventArgs e)
    {
        if (!EnsurePdfLoaded(showHint: true))
            return;

        SetInputModePanelVisible(false);
        LayerPanel.IsVisible = false;
        ThumbnailPanel.IsVisible = !ThumbnailPanel.IsVisible;
        if (ThumbnailPanel.IsVisible)
        {
            RefreshThumbnailList();
        }
    }

    private void OnHighlighterClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        ArmInkTool(InkToolKind.Fountain);
    }

    private void OnPencilClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        ArmInkTool(InkToolKind.Pencil);
    }

    private void OnMarkerClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        ArmInkTool(InkToolKind.Marker);
    }

    private void OnEraserClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        ArmInkTool(InkToolKind.Eraser);
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

    private void OnClearClicked(object? sender, EventArgs e)
    {
        if (!EnsureDrawingReady())
            return;

        DrawingCanvas.ClearCurrentLayer();
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
        PenModeButton.Source = CreateTintedToolIconSource("icon_pen.png", EnsureInkState(InkToolKind.Ballpoint).Color);
        HighlighterButton.Source = CreateTintedToolIconSource("icon_gelpen.png", EnsureInkState(InkToolKind.Fountain).Color);
        PencilButton.Source = CreateTintedToolIconSource("icon_pencil.png", EnsureInkState(InkToolKind.Pencil).Color);
        MarkerButton.Source = CreateTintedToolIconSource("icon_brush.png", EnsureInkState(InkToolKind.Marker).Color);
        EraserButton.Source = ImageSource.FromFile("icon_eraser.png");
    }

    private ImageSource CreateTintedToolIconSource(string iconFile, SKColor tintColor)
    {
        var cacheKey = $"{iconFile}:{tintColor.Alpha:X2}{tintColor.Red:X2}{tintColor.Green:X2}{tintColor.Blue:X2}";
        if (_toolIconSourceCache.TryGetValue(cacheKey, out var cachedSource))
            return cachedSource;

        try
        {
            using var rawStream = FileSystem.OpenAppPackageFileAsync(iconFile).GetAwaiter().GetResult();
            using var skData = SKData.Create(rawStream);
            using var baseBitmap = SKBitmap.Decode(skData);
            if (baseBitmap is null)
                return ImageSource.FromFile(iconFile);

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

            using var outputImage = SKImage.FromBitmap(tintedBitmap);
            using var outputData = outputImage.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = outputData.ToArray();
            var source = ImageSource.FromStream(() => new MemoryStream(bytes));
            _toolIconSourceCache[cacheKey] = source;
            return source;
        }
        catch
        {
            return ImageSource.FromFile(iconFile);
        }
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
        }
        finally
        {
            _isUpdatingToolUi = false;
        }

        ToolSettingsTitleLabel.Text = GetInkToolTitle(_activeInkTool);
        DrawingPenWidthLabel.Text = _activeInkTool == InkToolKind.Eraser ? "大小" : "粗细";
        StrokeWidthLabel.Text = $"{state.Width:0.00}mm";
        EraserModePanel.IsVisible = _activeInkTool == InkToolKind.Eraser;
        ToolColorPanel.IsVisible = _activeInkTool != InkToolKind.Eraser;
        UpdateEraserModeSelectionVisual();
        UpdateColorSelection(_activeInkTool == InkToolKind.Eraser ? null : GetColorKey(state.Color));
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

    private void RefreshThumbnailList()
    {
        ThumbnailList.Children.Clear();
        if (!IsEditorInitialized || _totalPageCount <= 0)
            return;

        for (int i = 0; i < _totalPageCount; i++)
        {
            var pageIndex = i;
            var isCurrent = pageIndex == _currentPageIndex;
            var palette = Palette;

            var item = new Border
            {
                BackgroundColor = isCurrent ? palette.LayerSelectedBackground : Colors.Transparent,
                Stroke = isCurrent ? palette.LayerSelectedBorder : palette.LayerNormalBorder,
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(8),
                Content = new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        new Border
                        {
                            HeightRequest = 72,
                            Stroke = isCurrent ? palette.ModeButtonExpandedBorder : palette.LayerNormalBorder,
                            StrokeThickness = 1,
                            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                            BackgroundColor = isCurrent ? palette.ModeButtonExpandedBackground : palette.ModeButtonCollapsedBackground
                        },
                        new Label
                        {
                            Text = $"Page {pageIndex + 1}",
                            FontSize = 12,
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
            ThumbnailList.Children.Add(item);
        }
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
