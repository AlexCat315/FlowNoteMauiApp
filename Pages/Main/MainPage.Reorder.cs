namespace FlowNoteMauiApp;

public partial class MainPage
{
    private const double TabReorderPanThreshold = 64d;
    private const double ToolReorderPanThreshold = 36d;
    private bool _inkToolReorderWired;
    private ImageButton? _draggingInkToolButton;
    private string? _draggingEditorTabNoteId;
    private readonly Dictionary<string, BoxView> _editorTabDropIndicators = new(StringComparer.Ordinal);
    private string? _tabDropTargetNoteId;
    private bool _tabDropInsertAfter;

    private void WireInkToolReorderGestures()
    {
        if (_inkToolReorderWired)
            return;

        _inkToolReorderWired = true;

        var reorderableTools = new[]
        {
            PenModeButton,
            HighlighterButton,
            PencilButton,
            MarkerButton,
            EraserButton
        };

        foreach (var toolButton in reorderableTools)
        {
            AttachInkToolDragReorder(toolButton);
        }
    }

    private void AttachInkToolDragReorder(ImageButton toolButton)
    {
        var dragGesture = new DragGestureRecognizer();
        dragGesture.CanDrag = true;
        dragGesture.DragStarting += (_, _) => _draggingInkToolButton = toolButton;
        dragGesture.DropCompleted += (_, _) => _draggingInkToolButton = null;
        toolButton.GestureRecognizers.Add(dragGesture);

        var dropGesture = new DropGestureRecognizer();
        dropGesture.AllowDrop = true;
        var baseBorderColor = toolButton.BorderColor;
        var baseBorderWidth = toolButton.BorderWidth;
        void RestoreToolDropVisual()
        {
            toolButton.BorderColor = baseBorderColor;
            toolButton.BorderWidth = baseBorderWidth;
        }

        dropGesture.DragOver += (_, _) =>
        {
            if (_draggingInkToolButton is null || ReferenceEquals(_draggingInkToolButton, toolButton))
                return;

            toolButton.BorderColor = Palette.ModeButtonExpandedBorder;
            toolButton.BorderWidth = 2;
        };
        dropGesture.DragLeave += (_, _) => RestoreToolDropVisual();
        dropGesture.Drop += (_, _) =>
        {
            var source = _draggingInkToolButton;
            _draggingInkToolButton = null;
            RestoreToolDropVisual();
            if (source is null)
                return;

            TryReorderInkToolButtons(source, toolButton);
        };
        toolButton.GestureRecognizers.Add(dropGesture);

        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += (_, e) =>
        {
            if (e.StatusType != GestureStatus.Completed)
                return;

            if (Math.Abs(e.TotalX) < ToolReorderPanThreshold || Math.Abs(e.TotalX) < Math.Abs(e.TotalY) * 1.4d)
                return;

            TryShiftInkToolButton(toolButton, e.TotalX > 0 ? 1 : -1);
        };
        toolButton.GestureRecognizers.Add(panGesture);
    }

    private bool TryReorderInkToolButtons(ImageButton sourceButton, ImageButton targetButton)
    {
        if (ReferenceEquals(sourceButton, targetButton))
            return false;

        var children = InkToolsHost.Children;
        var sourceIndex = children.IndexOf(sourceButton);
        var targetIndex = children.IndexOf(targetButton);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            return false;

        (children[sourceIndex], children[targetIndex]) = (children[targetIndex], children[sourceIndex]);
        if (DrawingToolbarPanel.IsVisible)
        {
            PositionDrawingToolbarPanelUnderTool(_activeInkTool);
        }

        return true;
    }

    private bool TryShiftInkToolButton(ImageButton sourceButton, int delta)
    {
        var children = InkToolsHost.Children;
        var sourceIndex = children.IndexOf(sourceButton);
        if (sourceIndex < 0)
            return false;

        var targetIndex = Math.Clamp(sourceIndex + delta, 0, children.Count - 1);
        if (targetIndex == sourceIndex)
            return false;

        (children[sourceIndex], children[targetIndex]) = (children[targetIndex], children[sourceIndex]);
        if (DrawingToolbarPanel.IsVisible)
        {
            PositionDrawingToolbarPanelUnderTool(_activeInkTool);
        }

        return true;
    }

    private void AttachEditorTabDragReorder(Border tabBorder, string noteId)
    {
        var dragGesture = new DragGestureRecognizer();
        dragGesture.CanDrag = true;
        dragGesture.DragStarting += (_, _) =>
        {
            _draggingEditorTabNoteId = noteId;
            ClearEditorTabDropIndicators();
        };
        dragGesture.DropCompleted += (_, _) =>
        {
            _draggingEditorTabNoteId = null;
            ClearEditorTabDropIndicators();
        };
        tabBorder.GestureRecognizers.Add(dragGesture);

        var dropGesture = new DropGestureRecognizer();
        dropGesture.AllowDrop = true;
        var baseStroke = tabBorder.Stroke;
        var baseThickness = tabBorder.StrokeThickness;
        void RestoreTabDropVisual()
        {
            tabBorder.Stroke = baseStroke;
            tabBorder.StrokeThickness = baseThickness;
        }

        dropGesture.DragOver += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(_draggingEditorTabNoteId)
                || string.Equals(_draggingEditorTabNoteId, noteId, StringComparison.Ordinal))
            {
                return;
            }

            _tabDropTargetNoteId = noteId;
            _tabDropInsertAfter = ResolveInsertAfter(e, tabBorder);
            ShowEditorTabDropIndicator(noteId, _tabDropInsertAfter);
            tabBorder.Stroke = Palette.ModeButtonExpandedBorder;
            tabBorder.StrokeThickness = 2;
        };
        dropGesture.DragLeave += (_, _) =>
        {
            RestoreTabDropVisual();
            ClearEditorTabDropIndicators();
        };
        dropGesture.Drop += (_, e) =>
        {
            var sourceNoteId = _draggingEditorTabNoteId;
            _draggingEditorTabNoteId = null;
            RestoreTabDropVisual();
            if (string.IsNullOrWhiteSpace(sourceNoteId))
                return;

            var insertAfter = string.Equals(_tabDropTargetNoteId, noteId, StringComparison.Ordinal)
                ? _tabDropInsertAfter
                : ResolveInsertAfter(e, tabBorder);
            TryReorderEditorTabs(sourceNoteId, noteId, insertAfter);
            ClearEditorTabDropIndicators();
        };
        tabBorder.GestureRecognizers.Add(dropGesture);

        var panGesture = new PanGestureRecognizer();
        panGesture.PanUpdated += (_, e) =>
        {
            if (e.StatusType != GestureStatus.Completed)
                return;

            if (Math.Abs(e.TotalX) < TabReorderPanThreshold || Math.Abs(e.TotalX) < Math.Abs(e.TotalY) * 1.4d)
                return;

            TryShiftEditorTab(noteId, e.TotalX > 0 ? 1 : -1);
        };
        tabBorder.GestureRecognizers.Add(panGesture);
    }

    private static bool ResolveInsertAfter(DragEventArgs args, Border tabBorder)
    {
        return ResolveInsertAfter(args.GetPosition(tabBorder), tabBorder);
    }

    private static bool ResolveInsertAfter(DropEventArgs args, Border tabBorder)
    {
        return ResolveInsertAfter(args.GetPosition(tabBorder), tabBorder);
    }

    private static bool ResolveInsertAfter(Point? point, Border tabBorder)
    {
        if (point is null)
            return false;

        var width = tabBorder.Width > 1 ? tabBorder.Width : tabBorder.WidthRequest;
        if (width <= 0)
            return false;

        return point.Value.X > (width / 2d);
    }

    private void ShowEditorTabDropIndicator(string noteId, bool insertAfter)
    {
        foreach (var (id, indicator) in _editorTabDropIndicators)
        {
            if (!string.Equals(id, noteId, StringComparison.Ordinal))
            {
                indicator.IsVisible = false;
                continue;
            }

            indicator.HorizontalOptions = insertAfter ? LayoutOptions.End : LayoutOptions.Start;
            indicator.IsVisible = true;
        }
    }

    private void ClearEditorTabDropIndicators()
    {
        foreach (var indicator in _editorTabDropIndicators.Values)
        {
            indicator.IsVisible = false;
        }

        _tabDropTargetNoteId = null;
        _tabDropInsertAfter = false;
    }

    private bool TryReorderEditorTabs(string sourceNoteId, string targetNoteId, bool insertAfter)
    {
        if (string.IsNullOrWhiteSpace(sourceNoteId)
            || string.IsNullOrWhiteSpace(targetNoteId)
            || string.Equals(sourceNoteId, targetNoteId, StringComparison.Ordinal))
        {
            return false;
        }

        var sourceIndex = _editorTabs.FindIndex(t => string.Equals(t.NoteId, sourceNoteId, StringComparison.Ordinal));
        var targetIndex = _editorTabs.FindIndex(t => string.Equals(t.NoteId, targetNoteId, StringComparison.Ordinal));
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            return false;

        var movingTab = _editorTabs[sourceIndex];
        _editorTabs.RemoveAt(sourceIndex);

        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        var insertIndex = insertAfter ? targetIndex + 1 : targetIndex;
        insertIndex = Math.Clamp(insertIndex, 0, _editorTabs.Count);
        _editorTabs.Insert(insertIndex, movingTab);
        RefreshEditorTabsVisual();
        return true;
    }

    private bool TryShiftEditorTab(string sourceNoteId, int delta)
    {
        if (string.IsNullOrWhiteSpace(sourceNoteId))
            return false;

        var sourceIndex = _editorTabs.FindIndex(t => string.Equals(t.NoteId, sourceNoteId, StringComparison.Ordinal));
        if (sourceIndex < 0)
            return false;

        var targetIndex = Math.Clamp(sourceIndex + delta, 0, _editorTabs.Count - 1);
        if (targetIndex == sourceIndex)
            return false;

        (_editorTabs[sourceIndex], _editorTabs[targetIndex]) = (_editorTabs[targetIndex], _editorTabs[sourceIndex]);
        RefreshEditorTabsVisual();
        return true;
    }
}
