using Flow.PDFView;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Controls;
using FlowNoteMauiApp.Resources;
using FlowNoteMauiApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Devices;

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
    private bool _isUiBootstrapped;
    private bool _isBootstrappingUi;
    private bool _isDisplayInfoEventsWired;
    private CancellationTokenSource? _floatingPanelRepositionCts;

    public MainPage()
    {
        InitializeComponent();
        HomePanelView.IsVisible = true;
        HomePanelView.InputTransparent = false;
        EditorChromeView.IsVisible = false;
        EditorChromeView.InputTransparent = true;
        DrawerOverlayView.IsVisible = false;
        DrawerOverlayView.InputTransparent = true;
        SettingsOverlayView.IsVisible = false;
        SettingsOverlayView.InputTransparent = true;
        StatusToastView.InputTransparent = true;

        WireHomePanelEvents();
        var services = Application.Current?.Handler?.MauiContext?.Services;
        _workspaceService = services?.GetService<IWorkspaceService>() ?? new WorkspaceService();
        _drawingPersistenceService = services?.GetService<IDrawingPersistenceService>() ?? new DrawingPersistenceService();

        if (ShouldUseFingerModeByDefault())
        {
            _drawingInputMode = DrawingInputMode.FingerCapacitive;
        }

        LoadPersistedAppSettings();
        ApplyGlobalSettings();
        UpdateLocalizedStrings();
        UpdateHomeSortLabel();
        UpdateHomeFilterButtons();
        Loaded += OnPageLoaded;
        LanguageManager.LanguageChanged += OnLanguageChanged;
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        }

        EnsureDisplayInfoEventsWired();
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
            VerticalOptions = LayoutOptions.Fill,
            ZoomAffectsStrokeWidth = _zoomFollowEnabled,
            PressureSensitivity = _pressureSensitivity,
            UsePressureSensitivity = true
        };
        _drawingCanvas.CanDrawAtDocumentPoint = CanDrawAtDocumentLocation;

        _drawingCanvas.Layers.CollectionChanged += (_, _) => RefreshLayerList();
        _drawingCanvas.StrokeStarted += OnDrawingStrokeStarted;
        _drawingCanvas.StrokeFinalized += OnDrawingStrokeFinalized;
        _drawingCanvas.StrokeCommitted += OnDrawingStrokeCommitted;
        _drawingCanvas.TwoFingerSwipe += OnDrawingCanvasTwoFingerSwipe;
        _drawingCanvas.TwoFingerPan += OnDrawingCanvasTwoFingerPan;

        EditorHost.Children.Add(_pdfViewer);
        EditorHost.Children.Add(_drawingCanvas);

        ApplyViewerSettingsFromUi();
        UpdateTwoFingerNavigationPolicy();
        ApplyDarkModeInversion();
        _drawingCanvas.ViewportZoom = _pdfViewer.Zoom <= 0f ? 1f : _pdfViewer.Zoom;
        ApplyInputMode(_drawingInputMode, activateDrawing: false);
        RefreshLayerList();
        UpdateToolButtonTintColors();
    }

    private void UpdateTwoFingerNavigationPolicy()
    {
        if (!IsEditorInitialized)
            return;

        DrawingCanvas.EnableTwoFingerSwipeNavigation = PdfViewer.DisplayMode == PdfDisplayMode.SinglePage;
    }

    private void ApplyViewerSettingsFromUi()
    {
        if (!IsEditorInitialized)
            return;

        var minZoom = Math.Max(EditorMinZoom, (float)ZoomSlider.Minimum);
        var maxZoom = Math.Max(minZoom, (float)ZoomSlider.Maximum);
        var zoom = Math.Clamp((float)ZoomSlider.Value, minZoom, maxZoom);
        var forceNativeGestures = RequiresNativePdfGesturesOnPlatform();
        var enableZoom = forceNativeGestures || EnableZoomSwitch.IsToggled;
        var enableSwipe = forceNativeGestures || EnableSwipeSwitch.IsToggled;

        PdfViewer.EnableZoom = enableZoom;
        PdfViewer.EnableSwipe = enableSwipe;
        PdfViewer.EnableLinkNavigation = EnableLinkSwitch.IsToggled;
        PdfViewer.MinZoom = minZoom;
        PdfViewer.MaxZoom = maxZoom;
        PdfViewer.Zoom = zoom;
        DrawingCanvas.ViewportZoom = zoom;
        LogInputGesture(
            $"viewer-settings platform={DeviceInfo.Platform} force-native={forceNativeGestures} " +
            $"swipe={PdfViewer.EnableSwipe} zoom={PdfViewer.EnableZoom} link={PdfViewer.EnableLinkNavigation}");
    }

    private bool EnsurePdfLoaded(bool showHint = false)
    {
        if (HasLoadedDocument)
            return true;

        if (showHint)
            ShowStatus(T("OpenPdfFirst", "Open a PDF first."));
        return false;
    }

    private void OnLanguageChanged()
    {
        UpdateLocalizedStrings();
        if (_isUiBootstrapped)
        {
            RefreshSettingsUiState();
        }
        RefreshHomeFeed();
    }

    private bool IsDarkTheme => Application.Current?.RequestedTheme == AppTheme.Dark;

    private static bool ShouldUseFingerModeByDefault()
    {
        if (DeviceInfo.Platform == DevicePlatform.MacCatalyst)
            return true;

        return DeviceInfo.Platform == DevicePlatform.iOS;
    }

    private static bool RequiresNativePdfGesturesOnPlatform()
    {
        return DeviceInfo.Platform == DevicePlatform.MacCatalyst
            || DeviceInfo.Platform == DevicePlatform.iOS;
    }

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
            if (_isUiBootstrapped)
            {
                ApplyLocalizedUiText();
            }
            else
            {
                ApplyHomeLocalizedUiText();
            }
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

        DisplayModePicker.SelectedIndex = (int)_savedDisplayMode;
        OrientationPicker.SelectedIndex = (int)_savedScrollOrientation;
        FitPolicyPicker.SelectedIndex = (int)_savedFitPolicy;
        ZoomSlider.Minimum = EditorMinZoom;
        ZoomSlider.Maximum = EditorMaxZoom;
        ZoomSlider.Value = Math.Clamp(_savedZoom, EditorMinZoom, EditorMaxZoom);
        ZoomValueLabel.Text = $"{ZoomSlider.Value:0.00}x";

        EnableZoomSwitch.IsToggled = _savedEnableZoom;
        EnableSwipeSwitch.IsToggled = _savedEnableSwipe;
        EnableLinkSwitch.IsToggled = _savedEnableLink;
        if (RequiresNativePdfGesturesOnPlatform())
        {
            EnableZoomSwitch.IsToggled = true;
            EnableSwipeSwitch.IsToggled = true;
        }
        ApplyViewerSettingsFromUi();

        UpdatePageIndicators();
        RefreshLayerList();
        UpdateColorSelection("Black");
        ApplyInputMode(_drawingInputMode, activateDrawing: false);
        WorkspaceFolderEntry.Text = _workspaceFolder;
        RefreshEditorTabsVisual();
        UpdateHomeSortLabel();
        UpdateHomeFilterButtons();
        ApplyImageButtonIconSizing();
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        if (_isInitialLoadDone)
            return;

        _isInitialLoadDone = true;
        ShowHomeScreen();
        _ = RefreshWorkspaceViewsAfterStartupAsync();
        _ = BootstrapUiAfterFirstFrameAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        EnsureDisplayInfoEventsWired();
        ScheduleFloatingPanelReposition();
    }

    private async Task BootstrapUiAfterFirstFrameAsync()
    {
        if (_isUiBootstrapped || _isBootstrappingUi)
            return;

        await Task.Delay(120);
        await MainThread.InvokeOnMainThreadAsync(EnsureUiBootstrapped);
    }

    private void EnsureUiBootstrapped()
    {
        if (_isUiBootstrapped || _isBootstrappingUi)
            return;

        try
        {
            _isBootstrappingUi = true;
            WireComposedViewEvents();
            InitializeControls();
            RefreshSettingsUiState();
            _isUiBootstrapped = true;
            UpdateLocalizedStrings();
        }
        finally
        {
            _isBootstrappingUi = false;
        }
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
        UnwireDisplayInfoEvents();
        _floatingPanelRepositionCts?.Cancel();
        _floatingPanelRepositionCts?.Dispose();
        _floatingPanelRepositionCts = null;
        CancelHomeFeedRender();
        StopTwoFingerInertia();
        await SaveCurrentDrawingStateAsync();
    }

    private void EnsureDisplayInfoEventsWired()
    {
        if (_isDisplayInfoEventsWired)
            return;

        DeviceDisplay.Current.MainDisplayInfoChanged += OnMainDisplayInfoChanged;
        _isDisplayInfoEventsWired = true;
    }

    private void UnwireDisplayInfoEvents()
    {
        if (!_isDisplayInfoEventsWired)
            return;

        DeviceDisplay.Current.MainDisplayInfoChanged -= OnMainDisplayInfoChanged;
        _isDisplayInfoEventsWired = false;
    }

    private void OnMainDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        ScheduleFloatingPanelReposition();
    }

    private void ScheduleFloatingPanelReposition()
    {
        _floatingPanelRepositionCts?.Cancel();
        _floatingPanelRepositionCts?.Dispose();
        _floatingPanelRepositionCts = new CancellationTokenSource();
        var token = _floatingPanelRepositionCts.Token;

        void Reposition()
        {
            if (token.IsCancellationRequested)
                return;

            OnEditorChromeLayoutChanged(this, EventArgs.Empty);
            OnHomeLayoutChanged(this, EventArgs.Empty);
        }

        MainThread.BeginInvokeOnMainThread(Reposition);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(24), Reposition);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(72), Reposition);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(156), Reposition);
    }
}
