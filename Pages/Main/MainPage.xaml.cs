using Flow.PDFView;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Controls;
using FlowNoteMauiApp.Resources;
using FlowNoteMauiApp.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FlowNoteMauiApp;

public partial class MainPage : ContentPage
{
    private const string DefaultSampleUrl = "https://www.w3.org/WAI/ER/tests/xhtml/testfiles/resources/pdf/dummy.pdf";
    private const float EditorMinZoom = 0.5f;
    private const float EditorMaxZoom = 4f;

    private int _currentPageIndex;
    private int _totalPageCount;
    private bool _isInitialLoadDone;
    private int _currentSearchIndex = -1;
    private IReadOnlyList<PdfSearchResult> _searchResults = Array.Empty<PdfSearchResult>();
    private readonly HttpClient _httpClient = new();
    private readonly IWorkspaceService _workspaceService;
    private readonly IDrawingPersistenceService _drawingPersistenceService;
    private string _workspaceFolder = "/";
    private string? _currentNoteId;
    private CancellationTokenSource? _inkSaveDebounce;
    private PdfView? _pdfViewer;
    private DrawingCanvas? _drawingCanvas;
    private DrawingInputMode _drawingInputMode = DrawingInputMode.PenStylus;
    private bool _isUpdatingFingerDrawSwitch;
    private bool _isSyncingZoomFromViewer;

    public MainPage()
    {
        InitializeComponent();
        var services = Application.Current?.Handler?.MauiContext?.Services;
        _workspaceService = services?.GetService<IWorkspaceService>() ?? new WorkspaceService();
        _drawingPersistenceService = services?.GetService<IDrawingPersistenceService>() ?? new DrawingPersistenceService();

        InitializeControls();
        UpdateLocalizedStrings();
        Loaded += OnPageLoaded;
        LanguageManager.LanguageChanged += OnLanguageChanged;
    }

    private bool IsEditorInitialized => _pdfViewer is not null && _drawingCanvas is not null;

    private PdfView PdfViewer =>
        _pdfViewer ?? throw new InvalidOperationException("PDF viewer has not been initialized.");

    private DrawingCanvas DrawingCanvas =>
        _drawingCanvas ?? throw new InvalidOperationException("Drawing canvas has not been initialized.");

    private bool HasLoadedDocument => IsEditorInitialized && PdfViewer.Source is not null;

    private void EnsureEditorInitialized()
    {
        if (IsEditorInitialized)
            return;

        _pdfViewer = new PdfView
        {
            IsVisible = false,
            EnableZoom = true,
            EnableSwipe = true,
            EnableLinkNavigation = true,
            DisplayMode = PdfDisplayMode.SinglePageContinuous,
            ScrollOrientation = PdfScrollOrientation.Vertical,
            FitPolicy = FitPolicy.Width,
            MinZoom = EditorMinZoom,
            MaxZoom = EditorMaxZoom,
            Zoom = 1f
        };

        _pdfViewer.DocumentLoaded += OnDocumentLoaded;
        _pdfViewer.PageChanged += OnPageChanged;
        _pdfViewer.Error += OnPdfError;
        _pdfViewer.SearchResultsFound += OnSearchResultsFound;
        _pdfViewer.SearchProgress += OnSearchProgress;
        _pdfViewer.ViewportChanged += OnPdfViewportChanged;

        _drawingCanvas = new DrawingCanvas
        {
            IsVisible = false,
            InputTransparent = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        _drawingCanvas.Layers.CollectionChanged += (_, _) => RefreshLayerList();
        _drawingCanvas.StrokeCommitted += OnDrawingStrokeCommitted;

        EditorHost.Children.Add(_pdfViewer);
        EditorHost.Children.Add(_drawingCanvas);

        ApplyViewerSettingsFromUi();
        _drawingCanvas.ViewportZoom = _pdfViewer.Zoom <= 0f ? 1f : _pdfViewer.Zoom;
        ApplyInputMode(_drawingInputMode, activateDrawing: false);
        RefreshLayerList();
    }

    private void ApplyViewerSettingsFromUi()
    {
        if (!IsEditorInitialized)
            return;

        var minZoom = Math.Max(EditorMinZoom, (float)ZoomSlider.Minimum);
        var maxZoom = Math.Max(minZoom, (float)ZoomSlider.Maximum);
        var zoom = Math.Clamp((float)ZoomSlider.Value, minZoom, maxZoom);

        PdfViewer.EnableZoom = EnableZoomSwitch.IsToggled;
        PdfViewer.EnableSwipe = EnableSwipeSwitch.IsToggled;
        PdfViewer.EnableLinkNavigation = EnableLinkSwitch.IsToggled;
        PdfViewer.MinZoom = minZoom;
        PdfViewer.MaxZoom = maxZoom;
        PdfViewer.Zoom = zoom;
        DrawingCanvas.ViewportZoom = zoom;
    }

    private bool EnsurePdfLoaded(bool showHint = false)
    {
        if (HasLoadedDocument)
            return true;

        if (showHint)
            ShowStatus("Open a PDF first.");
        return false;
    }

    private void OnLanguageChanged()
    {
        UpdateLocalizedStrings();
    }

    private bool IsDarkTheme => Application.Current?.RequestedTheme == AppTheme.Dark;

    private Color ThemeSelectedBackground => IsDarkTheme
        ? Color.FromArgb("#33527A")
        : Color.FromArgb("#E8F4FD");

    private Color ThemeListBackground => IsDarkTheme
        ? Color.FromArgb("#26374E")
        : Color.FromArgb("#F4F8FF");

    private Color ThemePrimaryText => IsDarkTheme
        ? Color.FromArgb("#E5E5EA")
        : Color.FromArgb("#1C1C1E");

    private Color ThemeSecondaryText => IsDarkTheme
        ? Color.FromArgb("#AEAEB2")
        : Color.FromArgb("#636366");

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
        UrlEntry.Text = string.Empty;
        HomeUrlEntry.Text = string.Empty;

        foreach (var item in Enum.GetNames<PdfDisplayMode>())
            DisplayModePicker.Items.Add(item);

        foreach (var item in Enum.GetNames<PdfScrollOrientation>())
            OrientationPicker.Items.Add(item);

        foreach (var item in Enum.GetNames<FitPolicy>())
            FitPolicyPicker.Items.Add(item);

        DisplayModePicker.SelectedIndex = (int)PdfDisplayMode.SinglePageContinuous;
        OrientationPicker.SelectedIndex = (int)PdfScrollOrientation.Vertical;
        FitPolicyPicker.SelectedIndex = (int)FitPolicy.Width;
        ZoomSlider.Minimum = EditorMinZoom;
        ZoomSlider.Maximum = EditorMaxZoom;
        ZoomSlider.Value = 1f;
        ZoomValueLabel.Text = "1.00x";

        EnableLinkSwitch.IsToggled = true;
        ApplyViewerSettingsFromUi();
        
        UpdatePageIndicators();
        RefreshLayerList();
        UpdateColorSelection("Black");
        ApplyInputMode(_drawingInputMode, activateDrawing: false);
        WorkspaceFolderEntry.Text = _workspaceFolder;
        UpdateHomeSortLabel();
        UpdateHomeFilterButtons();
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        if (_isInitialLoadDone)
            return;

        _isInitialLoadDone = true;
        ShowHomeScreen();
        _ = RefreshWorkspaceViewsAfterStartupAsync();
    }

    private async Task RefreshWorkspaceViewsAfterStartupAsync()
    {
        try
        {
            await RefreshWorkspaceViewsAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"Workspace refresh failed: {ex.Message}");
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await SaveCurrentDrawingStateAsync();
    }
}
