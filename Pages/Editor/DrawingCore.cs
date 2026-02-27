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
    private CancellationTokenSource? _toolTintUpdateCts;
    private readonly Dictionary<string, ImageSource> _thumbnailSourceCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _thumbnailRenderSemaphore = new(2, 2);
    private readonly ConcurrentDictionary<int, PdfPageBounds> _pageBoundsCache = new();
    private CancellationTokenSource? _thumbnailLoadCts;
    private readonly Dictionary<int, Border> _thumbnailItemLookup = new();
    private int _thumbnailWindowStart = -1;
    private int _thumbnailWindowEnd = -1;
    private int _thumbnailSelectedPage = -1;
    private const int ThumbnailRequestWidth = 220;
    private const int ThumbnailRequestHeight = 320;
    private const int MaxOverlayThumbnailItems = 36;
    private const int MaxPlainThumbnailItems = 80;
    private const double ThumbnailPreviewWidth = 118d;
    private const double ThumbnailPreviewHeight = 168d;
    private const float MinPressureSensitivity = 0.4f;
    private const float MaxPressureSensitivity = 2.0f;
    private bool _thumbnailIncludeInkOverlay = true;
    private float _pressureSensitivity = 1f;

    private sealed class ThumbnailStrokeSnapshot
    {
        public required DrawingStroke Stroke { get; init; }
        public required float LayerOpacity { get; init; }
        public required float MinX { get; init; }
        public required float MinY { get; init; }
        public required float MaxX { get; init; }
        public required float MaxY { get; init; }
    }
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
}
