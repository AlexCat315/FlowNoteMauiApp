using System.Globalization;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Resources;
using PdfPageChangedEventArgs = Flow.PDFView.Abstractions.PageChangedEventArgs;
using PdfViewportChangedEventArgs = Flow.PDFView.Abstractions.ViewportChangedEventArgs;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        if (!EnsurePdfLoaded(showHint: true))
        {
            SetSearchStatus(AppResources.SearchNotSupported);
            return;
        }

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
        if (!IsEditorInitialized)
            return;

        PdfViewer.ClearSearch();
    }

    private void OnSearchHighlightToggled(object? sender, ToggledEventArgs e)
    {
        if (!EnsurePdfLoaded())
            return;

        if (!PdfViewer.IsSearchSupported)
        {
            SetSearchStatus(AppResources.SearchHighlightNotSupported);
            return;
        }

        PdfViewer.HighlightSearchResults(e.Value);
    }

    private void OnPrevPageClicked(object? sender, EventArgs e)
    {
        if (!EnsurePdfLoaded())
            return;

        var currentPage = Math.Clamp(PdfViewer.CurrentPage, 0, Math.Max(0, _totalPageCount - 1));
        if (currentPage <= 0)
            return;

        PdfViewer.GoToPage(currentPage - 1);
    }

    private void OnNextPageClicked(object? sender, EventArgs e)
    {
        if (!EnsurePdfLoaded())
            return;

        var currentPage = Math.Clamp(PdfViewer.CurrentPage, 0, Math.Max(0, _totalPageCount - 1));
        if (currentPage + 1 >= _totalPageCount)
            return;

        PdfViewer.GoToPage(currentPage + 1);
    }

    private void OnDisplayModeChanged(object? sender, EventArgs e)
    {
        if (DisplayModePicker.SelectedIndex < 0)
            return;

        if (!IsEditorInitialized)
            return;

        PdfViewer.DisplayMode = (PdfDisplayMode)DisplayModePicker.SelectedIndex;
    }

    private void OnOrientationChanged(object? sender, EventArgs e)
    {
        if (OrientationPicker.SelectedIndex < 0)
            return;

        if (!IsEditorInitialized)
            return;

        PdfViewer.ScrollOrientation = (PdfScrollOrientation)OrientationPicker.SelectedIndex;
    }

    private void OnFitPolicyChanged(object? sender, EventArgs e)
    {
        if (FitPolicyPicker.SelectedIndex < 0)
            return;

        if (!IsEditorInitialized)
            return;

        PdfViewer.FitPolicy = (FitPolicy)FitPolicyPicker.SelectedIndex;
    }

    private void OnEnableZoomToggled(object? sender, ToggledEventArgs e)
    {
        ApplyViewerSettingsFromUi();
    }

    private void OnEnableSwipeToggled(object? sender, ToggledEventArgs e)
    {
        ApplyViewerSettingsFromUi();
    }

    private void OnEnableLinkToggled(object? sender, ToggledEventArgs e)
    {
        ApplyViewerSettingsFromUi();
    }

    private void OnZoomSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        var zoom = MathF.Round((float)e.NewValue, 2);
        ZoomValueLabel.Text = $"{zoom:0.00}x";

        if (!IsEditorInitialized)
            return;

        PdfViewer.Zoom = zoom;
        DrawingCanvas.ViewportZoom = zoom;
    }

    private void OnDocumentLoaded(object? sender, DocumentLoadedEventArgs e)
    {
        _totalPageCount = e.PageCount;
        _currentPageIndex = Math.Clamp(PdfViewer.CurrentPage, 0, Math.Max(0, _totalPageCount - 1));
        DrawingCanvas.ViewportZoom = PdfViewer.Zoom <= 0f ? 1f : PdfViewer.Zoom;
        UpdatePageIndicators();
        ShowStatus(AppResources.DocumentLoadedFormat.Replace("{0}", _totalPageCount.ToString()));
    }

    private void OnPageChanged(object? sender, PdfPageChangedEventArgs e)
    {
        _currentPageIndex = e.PageIndex;
        _totalPageCount = e.PageCount;
        UpdatePageIndicators();
    }

    private void OnPdfViewportChanged(object? sender, PdfViewportChangedEventArgs e)
    {
        if (!IsEditorInitialized)
            return;

        DrawingCanvas.ScrollX = e.OffsetX;
        DrawingCanvas.ScrollY = e.OffsetY;
        DrawingCanvas.ViewportZoom = e.Zoom <= 0f ? 1f : e.Zoom;
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
        if (!IsEditorInitialized || _totalPageCount <= 0)
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

        _ = HideStatusToastAsync();
    }

    private async Task HideStatusToastAsync()
    {
        await Task.Delay(2000);
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await StatusToast.FadeToAsync(0, 300);
        });
        await Task.Delay(300);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusToast.IsVisible = false;
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
        if (!EnsurePdfLoaded())
            return;

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
}
