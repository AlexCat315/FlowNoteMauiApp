using System.Globalization;
using System.Resources;
using System.ComponentModel;

namespace ExampleMauiApp.Resources;

public static class AppResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    public static ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager == null)
            {
                _resourceManager = new ResourceManager("ExampleMauiApp.Resources.AppResources", typeof(AppResources).Assembly);
            }
            return _resourceManager;
        }
    }

    public static CultureInfo? Culture
    {
        get => _resourceCulture;
        set
        {
            _resourceCulture = value;
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static event EventHandler? CultureChanged;

    public static string ToolbarToggle => ResourceManager.GetString("ToolbarToggle", Culture) ?? "Open Toolbar";
    public static string ToolbarTitle => ResourceManager.GetString("ToolbarTitle", Culture) ?? "FlowPDFView Toolbar";
    public static string Close => ResourceManager.GetString("Close", Culture) ?? "Close";
    public static string UrlPlaceholder => ResourceManager.GetString("UrlPlaceholder", Culture) ?? "";
    public static string LoadUrl => ResourceManager.GetString("LoadUrl", Culture) ?? "Load URL";
    public static string SearchPlaceholder => ResourceManager.GetString("SearchPlaceholder", Culture) ?? "";
    public static string Search => ResourceManager.GetString("Search", Culture) ?? "Search";
    public static string Clear => ResourceManager.GetString("Clear", Culture) ?? "Clear";
    public static string Previous => ResourceManager.GetString("Previous", Culture) ?? "Previous";
    public static string Next => ResourceManager.GetString("Next", Culture) ?? "Next";
    public static string Highlight => ResourceManager.GetString("Highlight", Culture) ?? "Highlight";
    public static string SearchStatus => ResourceManager.GetString("SearchStatus", Culture) ?? "Search: Not executed";
    public static string LoadSample => ResourceManager.GetString("LoadSample", Culture) ?? "Load Sample";
    public static string LocalFile => ResourceManager.GetString("LocalFile", Culture) ?? "Local File";
    public static string Reload => ResourceManager.GetString("Reload", Culture) ?? "Reload";
    public static string PreviousPage => ResourceManager.GetString("PreviousPage", Culture) ?? "Previous Page";
    public static string NextPage => ResourceManager.GetString("NextPage", Culture) ?? "Next Page";
    public static string Source => ResourceManager.GetString("Source", Culture) ?? "Source";
    public static string SourceNotLoaded => ResourceManager.GetString("SourceNotLoaded", Culture) ?? "Source: Not loaded";
    public static string DisplayMode => ResourceManager.GetString("DisplayMode", Culture) ?? "Display Mode";
    public static string ScrollOrientation => ResourceManager.GetString("ScrollOrientation", Culture) ?? "Scroll Orientation";
    public static string FitPolicy => ResourceManager.GetString("FitPolicy", Culture) ?? "Fit Policy";
    public static string Zoom => ResourceManager.GetString("Zoom", Culture) ?? "Zoom";
    public static string Swipe => ResourceManager.GetString("Swipe", Culture) ?? "Swipe";
    public static string Link => ResourceManager.GetString("Link", Culture) ?? "Link";
    public static string PageInfo => ResourceManager.GetString("PageInfo", Culture) ?? "Page: - / -";
    public static string Status => ResourceManager.GetString("Status", Culture) ?? "Status";
    public static string StatusWaiting => ResourceManager.GetString("StatusWaiting", Culture) ?? "Status: Waiting for PDF";
    public static string Language => ResourceManager.GetString("Language", Culture) ?? "Language";
    public static string English => ResourceManager.GetString("English", Culture) ?? "English";
    public static string Chinese => ResourceManager.GetString("Chinese", Culture) ?? "Chinese";
    public static string InvalidUrl => ResourceManager.GetString("InvalidUrl", Culture) ?? "Invalid URL";
    public static string EnterFullUrl => ResourceManager.GetString("EnterFullUrl", Culture) ?? "Please enter a complete absolute URL.";
    public static string LoadingUrlPdf => ResourceManager.GetString("LoadingUrlPdf", Culture) ?? "Loading URL PDF...";
    public static string FileSelectionCancelled => ResourceManager.GetString("FileSelectionCancelled", Culture) ?? "File selection cancelled";
    public static string SelectPdfFile => ResourceManager.GetString("SelectPdfFile", Culture) ?? "Select a PDF file";
    public static string FileEmpty => ResourceManager.GetString("FileEmpty", Culture) ?? "Selected file is empty";
    public static string LoadingLocalPdf => ResourceManager.GetString("LoadingLocalPdf", Culture) ?? "Loading local PDF...";
    public static string SelectFileFailed => ResourceManager.GetString("SelectFileFailed", Culture) ?? "Select file failed";
    public static string ReloadTriggered => ResourceManager.GetString("ReloadTriggered", Culture) ?? "Reload triggered";
    public static string SearchEnterKeyword => ResourceManager.GetString("SearchEnterKeyword", Culture) ?? "Search: Please enter keyword";
    public static string SearchNotSupported => ResourceManager.GetString("SearchNotSupported", Culture) ?? "Search: Not supported";
    public static string SearchNoResults => ResourceManager.GetString("SearchNoResults", Culture) ?? "Search: No results found";
    public static string SearchFailed => ResourceManager.GetString("SearchFailed", Culture) ?? "Search failed";
    public static string SearchCleared => ResourceManager.GetString("SearchCleared", Culture) ?? "Search: Cleared";
    public static string SearchHighlight => ResourceManager.GetString("SearchHighlight", Culture) ?? "Search highlight";
    public static string SearchHighlightNotSupported => ResourceManager.GetString("SearchHighlightNotSupported", Culture) ?? "Search highlight: Not supported";
    public static string Enabled => ResourceManager.GetString("Enabled", Culture) ?? "Enabled";
    public static string Disabled => ResourceManager.GetString("Disabled", Culture) ?? "Disabled";
    public static string DocumentLoaded => ResourceManager.GetString("DocumentLoaded", Culture) ?? "Document loaded";
    public static string LoadFailed => ResourceManager.GetString("LoadFailed", Culture) ?? "Load failed";
    public static string Searching => ResourceManager.GetString("Searching", Culture) ?? "Searching";
    public static string SelectPdfFileOnly => ResourceManager.GetString("SelectPdfFileOnly", Culture) ?? "Please select .pdf file only";
    public static string SearchResultsFoundFormat => ResourceManager.GetString("SearchResultsFoundFormat", Culture) ?? "Search: {0} results, {1}/{2}";
    public static string PageInfoFormat => ResourceManager.GetString("PageInfoFormat", Culture) ?? "Page: {1}/{2}";
    public static string StatusMessage => ResourceManager.GetString("StatusMessage", Culture) ?? "Status: {0}";
    public static string EnableZoomFormat => ResourceManager.GetString("EnableZoomFormat", Culture) ?? "Zoom: {0}";
    public static string EnableSwipeFormat => ResourceManager.GetString("EnableSwipeFormat", Culture) ?? "Swipe: {0}";
    public static string EnableLinkFormat => ResourceManager.GetString("EnableLinkFormat", Culture) ?? "Link: {0}";
    public static string DocumentLoadedFormat => ResourceManager.GetString("DocumentLoadedFormat", Culture) ?? "Document loaded, {0} pages total";
    public static string DisplayModeFormat => ResourceManager.GetString("DisplayModeFormat", Culture) ?? "Display: {0}";
    public static string OrientationFormat => ResourceManager.GetString("OrientationFormat", Culture) ?? "Orientation: {0}";
    public static string FitPolicyFormat => ResourceManager.GetString("FitPolicyFormat", Culture) ?? "Fit: {0}";
    public static string SearchProgressFormat => ResourceManager.GetString("SearchProgressFormat", Culture) ?? "Searching: {0}/{1}, {2} results";
    public static string Yes => ResourceManager.GetString("Yes", Culture) ?? "Yes";
    public static string No => ResourceManager.GetString("No", Culture) ?? "No";
}
