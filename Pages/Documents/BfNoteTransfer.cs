using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Flow.PDFView.Abstractions;
using FlowNoteMauiApp.Models;
using SkiaSharp;

namespace FlowNoteMauiApp;

public partial class MainPage
{
    private const string BfNoteFormatId = "bfnote";
    private const int BfNoteVersion = 1;
    private const string BfNoteManifestEntryName = "manifest.json";
    private const string BfNotePdfEntryName = "document.pdf";
    private const string BfNoteInkStateEntryName = "ink-state.json";
    private const double OverlayExportRenderScale = 1.95d;
    private const int OverlayExportMinPixels = 720;
    private const int OverlayExportMaxPixels = 2500;

    private static readonly JsonSerializerOptions BfNoteJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private sealed class BfNoteManifest
    {
        public string Format { get; set; } = BfNoteFormatId;
        public int Version { get; set; } = BfNoteVersion;
        public string App { get; set; } = "FlowNoteMauiApp";
        public string NoteName { get; set; } = "Untitled";
        public string? SourceNoteId { get; set; }
        public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
        public int PageCount { get; set; }
        public bool HasInk { get; set; }
    }

    private sealed class BfNotePackage
    {
        public required BfNoteManifest Manifest { get; init; }
        public required byte[] PdfBytes { get; init; }
        public DrawingDocumentState? InkState { get; init; }
    }

    private async void OnExportBfNoteClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentNoteId))
        {
            ShowStatus(T("OpenPdfFirst", "Open a PDF first."));
            return;
        }

        try
        {
            await SaveCurrentDrawingStateAsync();
            var note = await _workspaceService.GetNoteAsync(_currentNoteId!);
            if (note is null)
            {
                ShowStatus(T("ExportFailed", "Export failed."));
                return;
            }

            var pdfBytes = await _workspaceService.GetPdfBytesAsync(note.Id);
            if (pdfBytes is null || pdfBytes.Length == 0)
            {
                ShowStatus(T("ExportFailed", "Export failed."));
                return;
            }

            var drawingState = await _drawingPersistenceService.LoadAsync(note.Id)
                ?? new DrawingDocumentState();
            var exportPath = Path.Combine(
                FileSystem.CacheDirectory,
                $"{SanitizeFileName(note.Name)}-{DateTime.UtcNow:yyyyMMddHHmmss}.bfnote");

            await WriteBfNotePackageAsync(exportPath, note, pdfBytes, drawingState);
            await ShareFileAsync(
                exportPath,
                T("ExportBfNote", "Export bfnote"));
            ShowStatus(T("BfNoteExported", "bfnote exported."));
        }
        catch (Exception ex)
        {
            ShowStatus($"{T("ExportFailed", "Export failed.")} {ex.Message}");
        }
    }

    private async void OnImportBfNoteClicked(object? sender, EventArgs e)
    {
        try
        {
            await SaveCurrentDrawingStateAsync();
            var pickOptions = new PickOptions
            {
                PickerTitle = T("ImportBfNote", "Import bfnote"),
                FileTypes = BuildBfNotePickerFileTypes()
            };
            var result = await FilePicker.Default.PickAsync(pickOptions);
            if (result is null)
                return;

            var package = await ReadBfNotePackageAsync(result);
            var noteFileName = package.Manifest.NoteName;
            if (string.IsNullOrWhiteSpace(noteFileName))
            {
                noteFileName = Path.GetFileNameWithoutExtension(result.FileName);
            }

            if (!noteFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                noteFileName += ".pdf";
            }

            var note = await _workspaceService.ImportPdfAsync(noteFileName, package.PdfBytes, _workspaceFolder);
            if (package.InkState is not null)
            {
                await _drawingPersistenceService.SaveAsync(note.Id, package.InkState);
            }

            QueuePrimeHomeCoverCache(note, package.PdfBytes);
            await RefreshWorkspaceViewsAsync();
            await OpenWorkspaceNoteAsync(note);
            ShowStatus(T("BfNoteImported", "bfnote imported."));
        }
        catch (Exception ex)
        {
            ShowStatus($"{T("ImportFailed", "Import failed.")} {ex.Message}");
        }
    }

    private async void OnExportOriginalPdfClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentNoteId))
        {
            ShowStatus(T("OpenPdfFirst", "Open a PDF first."));
            return;
        }

        try
        {
            var note = await _workspaceService.GetNoteAsync(_currentNoteId!);
            if (note is null)
            {
                ShowStatus(T("ExportFailed", "Export failed."));
                return;
            }

            var pdfBytes = await _workspaceService.GetPdfBytesAsync(note.Id);
            if (pdfBytes is null || pdfBytes.Length == 0)
            {
                ShowStatus(T("ExportFailed", "Export failed."));
                return;
            }

            var exportPath = Path.Combine(
                FileSystem.CacheDirectory,
                $"{SanitizeFileName(note.Name)}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
            await File.WriteAllBytesAsync(exportPath, pdfBytes);
            await ShareFileAsync(exportPath, T("ExportOriginalPdf", "Export Original PDF"));
            ShowStatus(T("OriginalPdfExported", "Original PDF exported."));
        }
        catch (Exception ex)
        {
            ShowStatus($"{T("ExportFailed", "Export failed.")} {ex.Message}");
        }
    }

    private async void OnExportOverlayPdfClicked(object? sender, EventArgs e)
    {
        if (!EnsurePdfLoaded(showHint: true) || string.IsNullOrWhiteSpace(_currentNoteId))
            return;

        try
        {
            await SaveCurrentDrawingStateAsync();
            var note = await _workspaceService.GetNoteAsync(_currentNoteId!);
            if (note is null)
            {
                ShowStatus(T("ExportFailed", "Export failed."));
                return;
            }

            var exportPath = Path.Combine(
                FileSystem.CacheDirectory,
                $"{SanitizeFileName(note.Name)}-overlay-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");
            var success = await ExportCurrentDocumentWithInkOverlayPdfAsync(exportPath, CancellationToken.None);
            if (!success)
            {
                ShowStatus(T("ExportFailed", "Export failed."));
                return;
            }

            await ShareFileAsync(exportPath, T("ExportOverlayPdf", "Export PDF + Notes"));
            ShowStatus(T("OverlayPdfExported", "Overlay PDF exported."));
        }
        catch (Exception ex)
        {
            ShowStatus($"{T("ExportFailed", "Export failed.")} {ex.Message}");
        }
    }

    private async Task WriteBfNotePackageAsync(
        string outputPath,
        WorkspaceNote note,
        byte[] pdfBytes,
        DrawingDocumentState drawingState)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var manifest = new BfNoteManifest
        {
            NoteName = note.Name,
            SourceNoteId = note.Id,
            ExportedAtUtc = DateTime.UtcNow,
            PageCount = Math.Max(0, _totalPageCount),
            HasInk = drawingState.Layers.Count > 0 && drawingState.Layers.Any(layer => layer.Strokes.Count > 0)
        };

        await using var stream = File.Create(outputPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        WriteZipEntry(archive, BfNoteManifestEntryName, JsonSerializer.Serialize(manifest, BfNoteJsonOptions));
        WriteZipEntry(archive, BfNotePdfEntryName, pdfBytes);
        WriteZipEntry(archive, BfNoteInkStateEntryName, JsonSerializer.Serialize(drawingState, BfNoteJsonOptions));
    }

    private async Task<BfNotePackage> ReadBfNotePackageAsync(FileResult result)
    {
        await using var source = await result.OpenReadAsync();
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);

        var pdfEntry = FindEntryIgnoreCase(archive, BfNotePdfEntryName)
            ?? throw new InvalidDataException("bfnote package is missing document.pdf.");
        var pdfBytes = await ReadZipEntryBytesAsync(pdfEntry);
        if (pdfBytes.Length == 0)
            throw new InvalidDataException("bfnote package contains empty PDF payload.");

        BfNoteManifest manifest;
        var manifestEntry = FindEntryIgnoreCase(archive, BfNoteManifestEntryName);
        if (manifestEntry is null)
        {
            manifest = new BfNoteManifest
            {
                NoteName = Path.GetFileNameWithoutExtension(result.FileName)
            };
        }
        else
        {
            var manifestJson = Encoding.UTF8.GetString(await ReadZipEntryBytesAsync(manifestEntry));
            manifest = JsonSerializer.Deserialize<BfNoteManifest>(manifestJson, BfNoteJsonOptions)
                ?? new BfNoteManifest
                {
                    NoteName = Path.GetFileNameWithoutExtension(result.FileName)
                };
        }

        DrawingDocumentState? inkState = null;
        var inkEntry = FindEntryIgnoreCase(archive, BfNoteInkStateEntryName);
        if (inkEntry is not null)
        {
            var inkJson = Encoding.UTF8.GetString(await ReadZipEntryBytesAsync(inkEntry));
            inkState = JsonSerializer.Deserialize<DrawingDocumentState>(inkJson, BfNoteJsonOptions);
        }

        return new BfNotePackage
        {
            Manifest = manifest,
            PdfBytes = pdfBytes,
            InkState = inkState
        };
    }

    private async Task<bool> ExportCurrentDocumentWithInkOverlayPdfAsync(string outputPath, CancellationToken token)
    {
        if (!EnsurePdfLoaded())
            return false;

        var drawingState = await MainThread
            .InvokeOnMainThreadAsync(() => DrawingCanvas.ExportState())
            .ConfigureAwait(false);
        var snapshots = BuildHomeCoverStrokeSnapshots(drawingState, maxPointsPerStroke: 360);
        if (_totalPageCount <= 0)
            return false;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        using var document = SKDocument.CreatePdf(outputPath);
        if (document is null)
            return false;

        for (var pageIndex = 0; pageIndex < _totalPageCount; pageIndex++)
        {
            token.ThrowIfCancellationRequested();
            var bounds = await GetCachedPageBoundsAsync(pageIndex).ConfigureAwait(false)
                ?? new PdfPageBounds(0d, 0d, 595d, 842d);

            var pageWidth = (float)Math.Max(1d, bounds.Width);
            var pageHeight = (float)Math.Max(1d, bounds.Height);
            var requestWidth = (int)Math.Clamp(
                Math.Round(pageWidth * OverlayExportRenderScale),
                OverlayExportMinPixels,
                OverlayExportMaxPixels);
            var requestHeight = (int)Math.Clamp(
                Math.Round(pageHeight * OverlayExportRenderScale),
                OverlayExportMinPixels,
                OverlayExportMaxPixels);

            byte[]? pageBytes = null;
            var stream = await PdfViewer.GetThumbnailAsync(pageIndex, requestWidth, requestHeight).ConfigureAwait(false);
            if (stream is not null)
            {
                using (stream)
                using (var memory = new MemoryStream())
                {
                    await stream.CopyToAsync(memory, token).ConfigureAwait(false);
                    if (memory.Length > 0)
                    {
                        pageBytes = memory.ToArray();
                    }
                }
            }

            if (pageBytes is { Length: > 0 } && snapshots.Count > 0)
            {
                var pageSnapshots = snapshots
                    .Where(snapshot => DoesSnapshotIntersectPage(snapshot, bounds))
                    .ToArray();
                if (pageSnapshots.Length > 0)
                {
                    var composed = ComposeThumbnailWithInkOverlay(pageBytes, bounds, pageSnapshots, token);
                    if (composed is { Length: > 0 })
                    {
                        pageBytes = composed;
                    }
                }
            }

            var pageCanvas = document.BeginPage(pageWidth, pageHeight);
            pageCanvas.Clear(SKColors.White);
            if (pageBytes is { Length: > 0 })
            {
                using var pageBitmap = CreateOpaquePdfBitmap(pageBytes);
                if (pageBitmap is not null && pageBitmap.Width > 0 && pageBitmap.Height > 0)
                {
                    pageCanvas.DrawBitmap(pageBitmap, new SKRect(0f, 0f, pageWidth, pageHeight));
                }
            }
            document.EndPage();
        }

        document.Close();
        return true;
    }

    private static SKBitmap? CreateOpaquePdfBitmap(byte[] pageBytes)
    {
        using var decoded = SKBitmap.Decode(pageBytes);
        if (decoded is null || decoded.Width <= 0 || decoded.Height <= 0)
            return null;

        var info = new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var surface = SKSurface.Create(info);
        if (surface is null)
            return decoded.Copy();

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(decoded, 0, 0);
        canvas.Flush();

        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static FilePickerFileType BuildBfNotePickerFileTypes()
    {
        return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.iOS, new[] { "public.data", "public.zip-archive" } },
            { DevicePlatform.MacCatalyst, new[] { "public.data", "public.zip-archive" } },
            { DevicePlatform.Android, new[] { "application/zip", "application/octet-stream" } },
            { DevicePlatform.WinUI, new[] { ".bfnote", ".zip" } }
        });
    }

    private static ZipArchiveEntry? FindEntryIgnoreCase(ZipArchive archive, string entryName)
    {
        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase));
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: false);
        writer.Write(content);
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, byte[] content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static async Task<byte[]> ReadZipEntryBytesAsync(ZipArchiveEntry entry)
    {
        await using var stream = entry.Open();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "FlowNote";

        var safe = input.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(safe) ? "FlowNote" : safe;
    }

    private static Task ShareFileAsync(string path, string title)
    {
        return Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(path)
        });
    }
}
