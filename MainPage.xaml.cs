using System.Globalization;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Resources;
using PdfPageChangedEventArgs = Flow.PDFView.Abstractions.PageChangedEventArgs;
using SkiaSharp;

namespace FlowNoteMauiApp;

public partial class MainPage : ContentPage
{
    private const string DefaultSampleUrl = "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf";

    private int _currentPageIndex;
    private int _totalPageCount;
    private bool _isInitialLoadDone;
    private int _currentSearchIndex = -1;
    private IReadOnlyList<PdfSearchResult> _searchResults = Array.Empty<PdfSearchResult>();
    private double _lastScrollX;
    private double _lastScrollY;
    private System.Threading.Timer? _scrollSyncTimer;
    private double _lastZoom = 1.0;

    public MainPage()
    {
        InitializeComponent();
        InitializeControls();
        UpdateLocalizedStrings();
        PdfViewer.SearchResultsFound += OnSearchResultsFound;
        PdfViewer.SearchProgress += OnSearchProgress;
        Loaded += OnPageLoaded;
        LanguageManager.LanguageChanged += OnLanguageChanged;
        
        DrawingCanvas.Layers.CollectionChanged += (s, e) => RefreshLayerList();
        
        _scrollSyncTimer = new System.Threading.Timer(SyncScrollPosition, null, 100, 100);
    }

    private void SyncScrollPosition(object? state)
    {
        if (!DrawingCanvas.EnableDrawing || _totalPageCount <= 0)
            return;

        try
        {
            var currentPage = PdfViewer.CurrentPage;
            var zoom = PdfViewer.Zoom;
            
            var pageHeight = 800 * zoom;
            var scrollY = currentPage * pageHeight;
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Math.Abs(DrawingCanvas.ScrollY - scrollY) > 1)
                {
                    DrawingCanvas.ScrollY = scrollY;
                }
            });
        }
        catch { }
    }

    private void OnLanguageChanged()
    {
        UpdateLocalizedStrings();
    }

    private void UpdateLocalizedStrings()
    {
        try
        {
            var isZh = AppResources.Culture?.TwoLetterISOLanguageName == "zh";
            
            StatusLabel.Text = isZh ? "就绪" : "Ready";
            UrlEntry.Placeholder = isZh ? "PDF网址" : "PDF URL";
        }
        catch { }
    }

    private void InitializeControls()
    {
        UrlEntry.Text = DefaultSampleUrl;

        foreach (var item in Enum.GetNames<PdfDisplayMode>())
            DisplayModePicker.Items.Add(item);

        foreach (var item in Enum.GetNames<PdfScrollOrientation>())
            OrientationPicker.Items.Add(item);

        foreach (var item in Enum.GetNames<FitPolicy>())
            FitPolicyPicker.Items.Add(item);

        DisplayModePicker.SelectedIndex = (int)PdfDisplayMode.SinglePageContinuous;
        OrientationPicker.SelectedIndex = (int)PdfScrollOrientation.Vertical;
        FitPolicyPicker.SelectedIndex = (int)FitPolicy.Width;

        PdfViewer.EnableZoom = EnableZoomSwitch.IsToggled;
        PdfViewer.EnableSwipe = EnableSwipeSwitch.IsToggled;
        EnableLinkSwitch.IsToggled = true;
        PdfViewer.EnableLinkNavigation = EnableLinkSwitch.IsToggled;
        PdfViewer.Zoom = (float)ZoomSlider.Value;
        
        UpdatePageIndicators();
        RefreshLayerList();
        UpdateColorSelection("Black");
        UpdateToolSelection("Pen");
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        if (_isInitialLoadDone)
            return;

        _isInitialLoadDone = true;
        await LoadFromUrlAsync(UrlEntry.Text, showAlertOnError: false);
    }

    private async void OnLoadUrlClicked(object? sender, EventArgs e)
    {
        await LoadFromUrlAsync(UrlEntry.Text, showAlertOnError: true);
    }

    private async void OnLoadSampleClicked(object? sender, EventArgs e)
    {
        UrlEntry.Text = DefaultSampleUrl;
        await LoadFromUrlAsync(DefaultSampleUrl, showAlertOnError: true);
    }

    private async Task LoadFromUrlAsync(string? input, bool showAlertOnError)
    {
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            ShowStatus(AppResources.InvalidUrl);
            if (showAlertOnError)
                await DisplayAlertAsync(AppResources.InvalidUrl, AppResources.EnterFullUrl, "OK");
            return;
        }

        PdfViewer.Source = new UriPdfSource(uri);
        ShowStatus(AppResources.LoadingUrlPdf);
    }

    private async void OnPickFileClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = AppResources.SelectPdfFile
            });

            if (result is null)
            {
                ShowStatus(AppResources.FileSelectionCancelled);
                return;
            }

            if (!string.Equals(Path.GetExtension(result.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus(AppResources.SelectPdfFileOnly);
                return;
            }

            await using var stream = await result.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var data = memory.ToArray();
            if (data.Length == 0)
            {
                ShowStatus(AppResources.FileEmpty);
                return;
            }

            PdfViewer.Source = new BytesPdfSource(data);
            ShowStatus(AppResources.LoadingLocalPdf);
        }
        catch (Exception ex)
        {
            ShowStatus($"{AppResources.SelectFileFailed}: {ex.Message}");
        }
    }

    private void OnReloadClicked(object? sender, EventArgs e)
    {
        PdfViewer.Reload();
        ShowStatus(AppResources.ReloadTriggered);
    }

    private void OnMenuClicked(object? sender, EventArgs e)
    {
        SettingsPanel.IsVisible = !SettingsPanel.IsVisible;
    }

    private void OnSettingsCloseClicked(object? sender, EventArgs e)
    {
        SettingsPanel.IsVisible = false;
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        var query = SearchEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SetSearchStatus(AppResources.SearchEnterKeyword);
            return;
        }

        if (!PdfViewer.IsSearchSupported)
        {
            SetSearchStatus(AppResources.SearchNotSupported);
            return;
        }

        try
        {
            var options = new PdfSearchOptions
            {
                Highlight = true,
                SearchAllPages = true,
                MaxResults = 200
            };

            _searchResults = await PdfViewer.SearchAsync(query, options);
            _currentSearchIndex = _searchResults.Count > 0 ? 0 : -1;
            UpdateSearchNavState();

            if (_currentSearchIndex >= 0)
            {
                PdfViewer.GoToSearchResult(_currentSearchIndex);
                SetSearchStatus(AppResources.SearchResultsFoundFormat.Replace("{0}", _searchResults.Count.ToString()).Replace("{1}", "1").Replace("{2}", _searchResults.Count.ToString()));
            }
            else
            {
                SetSearchStatus(AppResources.SearchNoResults);
            }
        }
        catch (Exception ex)
        {
            SetSearchStatus(AppResources.SearchFailed + $": {ex.Message}");
        }
    }

    private void OnSearchPrevClicked(object? sender, EventArgs e)
    {
        GoToSearchResultWithOffset(-1);
    }

    private void OnSearchNextClicked(object? sender, EventArgs e)
    {
        GoToSearchResultWithOffset(1);
    }

    private void OnSearchClearClicked(object? sender, EventArgs e)
    {
        _searchResults = Array.Empty<PdfSearchResult>();
        _currentSearchIndex = -1;
        UpdateSearchNavState();
        SetSearchStatus(AppResources.SearchCleared);
        PdfViewer.ClearSearch();
    }

    private void OnSearchHighlightToggled(object? sender, ToggledEventArgs e)
    {
        if (!PdfViewer.IsSearchSupported)
        {
            SetSearchStatus(AppResources.SearchHighlightNotSupported);
            return;
        }

        PdfViewer.HighlightSearchResults(e.Value);
    }

    private void OnPrevPageClicked(object? sender, EventArgs e)
    {
        var currentPage = Math.Clamp(PdfViewer.CurrentPage, 0, Math.Max(0, _totalPageCount - 1));
        if (currentPage <= 0)
            return;

        PdfViewer.GoToPage(currentPage - 1);
    }

    private void OnNextPageClicked(object? sender, EventArgs e)
    {
        var currentPage = Math.Clamp(PdfViewer.CurrentPage, 0, Math.Max(0, _totalPageCount - 1));
        if (currentPage + 1 >= _totalPageCount)
            return;

        PdfViewer.GoToPage(currentPage + 1);
    }

    private void OnDisplayModeChanged(object? sender, EventArgs e)
    {
        if (DisplayModePicker.SelectedIndex < 0)
            return;

        PdfViewer.DisplayMode = (PdfDisplayMode)DisplayModePicker.SelectedIndex;
    }

    private void OnOrientationChanged(object? sender, EventArgs e)
    {
        if (OrientationPicker.SelectedIndex < 0)
            return;

        PdfViewer.ScrollOrientation = (PdfScrollOrientation)OrientationPicker.SelectedIndex;
    }

    private void OnFitPolicyChanged(object? sender, EventArgs e)
    {
        if (FitPolicyPicker.SelectedIndex < 0)
            return;

        PdfViewer.FitPolicy = (FitPolicy)FitPolicyPicker.SelectedIndex;
    }

    private void OnEnableZoomToggled(object? sender, ToggledEventArgs e)
    {
        PdfViewer.EnableZoom = e.Value;
    }

    private void OnEnableSwipeToggled(object? sender, ToggledEventArgs e)
    {
        PdfViewer.EnableSwipe = e.Value;
    }

    private void OnEnableLinkToggled(object? sender, ToggledEventArgs e)
    {
        PdfViewer.EnableLinkNavigation = e.Value;
    }

    private void OnZoomSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        var zoom = MathF.Round((float)e.NewValue, 2);
        PdfViewer.Zoom = zoom;
        ZoomValueLabel.Text = $"{zoom:0.00}x";
    }

    private void OnDocumentLoaded(object? sender, DocumentLoadedEventArgs e)
    {
        _totalPageCount = e.PageCount;
        _currentPageIndex = Math.Clamp(PdfViewer.CurrentPage, 0, Math.Max(0, _totalPageCount - 1));
        UpdatePageIndicators();
        ShowStatus(AppResources.DocumentLoadedFormat.Replace("{0}", _totalPageCount.ToString()));
    }

    private void OnPageChanged(object? sender, PdfPageChangedEventArgs e)
    {
        _currentPageIndex = e.PageIndex;
        _totalPageCount = e.PageCount;
        UpdatePageIndicators();
    }

    private void OnPdfError(object? sender, PdfErrorEventArgs e)
    {
        ShowStatus(AppResources.LoadFailed + $": {e.Message}");
    }

    private void OnSearchResultsFound(object? sender, PdfSearchResultsEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _searchResults = e.Results;
            _currentSearchIndex = _searchResults.Count > 0 ? Math.Clamp(e.CurrentIndex, 0, _searchResults.Count - 1) : -1;
            UpdateSearchNavState();

            if (_searchResults.Count > 0)
            {
                SetSearchStatus(AppResources.SearchResultsFoundFormat.Replace("{0}", _searchResults.Count.ToString()).Replace("{1}", (_currentSearchIndex + 1).ToString()).Replace("{2}", _searchResults.Count.ToString()));
            }
            else if (!string.IsNullOrWhiteSpace(e.Query))
            {
                SetSearchStatus(AppResources.SearchNoResults);
            }
        });
    }

    private void OnSearchProgress(object? sender, PdfSearchProgressEventArgs e)
    {
        if (_currentSearchIndex < 0)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetSearchStatus(AppResources.SearchProgressFormat.Replace("{0}", e.CurrentPage.ToString()).Replace("{1}", e.TotalPages.ToString()).Replace("{2}", e.ResultCount.ToString()));
            });
        }
    }

    private void UpdatePageIndicators()
    {
        if (_totalPageCount <= 0)
        {
            PageInfoLabel.Text = AppResources.PageInfoFormat.Replace("{0}", "0").Replace("{1}", "0");
            PrevPageButton.IsEnabled = false;
            NextPageButton.IsEnabled = false;
            return;
        }

        _currentPageIndex = Math.Clamp(PdfViewer.CurrentPage, 0, Math.Max(0, _totalPageCount - 1));
        PageInfoLabel.Text = string.Format(AppResources.PageInfoFormat, _currentPageIndex + 1, _totalPageCount);
        PrevPageButton.IsEnabled = _currentPageIndex > 0;
        NextPageButton.IsEnabled = _currentPageIndex + 1 < _totalPageCount;
    }

    private void ShowStatus(string message)
    {
        StatusLabel.Text = message;
        StatusToast.IsVisible = true;
        StatusToast.Opacity = 1;
        
        Task.Run(async () =>
        {
            await Task.Delay(2000);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusToast.FadeTo(0, 300);
            });
            await Task.Delay(300);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusToast.IsVisible = false;
            });
        });
    }

    private void SetSearchStatus(string message)
    {
        SearchStatusLabel.Text = message;
    }

    private void UpdateSearchNavState()
    {
        var hasResults = _searchResults.Count > 0;
        SearchPrevButton.IsEnabled = hasResults;
        SearchNextButton.IsEnabled = hasResults;
    }

    private void GoToSearchResultWithOffset(int offset)
    {
        if (_searchResults.Count == 0)
            return;

        if (!PdfViewer.IsSearchSupported)
            return;

        _currentSearchIndex = (_currentSearchIndex + offset + _searchResults.Count) % _searchResults.Count;
        PdfViewer.GoToSearchResult(_currentSearchIndex);
        SetSearchStatus(AppResources.SearchResultsFoundFormat.Replace("{0}", _searchResults.Count.ToString()).Replace("{1}", (_currentSearchIndex + 1).ToString()).Replace("{2}", _searchResults.Count.ToString()));
    }

    private void OnLangEnClicked(object? sender, EventArgs e)
    {
        LanguageManager.SetCulture(new CultureInfo("en-US"));
    }

    private void OnLangZhClicked(object? sender, EventArgs e)
    {
        LanguageManager.SetCulture(new CultureInfo("zh-CN"));
    }

    // Drawing related methods
    private void OnDrawingToggleClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.EnableDrawing = !DrawingCanvas.EnableDrawing;
        DrawingCanvas.IsVisible = DrawingCanvas.EnableDrawing;
        DrawingToolbarPanel.IsVisible = DrawingCanvas.EnableDrawing;
        
        if (DrawingCanvas.EnableDrawing)
        {
            UpdateToolSelection("Pen");
        }
    }

    private void OnDrawingToolbarCloseClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.EnableDrawing = false;
        DrawingCanvas.IsVisible = false;
        DrawingToolbarPanel.IsVisible = false;
        LayerPanel.IsVisible = false;
    }

    private void OnPenModeClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.IsPenMode = !DrawingCanvas.IsPenMode;
        DrawingCanvas.IsErasing = false;
        DrawingCanvas.IsHighlighter = false;
        UpdateToolSelection(DrawingCanvas.IsPenMode ? "Pen" : "Finger");
        
        if (DrawingCanvas.IsPenMode)
        {
            PenModeButton.Style = (Style)Resources["ToolButtonSelected"];
        }
        else
        {
            PenModeButton.Style = (Style)Resources["ToolButton"];
        }
    }

    private void OnLayerToggleClicked(object? sender, EventArgs e)
    {
        LayerPanel.IsVisible = !LayerPanel.IsVisible;
    }

    private void OnHighlighterClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.IsErasing = false;
        DrawingCanvas.IsHighlighter = !DrawingCanvas.IsHighlighter;
        UpdateToolSelection(DrawingCanvas.IsHighlighter ? "Highlighter" : "Pen");
    }

    private void OnEraserClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.IsErasing = !DrawingCanvas.IsErasing;
        DrawingCanvas.IsHighlighter = false;
        UpdateToolSelection(DrawingCanvas.IsErasing ? "Eraser" : "Pen");
    }

    private void UpdateToolSelection(string selectedTool)
    {
        var isSelected = Application.Current.RequestedTheme == AppTheme.Light;
        var selectedColor = isSelected ? Color.FromRgb(219, 234, 254) : Color.FromRgb(30, 58, 95);
        var normalColor = Colors.Transparent;
        
        PenModeButton.BackgroundColor = (selectedTool == "Pen" || selectedTool == "Finger") ? selectedColor : normalColor;
        HighlighterButton.BackgroundColor = selectedTool == "Highlighter" ? selectedColor : normalColor;
        EraserButton.BackgroundColor = selectedTool == "Eraser" ? selectedColor : normalColor;
    }

    private void OnRedoClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.Redo();
    }

    private void OnUndoClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.Undo();
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.ClearCurrentLayer();
    }

    private void OnStrokeWidthChanged(object? sender, ValueChangedEventArgs e)
    {
        DrawingCanvas.StrokeWidth = (float)e.NewValue;
        StrokeWidthLabel.Text = $"{(int)e.NewValue}";
    }

    private void OnColorBlackClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.StrokeColor = SKColors.Black;
        UpdateColorSelection("Black");
    }

    private void OnColorRedClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.StrokeColor = SKColors.Red;
        UpdateColorSelection("Red");
    }

    private void OnColorBlueClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.StrokeColor = SKColors.Blue;
        UpdateColorSelection("Blue");
    }

    private void OnColorGreenClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.StrokeColor = SKColors.Green;
        UpdateColorSelection("Green");
    }

    private void OnColorOrangeClicked(object? sender, EventArgs e)
    {
        DrawingCanvas.StrokeColor = SKColors.Orange;
        UpdateColorSelection("Orange");
    }

    private void OnColorWhiteClicked(object? sender, EventArgs e)
    {
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
        DrawingCanvas.AddLayer();
        RefreshLayerList();
    }

    private void OnDeleteLayerClicked(object? sender, EventArgs e)
    {
        if (DrawingCanvas.Layers.Count > 1)
        {
            DrawingCanvas.RemoveLayer(DrawingCanvas.CurrentLayerIndex);
            RefreshLayerList();
        }
    }

    private void RefreshLayerList()
    {
        LayerList.Clear();
        for (int i = 0; i < DrawingCanvas.Layers.Count; i++)
        {
            var layer = DrawingCanvas.Layers[i];
            var isSelected = i == DrawingCanvas.CurrentLayerIndex;
            
            var bgColor = isSelected ? Color.FromRgb(219, 234, 254) : Colors.Transparent;
            if (Application.Current.RequestedTheme == AppTheme.Dark)
            {
                bgColor = isSelected ? Color.FromRgb(30, 58, 95) : Colors.Transparent;
            }
            
            var layerItem = new Border
            {
                BackgroundColor = bgColor,
                Padding = new Thickness(8, 8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 }
            };
            
            var stack = new HorizontalStackLayout();
            
            var visibilityIcon = new ImageButton
            {
                Source = layer.IsVisible ? "visibility" : "visibility_off",
                WidthRequest = 24,
                HeightRequest = 24,
                BackgroundColor = Colors.Transparent,
                Command = new Command(() => 
                {
                    layer.IsVisible = !layer.IsVisible;
                    DrawingCanvas.InvalidateSurface();
                    RefreshLayerList();
                })
            };
            
            var label = new Label
            {
                Text = layer.Name,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 14
            };
            
            stack.Children.Add(visibilityIcon);
            stack.Children.Add(label);
            
            layerItem.Content = stack;
            layerItem.GestureRecognizers.Add(new TapGestureRecognizer 
            {
                Command = new Command(() => 
                {
                    DrawingCanvas.CurrentLayerIndex = i;
                    RefreshLayerList();
                })
            });
            
            LayerList.Add(layerItem);
        }
    }

    private void OnFingerDrawToggled(object? sender, ToggledEventArgs e)
    {
        DrawingCanvas.IsPenMode = !e.Value;
        if (e.Value)
        {
            PenModeButton.Style = (Style)Resources["ToolButton"];
        }
        else
        {
            PenModeButton.Style = (Style)Resources["ToolButtonSelected"];
        }
    }
}
