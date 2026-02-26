namespace FlowNoteMauiApp;

public partial class MainPage
{
    private const double TabReorderPanThreshold = 64d;
    private const double ToolReorderPanThreshold = 36d;
    private bool _inkToolReorderWired;
    private ImageButton? _draggingInkToolButton;
    private string? _draggingEditorTabNoteId;

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
        dropGesture.Drop += (_, _) =>
        {
            var source = _draggingInkToolButton;
            _draggingInkToolButton = null;
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

        children.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        children.Insert(targetIndex, sourceButton);
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

        children.RemoveAt(sourceIndex);
        if (targetIndex > sourceIndex)
            targetIndex--;

        children.Insert(targetIndex, sourceButton);
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
        dragGesture.DragStarting += (_, _) => _draggingEditorTabNoteId = noteId;
        dragGesture.DropCompleted += (_, _) => _draggingEditorTabNoteId = null;
        tabBorder.GestureRecognizers.Add(dragGesture);

        var dropGesture = new DropGestureRecognizer();
        dropGesture.AllowDrop = true;
        dropGesture.Drop += (_, _) =>
        {
            var sourceNoteId = _draggingEditorTabNoteId;
            _draggingEditorTabNoteId = null;
            if (string.IsNullOrWhiteSpace(sourceNoteId))
                return;

            TryReorderEditorTabs(sourceNoteId, noteId);
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

    private bool TryReorderEditorTabs(string sourceNoteId, string targetNoteId)
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

        var sourceTab = _editorTabs[sourceIndex];
        _editorTabs.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        _editorTabs.Insert(targetIndex, sourceTab);
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

        var sourceTab = _editorTabs[sourceIndex];
        _editorTabs.RemoveAt(sourceIndex);
        if (targetIndex > sourceIndex)
            targetIndex--;

        _editorTabs.Insert(targetIndex, sourceTab);
        RefreshEditorTabsVisual();
        return true;
    }
}
