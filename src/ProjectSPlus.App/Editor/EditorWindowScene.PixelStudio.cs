using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Themes;
using Silk.NET.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ProjectSPlus.App.Editor;

public sealed partial class EditorWindowScene
{
    private const string DefaultPaletteSelectionId = "__default_palette__";
    private const string PixelStudioCameraLogPrefix = "[PixelStudioCamera]";

    private enum PixelStudioDragMode
    {
        None,
        PanCanvas,
        AdjustBrushSize,
        MoveToolSettingsDock,
        ResizeToolsPanel,
        ResizeSidebar
    }

    private static readonly JsonSerializerOptions PixelStudioDocumentSerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly Stack<PixelStudioState> _pixelUndoStack = [];
    private readonly Stack<PixelStudioState> _pixelRedoStack = [];

    private PixelStudioState? _strokeSnapshot;
    private bool _isPixelStrokeActive;
    private bool _strokeChanged;
    private int _lastStrokeCellIndex = -1;
    private long _lastPlaybackTick;
    private int? _contextPaletteIndex;
    private int? _contextLayerIndex;
    private int? _contextFrameIndex;
    private bool _contextSelectionActive;
    private string? _currentPixelDocumentPath;
    private float _pixelPanDragStartMouseX;
    private float _pixelPanDragStartMouseY;
    private float _pixelPanDragStartX;
    private float _pixelPanDragStartY;
    private float _toolSettingsDragStartMouseX;
    private float _toolSettingsDragStartMouseY;
    private float _toolSettingsDragStartOffsetX;
    private float _toolSettingsDragStartOffsetY;
    private int _strokeAnchorCellIndex = -1;
    private bool _frameRenameActive;
    private string _frameRenameBuffer = string.Empty;
    private bool _selectionActive;
    private bool _selectionCommitted;
    private bool _selectionDragActive;
    private int _selectionStartX;
    private int _selectionStartY;
    private int _selectionEndX;
    private int _selectionEndY;

    private bool HandlePixelStudioKeyDown(IKeyboard keyboard, Key key)
    {
        bool controlPressed = keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight);
        if (controlPressed && key == Key.Z)
        {
            UndoPixelStudio();
            return true;
        }

        if (controlPressed && key == Key.Y)
        {
            RedoPixelStudio();
            return true;
        }

        if (key == Key.Space)
        {
            ExecutePixelStudioAction(PixelStudioAction.TogglePlayback);
            return true;
        }

        return false;
    }

    private void UpdatePixelStudioPlayback()
    {
        if (!_pixelStudio.IsPlaying || _pixelStudio.Frames.Count <= 1)
        {
            _lastPlaybackTick = 0;
            return;
        }

        long currentTicks = Stopwatch.GetTimestamp();
        if (_lastPlaybackTick == 0)
        {
            _lastPlaybackTick = currentTicks;
            return;
        }

        double elapsedMilliseconds = (currentTicks - _lastPlaybackTick) * 1000.0 / Stopwatch.Frequency;
        double frameDuration = 1000.0 / Math.Max(_pixelStudio.FramesPerSecond, 1);
        if (elapsedMilliseconds < frameDuration)
        {
            return;
        }

        int frameSteps = Math.Max(1, (int)(elapsedMilliseconds / frameDuration));
        _pixelStudio.PreviewFrameIndex = (_pixelStudio.PreviewFrameIndex + frameSteps) % _pixelStudio.Frames.Count;
        _lastPlaybackTick = currentTicks;
        RefreshPixelStudioView();
    }

    private bool HandlePixelStudioMouse(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, MouseButton button)
    {
        if (button == MouseButton.Right)
        {
            if (layout.ContextMenuRect is not null && layout.ContextMenuRect.Value.Contains(mouseX, mouseY))
            {
                return true;
            }

            IndexedRect? savedPaletteRow = layout.SavedPaletteRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (savedPaletteRow is not null && savedPaletteRow.Index >= 0)
            {
                SelectAndApplySavedPalette(savedPaletteRow.Index);
                OpenPaletteContextMenu(savedPaletteRow.Index, mouseX, mouseY);
                return true;
            }

            IndexedRect? layerRow = layout.LayerRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (layerRow is not null)
            {
                StopPixelPlayback();
                _pixelStudio.ActiveLayerIndex = Math.Clamp(layerRow.Index, 0, CurrentPixelFrame.Layers.Count - 1);
                OpenLayerContextMenu(layerRow.Index, mouseX, mouseY);
                return true;
            }

            IndexedRect? frameContextRow = layout.FrameRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (frameContextRow is not null)
            {
                StopPixelPlayback();
                _pixelStudio.ActiveFrameIndex = Math.Clamp(frameContextRow.Index, 0, _pixelStudio.Frames.Count - 1);
                _pixelStudio.PreviewFrameIndex = _pixelStudio.ActiveFrameIndex;
                OpenFrameContextMenu(frameContextRow.Index, mouseX, mouseY);
                return true;
            }

            if (_selectionActive && layout.CanvasClipRect.Contains(mouseX, mouseY))
            {
                OpenSelectionContextMenu(mouseX, mouseY);
                return true;
            }

            ClosePixelContextMenu();
            return false;
        }

        if (button == MouseButton.Middle)
        {
            if (!layout.CanvasClipRect.Contains(mouseX, mouseY))
            {
                return false;
            }

            ClosePixelContextMenu();
            _pixelDragMode = PixelStudioDragMode.PanCanvas;
            _pixelPanDragStartMouseX = mouseX;
            _pixelPanDragStartMouseY = mouseY;
            _pixelPanDragStartX = _pixelStudio.CanvasPanX;
            _pixelPanDragStartY = _pixelStudio.CanvasPanY;
            return true;
        }

        if (button != MouseButton.Left)
        {
            return false;
        }

        if (layout.ContextMenuRect is not null)
        {
            ActionRect<PixelStudioContextMenuAction>? contextButton = layout.ContextMenuButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (contextButton is not null)
            {
                ExecutePixelStudioContextMenuAction(contextButton.Action);
                return true;
            }

            if (!layout.ContextMenuRect.Value.Contains(mouseX, mouseY))
            {
                ClosePixelContextMenu();
                RefreshPixelStudioView("Closed context menu.", rebuildLayout: true);
                return true;
            }
        }

        if (layout.LeftCollapseHandleRect.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            TogglePixelToolsCollapse();
            return true;
        }

        if (layout.RightCollapseHandleRect.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            TogglePixelSidebarCollapse();
            return true;
        }

        if (layout.LeftSplitterRect.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            _pixelDragMode = PixelStudioDragMode.ResizeToolsPanel;
            return true;
        }

        if (layout.RightSplitterRect.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            _pixelDragMode = PixelStudioDragMode.ResizeSidebar;
            return true;
        }

        if (layout.BrushSizeSliderRect is not null && layout.BrushSizeSliderRect.Value.Contains(mouseX, mouseY))
        {
            SetBrushSizeFromSlider(layout.BrushSizeSliderRect.Value, mouseY);
            _pixelDragMode = PixelStudioDragMode.AdjustBrushSize;
            return true;
        }

        if (layout.ToolSettingsPanelRect.Width > 40f && layout.ToolSettingsPanelRect.Contains(mouseX, mouseY))
        {
            _pixelDragMode = PixelStudioDragMode.MoveToolSettingsDock;
            _toolSettingsDragStartMouseX = mouseX;
            _toolSettingsDragStartMouseY = mouseY;
            _toolSettingsDragStartOffsetX = float.IsFinite(_pixelToolSettingsPanelOffsetX) ? _pixelToolSettingsPanelOffsetX : layout.ToolSettingsPanelRect.X - layout.CanvasClipRect.X;
            _toolSettingsDragStartOffsetY = float.IsFinite(_pixelToolSettingsPanelOffsetY) ? _pixelToolSettingsPanelOffsetY : layout.ToolSettingsPanelRect.Y - layout.CanvasClipRect.Y;
            return true;
        }

        ActionRect<PixelStudioToolKind>? toolButton = layout.ToolButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (toolButton is not null)
        {
            ClosePixelContextMenu();
            ExecutePixelStudioTool(toolButton.Action);
            return true;
        }

        if (TryHandlePixelAction(layout.DocumentButtons, mouseX, mouseY)
            || TryHandlePixelAction(layout.CanvasButtons, mouseX, mouseY)
            || TryHandlePixelAction(layout.ToolSettingsButtons, mouseX, mouseY)
            || TryHandlePixelAction(layout.PaletteButtons, mouseX, mouseY)
            || TryHandlePixelAction(layout.LayerButtons, mouseX, mouseY)
            || TryHandlePixelAction(layout.TimelineButtons, mouseX, mouseY))
        {
            return true;
        }

        if (layout.PalettePromptRect is not null && TryHandlePixelAction(layout.PalettePromptButtons, mouseX, mouseY))
        {
            return true;
        }

        if (layout.PaletteLibraryRect is not null)
        {
            if (TryHandlePixelAction(layout.PaletteLibraryButtons, mouseX, mouseY))
            {
                return true;
            }

            IndexedRect? savedPaletteRow = layout.SavedPaletteRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (savedPaletteRow is not null)
            {
                SelectAndApplySavedPalette(savedPaletteRow.Index);
                return true;
            }
        }

        IndexedRect? paletteSwatch = layout.PaletteSwatches.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (paletteSwatch is not null)
        {
            _pixelStudio.ActivePaletteIndex = Math.Clamp(paletteSwatch.Index, 0, _pixelStudio.Palette.Count - 1);
            ClosePixelContextMenu();
            RefreshPixelStudioView($"{EditorBranding.PixelToolName} color set to {BuildPixelStudioViewState().ActiveColorHex}.");
            return true;
        }

        IndexedRect? visibilityButton = layout.LayerVisibilityButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (visibilityButton is not null)
        {
            ClosePixelContextMenu();
            TogglePixelLayerVisibility(visibilityButton.Index);
            return true;
        }

        IndexedRect? layerRowHit = layout.LayerRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (layerRowHit is not null)
        {
            StopPixelPlayback();
            _pixelStudio.ActiveLayerIndex = Math.Clamp(layerRowHit.Index, 0, CurrentPixelFrame.Layers.Count - 1);
            ClosePixelContextMenu();
            RefreshPixelStudioView($"{EditorBranding.PixelToolName} layer selected: {CurrentPixelFrame.Layers[_pixelStudio.ActiveLayerIndex].Name}.");
            return true;
        }

        IndexedRect? frameRow = layout.FrameRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (frameRow is not null)
        {
            StopPixelPlayback();
            _pixelStudio.ActiveFrameIndex = Math.Clamp(frameRow.Index, 0, _pixelStudio.Frames.Count - 1);
            _pixelStudio.PreviewFrameIndex = _pixelStudio.ActiveFrameIndex;
            _pixelStudio.ActiveLayerIndex = Math.Min(_pixelStudio.ActiveLayerIndex, CurrentPixelFrame.Layers.Count - 1);
            ClosePixelContextMenu();
            RefreshPixelStudioView($"{EditorBranding.PixelToolName} frame selected: {CurrentPixelFrame.Name}.");
            return true;
        }

        if (TryGetCanvasCellIndex(layout, mouseX, mouseY, out int cellIndex))
        {
            return HandlePixelCanvasPress(cellIndex);
        }

        return false;
    }

    private void HandlePixelStudioMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            bool wasBrushSizeDrag = _pixelDragMode == PixelStudioDragMode.AdjustBrushSize;
            bool wasToolSettingsDrag = _pixelDragMode == PixelStudioDragMode.MoveToolSettingsDock;
            _pixelDragMode = PixelStudioDragMode.None;
            if (_pixelStudio.ActiveTool == PixelStudioToolKind.Select)
            {
                CommitSelection();
            }
            EndPixelStroke();
            if (wasBrushSizeDrag)
            {
                RefreshPixelStudioView($"Brush size set to {_pixelStudio.BrushSize}px.");
                return;
            }

            if (wasToolSettingsDrag)
            {
                RefreshPixelStudioInteraction(rebuildLayout: true);
            }

            return;
        }

        if (button == MouseButton.Middle && _pixelDragMode == PixelStudioDragMode.PanCanvas)
        {
            _pixelDragMode = PixelStudioDragMode.None;
            LogCurrentCanvasCamera("PanComplete");
            RefreshPixelStudioView("Canvas pan set.", rebuildLayout: true);
        }
    }

    private void HandlePixelStudioMouseMove(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (_pixelDragMode == PixelStudioDragMode.PanCanvas)
        {
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.AdjustBrushSize && layout.BrushSizeSliderRect is not null)
        {
            SetBrushSizeFromSlider(layout.BrushSizeSliderRect.Value, mouseY);
            RefreshPixelStudioInteraction();
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.MoveToolSettingsDock)
        {
            MoveToolSettingsPanel(layout, mouseX, mouseY);
            return;
        }

        if (_pixelStudio.ActiveTool == PixelStudioToolKind.Select)
        {
            if (TryGetCanvasCellIndex(layout, mouseX, mouseY, out int selectionCellIndex))
            {
                UpdateSelection(selectionCellIndex);
            }

            return;
        }

        if (!_isPixelStrokeActive || !TryGetCanvasCellIndex(layout, mouseX, mouseY, out int cellIndex))
        {
            return;
        }

        ApplyStrokePath(cellIndex);
    }

    private bool HandlePixelStudioLayoutDrag(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (_pixelDragMode == PixelStudioDragMode.None)
        {
            return false;
        }

        switch (_pixelDragMode)
        {
            case PixelStudioDragMode.PanCanvas:
                _pixelStudio.CanvasPanX = _pixelPanDragStartX + (mouseX - _pixelPanDragStartMouseX);
                _pixelStudio.CanvasPanY = _pixelPanDragStartY + (mouseY - _pixelPanDragStartMouseY);
                RefreshPixelStudioPanLayout();
                return true;
            case PixelStudioDragMode.MoveToolSettingsDock:
                MoveToolSettingsPanel(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.ResizeToolsPanel:
                _pixelToolsCollapsed = false;
                _pixelToolsPanelWidth = Math.Clamp(mouseX - layout.ToolbarRect.X, 132, 280);
                _previousPixelToolsPanelWidth = _pixelToolsPanelWidth;
                SyncUiState("Resizing tools panel.");
                return true;
            case PixelStudioDragMode.ResizeSidebar:
                _pixelSidebarCollapsed = false;
                _pixelSidebarWidth = Math.Clamp((layout.PalettePanelRect.X + layout.PalettePanelRect.Width) - mouseX, 248, 420);
                _previousPixelSidebarWidth = _pixelSidebarWidth;
                SyncUiState("Resizing sidebar.");
                return true;
            default:
                return false;
        }
    }

    private void HandlePixelStudioScroll(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, ScrollWheel scrollWheel)
    {
        int direction = scrollWheel.Y < 0 ? 1 : scrollWheel.Y > 0 ? -1 : 0;
        if (direction == 0)
        {
            return;
        }

        if (layout.CanvasClipRect.Contains(mouseX, mouseY))
        {
            int previousZoom = _pixelStudio.DesiredZoom;
            _pixelStudio.DesiredZoom = direction > 0
                ? PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom - 2)
                : PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom + 2);

            if (_pixelStudio.DesiredZoom != previousZoom)
            {
                ClosePixelContextMenu();
                RefreshPixelStudioView($"{EditorBranding.PixelToolName} zoom target set to {_pixelStudio.DesiredZoom}x.", rebuildLayout: true);
            }

            return;
        }

        if (layout.PaletteSwatchViewportRect is not null && layout.PaletteSwatchViewportRect.Value.Contains(mouseX, mouseY))
        {
            _paletteSwatchScrollRow = Math.Max(_paletteSwatchScrollRow + direction, 0);
            RefreshPixelStudioView("Scrolling palette colors.", rebuildLayout: true);
            return;
        }

        if (layout.SavedPaletteViewportRect is not null && layout.SavedPaletteViewportRect.Value.Contains(mouseX, mouseY))
        {
            _savedPaletteScrollRow = Math.Max(_savedPaletteScrollRow + direction, 0);
            RefreshPixelStudioView("Scrolling saved palettes.", rebuildLayout: true);
            return;
        }

        if (layout.LayerListViewportRect is not null && layout.LayerListViewportRect.Value.Contains(mouseX, mouseY))
        {
            _layerScrollRow = Math.Max(_layerScrollRow + direction, 0);
            RefreshPixelStudioView("Scrolling layers.", rebuildLayout: true);
            return;
        }

        if (layout.FrameListViewportRect is not null && layout.FrameListViewportRect.Value.Contains(mouseX, mouseY))
        {
            _frameScrollRow = Math.Max(_frameScrollRow + direction, 0);
            RefreshPixelStudioView("Scrolling frames.", rebuildLayout: true);
        }
    }

    private bool TryHandlePixelAction(IReadOnlyList<ActionRect<PixelStudioAction>> buttons, float mouseX, float mouseY)
    {
        ActionRect<PixelStudioAction>? button = buttons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (button is null)
        {
            return false;
        }

        ClosePixelContextMenu();
        ExecutePixelStudioAction(button.Action);
        return true;
    }

    private bool TryGetCanvasCellIndex(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, out int cellIndex)
    {
        cellIndex = -1;
        if (!layout.CanvasClipRect.Contains(mouseX, mouseY))
        {
            return false;
        }

        int cellSize = Math.Max(layout.CanvasCellSize, 1);
        int x = (int)((mouseX - layout.CanvasViewportRect.X) / cellSize);
        int y = (int)((mouseY - layout.CanvasViewportRect.Y) / cellSize);
        if (x < 0 || y < 0 || x >= _pixelStudio.CanvasWidth || y >= _pixelStudio.CanvasHeight)
        {
            return false;
        }

        cellIndex = (y * _pixelStudio.CanvasWidth) + x;
        return true;
    }

    private bool HandlePixelCanvasPress(int cellIndex)
    {
        StopPixelPlayback();
        if (CurrentPixelLayer.IsLocked)
        {
            RefreshPixelStudioView("Unlock the active layer before editing.");
            return true;
        }

        switch (_pixelStudio.ActiveTool)
        {
            case PixelStudioToolKind.Select:
                BeginSelection(cellIndex);
                return true;
            case PixelStudioToolKind.Pencil:
            case PixelStudioToolKind.Eraser:
                BeginPixelStroke();
                ApplyStrokePath(cellIndex);
                return true;
            case PixelStudioToolKind.Line:
                BeginPixelStroke();
                _strokeAnchorCellIndex = cellIndex;
                return true;
            case PixelStudioToolKind.Fill:
                ApplyPixelStudioChange(
                    $"Filled region with {BuildPixelStudioViewState().ActiveColorHex}.",
                    () => FloodFillCell(cellIndex, _pixelStudio.ActivePaletteIndex),
                    rebuildLayout: false);
                return true;
            case PixelStudioToolKind.Picker:
                int pickedPaletteIndex = GetTopVisiblePaletteIndex(cellIndex);
                if (pickedPaletteIndex >= 0)
                {
                    _pixelStudio.ActivePaletteIndex = pickedPaletteIndex;
                    RefreshPixelStudioView($"Picked color {BuildPixelStudioViewState().ActiveColorHex}.");
                }

                return true;
            default:
                return false;
        }
    }

    private void BeginPixelStroke()
    {
        if (_isPixelStrokeActive)
        {
            return;
        }

        _strokeSnapshot = ClonePixelStudioState(_pixelStudio);
        _isPixelStrokeActive = true;
        _strokeChanged = false;
        _lastStrokeCellIndex = -1;
        _strokeAnchorCellIndex = -1;
    }

    private void ApplyStrokeCell(int cellIndex)
    {
        if (!_isPixelStrokeActive || cellIndex == _lastStrokeCellIndex || cellIndex < 0 || cellIndex >= CurrentPixelLayer.Pixels.Length)
        {
            return;
        }

        ApplyBrushStamp(cellIndex);
    }

    private void ApplyStrokePath(int targetCellIndex)
    {
        if (!_isPixelStrokeActive || targetCellIndex < 0 || targetCellIndex >= CurrentPixelLayer.Pixels.Length)
        {
            return;
        }

        if (_lastStrokeCellIndex < 0)
        {
            ApplyStrokeCell(targetCellIndex);
        }
        else
        {
            foreach (int cellIndex in EnumerateStrokeCells(_lastStrokeCellIndex, targetCellIndex))
            {
                ApplyStrokeCell(cellIndex);
            }
        }

        if (_strokeChanged)
        {
            RefreshPixelStudioPixels();
        }
    }

    private void ApplyBrushStamp(int centerCellIndex)
    {
        int radius = Math.Max(_pixelStudio.BrushSize - 1, 0) / 2;
        int centerX = centerCellIndex % _pixelStudio.CanvasWidth;
        int centerY = centerCellIndex / _pixelStudio.CanvasWidth;
        int nextValue = _pixelStudio.ActiveTool == PixelStudioToolKind.Eraser
            ? -1
            : _pixelStudio.ActivePaletteIndex;

        for (int offsetY = -radius; offsetY <= radius; offsetY++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                if ((_pixelStudio.BrushSize > 1) && ((offsetX * offsetX) + (offsetY * offsetY) > ((radius + 0.5f) * (radius + 0.5f))))
                {
                    continue;
                }

                int x = centerX + offsetX;
                int y = centerY + offsetY;
                if (!IsWithinCanvas(x, y) || !IsWithinSelection(x, y))
                {
                    continue;
                }

                int cellIndex = (y * _pixelStudio.CanvasWidth) + x;
                if (CurrentPixelLayer.Pixels[cellIndex] == nextValue)
                {
                    continue;
                }

                CurrentPixelLayer.Pixels[cellIndex] = nextValue;
                _strokeChanged = true;
            }
        }

        _lastStrokeCellIndex = centerCellIndex;
    }

    private void BeginSelection(int cellIndex)
    {
        int x = cellIndex % _pixelStudio.CanvasWidth;
        int y = cellIndex / _pixelStudio.CanvasWidth;

        if (_selectionCommitted && !_selectionDragActive && IsWithinCurrentSelection(x, y))
        {
            RefreshPixelStudioInteraction();
            return;
        }

        _selectionActive = true;
        _selectionCommitted = false;
        _selectionDragActive = true;
        _selectionStartX = x;
        _selectionStartY = y;
        _selectionEndX = x;
        _selectionEndY = y;
        RefreshPixelStudioInteraction();
    }

    private void UpdateSelection(int cellIndex)
    {
        if (!_selectionActive || !_selectionDragActive)
        {
            return;
        }

        _selectionEndX = cellIndex % _pixelStudio.CanvasWidth;
        _selectionEndY = cellIndex / _pixelStudio.CanvasWidth;
        RefreshPixelStudioInteraction();
    }

    private void CommitSelection()
    {
        if (!_selectionActive || !_selectionDragActive)
        {
            return;
        }

        _selectionDragActive = false;
        _selectionCommitted = true;
        RefreshPixelStudioView($"Selection set to {GetSelectionWidth()}x{GetSelectionHeight()}.");
    }

    private void EndPixelStroke()
    {
        if (!_isPixelStrokeActive)
        {
            return;
        }

        if (_pixelStudio.ActiveTool == PixelStudioToolKind.Line && _strokeAnchorCellIndex >= 0 && _lastStrokeCellIndex >= 0 && _strokeSnapshot is not null)
        {
            RestorePixelStudioState(_strokeSnapshot);
            _strokeChanged = false;
            foreach (int cellIndex in EnumerateStrokeCells(_strokeAnchorCellIndex, _lastStrokeCellIndex))
            {
                ApplyBrushStamp(cellIndex);
            }
        }

        if (_strokeChanged && _strokeSnapshot is not null)
        {
            _pixelUndoStack.Push(_strokeSnapshot);
            _pixelRedoStack.Clear();
            RefreshPixelStudioView(_pixelStudio.ActiveTool == PixelStudioToolKind.Eraser ? "Erased pixel stroke." : "Painted pixel stroke.");
        }

        _strokeSnapshot = null;
        _isPixelStrokeActive = false;
        _strokeChanged = false;
        _lastStrokeCellIndex = -1;
        _strokeAnchorCellIndex = -1;
    }

    private void ExecutePixelStudioTool(PixelStudioToolKind tool)
    {
        StopPixelPlayback();
        if (tool != PixelStudioToolKind.Select && _selectionActive && !_selectionCommitted)
        {
            ClearSelection();
        }

        _pixelStudio.ActiveTool = tool;
        RefreshPixelStudioView($"{EditorBranding.PixelToolName} tool: {tool}.");
    }

    private void ExecutePixelStudioAction(PixelStudioAction action)
    {
        switch (action)
        {
            case PixelStudioAction.NewBlankDocument:
                ApplyPixelStudioChange("Created a blank sprite document.", () =>
                {
                    ReplacePixelStudioDocument(CreateBlankPixelStudio(32, 32));
                    return true;
                });
                break;
            case PixelStudioAction.SaveProjectDocument:
                SavePixelStudioDocumentAs();
                break;
            case PixelStudioAction.LoadProjectDocument:
                OpenPixelStudioDocument();
                break;
            case PixelStudioAction.LoadDemoDocument:
                ApplyPixelStudioChange($"Loaded the {EditorBranding.PixelToolName} demo.", () =>
                {
                    ReplacePixelStudioDocument(CreateDemoPixelStudio());
                    return true;
                });
                break;
            case PixelStudioAction.ImportImage:
                ImportPixelStudioImage();
                break;
            case PixelStudioAction.ResizeCanvas16:
                ResizePixelCanvas(16, 16);
                break;
            case PixelStudioAction.ResizeCanvas32:
                ResizePixelCanvas(32, 32);
                break;
            case PixelStudioAction.ResizeCanvas64:
                ResizePixelCanvas(64, 64);
                break;
            case PixelStudioAction.ResizeCanvas128:
                ResizePixelCanvas(128, 128);
                break;
            case PixelStudioAction.ZoomOut:
                _pixelStudio.DesiredZoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom - 2);
                RefreshPixelStudioView($"{EditorBranding.PixelToolName} zoom target set to {_pixelStudio.DesiredZoom}x.", rebuildLayout: true);
                break;
            case PixelStudioAction.ZoomIn:
                _pixelStudio.DesiredZoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom + 2);
                RefreshPixelStudioView($"{EditorBranding.PixelToolName} zoom target set to {_pixelStudio.DesiredZoom}x.", rebuildLayout: true);
                break;
            case PixelStudioAction.ToggleGrid:
                _pixelStudio.ShowGrid = !_pixelStudio.ShowGrid;
                RefreshPixelStudioView(_pixelStudio.ShowGrid ? $"{EditorBranding.PixelToolName} grid enabled." : $"{EditorBranding.PixelToolName} grid hidden.");
                break;
            case PixelStudioAction.FitCanvas:
                FitCanvasToViewport();
                break;
            case PixelStudioAction.ResetView:
                ResetCanvasView();
                break;
            case PixelStudioAction.ExportPng:
                ExportPixelStudioPng();
                break;
            case PixelStudioAction.DecreaseBrushSize:
                _pixelStudio.BrushSize = Math.Max(_pixelStudio.BrushSize - 1, 1);
                RefreshPixelStudioView($"Brush size set to {_pixelStudio.BrushSize}px.");
                break;
            case PixelStudioAction.IncreaseBrushSize:
                _pixelStudio.BrushSize = Math.Min(_pixelStudio.BrushSize + 1, 16);
                RefreshPixelStudioView($"Brush size set to {_pixelStudio.BrushSize}px.");
                break;
            case PixelStudioAction.DockToolSettingsLeft:
                _pixelToolSettingsPanelOffsetX = 12f;
                RefreshPixelStudioView("Tool settings docked left.", rebuildLayout: true);
                break;
            case PixelStudioAction.DockToolSettingsRight:
                _pixelToolSettingsPanelOffsetX = float.NaN;
                RefreshPixelStudioView("Tool settings docked right.", rebuildLayout: true);
                break;
            case PixelStudioAction.TogglePaletteLibrary:
                _paletteLibraryVisible = !_paletteLibraryVisible;
                _paletteRenameActive = false;
                RefreshPixelStudioView(_paletteLibraryVisible ? "Opened palette library." : "Closed palette library.", rebuildLayout: true);
                break;
            case PixelStudioAction.ToggleTimelinePanel:
                _pixelTimelineVisible = !_pixelTimelineVisible;
                RefreshPixelStudioView(_pixelTimelineVisible ? "Opened frames panel." : "Hidden frames panel.", rebuildLayout: true);
                break;
            case PixelStudioAction.AddPaletteSwatch:
                AddPaletteSwatch();
                break;
            case PixelStudioAction.SaveCurrentPalette:
                SaveCurrentPalette();
                break;
            case PixelStudioAction.GeneratePaletteFromImage:
                GeneratePaletteFromImage();
                break;
            case PixelStudioAction.RenameSelectedPalette:
                StartPaletteRename();
                break;
            case PixelStudioAction.DeleteSelectedPalette:
                DeleteSelectedSavedPalette();
                break;
            case PixelStudioAction.PalettePromptGenerate:
                _palettePromptVisible = false;
                GeneratePaletteFromImage(_lastImportedImagePath);
                break;
            case PixelStudioAction.PalettePromptDismiss:
                _palettePromptVisible = false;
                RefreshPixelStudioView("Kept the current palette.", rebuildLayout: true);
                break;
            case PixelStudioAction.PalettePromptDismissForever:
                _palettePromptVisible = false;
                _promptForPaletteGenerationAfterImport = false;
                RefreshPixelStudioView("Kept the current palette and disabled the import palette prompt.", rebuildLayout: true);
                break;
            case PixelStudioAction.DecreaseRed:
                AdjustActivePaletteColor(-12, 0, 0);
                break;
            case PixelStudioAction.IncreaseRed:
                AdjustActivePaletteColor(12, 0, 0);
                break;
            case PixelStudioAction.DecreaseGreen:
                AdjustActivePaletteColor(0, -12, 0);
                break;
            case PixelStudioAction.IncreaseGreen:
                AdjustActivePaletteColor(0, 12, 0);
                break;
            case PixelStudioAction.DecreaseBlue:
                AdjustActivePaletteColor(0, 0, -12);
                break;
            case PixelStudioAction.IncreaseBlue:
                AdjustActivePaletteColor(0, 0, 12);
                break;
            case PixelStudioAction.AddLayer:
                AddPixelLayer();
                break;
            case PixelStudioAction.DeleteLayer:
                DeletePixelLayer(_pixelStudio.ActiveLayerIndex);
                break;
            case PixelStudioAction.AddFrame:
                AddPixelFrame();
                break;
            case PixelStudioAction.DeleteFrame:
                DeletePixelFrame(_pixelStudio.ActiveFrameIndex);
                break;
            case PixelStudioAction.TogglePlayback:
                TogglePixelPlayback();
                break;
            case PixelStudioAction.DecreaseFrameRate:
                _pixelStudio.FramesPerSecond = Math.Max(_pixelStudio.FramesPerSecond - 1, 1);
                RefreshPixelStudioView($"Animation preview set to {_pixelStudio.FramesPerSecond} FPS.");
                break;
            case PixelStudioAction.IncreaseFrameRate:
                _pixelStudio.FramesPerSecond = Math.Min(_pixelStudio.FramesPerSecond + 1, 24);
                RefreshPixelStudioView($"Animation preview set to {_pixelStudio.FramesPerSecond} FPS.");
                break;
        }
    }

    private void ImportPixelStudioImage()
    {
        string initialDirectory = Directory.Exists(_projectLibraryPath)
            ? _projectLibraryPath
            : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        string? filePath = ImageImporter.ShowImportDialog(initialDirectory);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            RefreshPixelStudioView("Image import cancelled.");
            return;
        }

        ImageImportResult? result = null;
        try
        {
            ApplyPixelStudioChange($"Imported {Path.GetFileName(filePath)} into the active layer.", () =>
            {
                result = ImageImporter.ImportIntoLayer(filePath, _pixelStudio, CurrentPixelLayer);
                return true;
            });

            _lastImportedImagePath = filePath;
            _palettePromptVisible = _promptForPaletteGenerationAfterImport;
            if (_palettePromptVisible)
            {
                _paletteLibraryVisible = true;
            }

            if (result is not null)
            {
                RefreshPixelStudioView($"Imported {result.ImportedPixelCount} pixels using the current palette ({result.UniqueSourceColorCount} source colors detected).", rebuildLayout: true);
            }
        }
        catch (Exception exception)
        {
            RefreshPixelStudioView($"Image import failed: {exception.Message}");
        }
    }

    private void ResizePixelCanvas(int width, int height)
    {
        if (_pixelStudio.CanvasWidth == width && _pixelStudio.CanvasHeight == height)
        {
            RefreshPixelStudioView($"Canvas is already {width}x{height}.");
            return;
        }

        ApplyPixelStudioChange($"Resized canvas to {width}x{height}.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                foreach (PixelStudioLayerState layer in frame.Layers)
                {
                    int[] resizedPixels = CreateBlankPixels(width, height);
                    int copyWidth = Math.Min(width, _pixelStudio.CanvasWidth);
                    int copyHeight = Math.Min(height, _pixelStudio.CanvasHeight);
                    for (int y = 0; y < copyHeight; y++)
                    {
                        Array.Copy(layer.Pixels, y * _pixelStudio.CanvasWidth, resizedPixels, y * width, copyWidth);
                    }

                    layer.Pixels = resizedPixels;
                }
            }

            _pixelStudio.CanvasWidth = width;
            _pixelStudio.CanvasHeight = height;
        _pixelStudio.DesiredZoom = Math.Min(_pixelStudio.DesiredZoom, PixelStudioCameraMath.ClampZoom(width >= 128 || height >= 128 ? 8 : 24));
            return true;
        });
    }

    private void TogglePixelLayerVisibility(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count)
        {
            return;
        }

        ApplyPixelStudioChange(
            CurrentPixelFrame.Layers[layerIndex].IsVisible
                    ? $"{EditorBranding.PixelToolName} layer hidden: {CurrentPixelFrame.Layers[layerIndex].Name}."
                    : $"{EditorBranding.PixelToolName} layer shown: {CurrentPixelFrame.Layers[layerIndex].Name}.",
            () =>
            {
                bool newVisibility = !CurrentPixelFrame.Layers[layerIndex].IsVisible;
                foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
                {
                    frame.Layers[layerIndex].IsVisible = newVisibility;
                }

                return true;
            },
            rebuildLayout: false);
    }

    private void AddPixelLayer()
    {
        ClosePixelContextMenu();
        _layerRenameActive = false;
        ApplyPixelStudioChange("Added a new layer.", () =>
        {
            int layerNumber = CurrentPixelFrame.Layers.Count + 1;
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                frame.Layers.Add(new PixelStudioLayerState
                {
                    Name = $"Layer {layerNumber}",
                    Pixels = CreateBlankPixels(_pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight)
                });
            }

            _pixelStudio.ActiveLayerIndex = CurrentPixelFrame.Layers.Count - 1;
            return true;
        });
    }

    private void DeletePixelLayer(int layerIndex)
    {
        if (_pixelStudio.Frames.Count == 0 || CurrentPixelFrame.Layers.Count <= 1 || layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count)
        {
            RefreshPixelStudioView("At least one layer must remain.");
            return;
        }

        ClosePixelContextMenu();
        _layerRenameActive = false;
        _layerRenameBuffer = string.Empty;
        ApplyPixelStudioChange("Deleted layer.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                frame.Layers.RemoveAt(layerIndex);
            }

            _pixelStudio.ActiveLayerIndex = Math.Clamp(_pixelStudio.ActiveLayerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
            return true;
        });
    }

    private void DuplicatePixelLayer(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count)
        {
            return;
        }

        ApplyPixelStudioChange("Duplicated layer.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                PixelStudioLayerState sourceLayer = frame.Layers[layerIndex];
                frame.Layers.Insert(layerIndex + 1, CloneLayerState(sourceLayer));
                frame.Layers[layerIndex + 1].Name = $"{sourceLayer.Name} Copy";
            }

            _pixelStudio.ActiveLayerIndex = Math.Min(layerIndex + 1, CurrentPixelFrame.Layers.Count - 1);
            return true;
        });
    }

    private void MovePixelLayer(int layerIndex, int direction)
    {
        int targetIndex = layerIndex + direction;
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count || targetIndex < 0 || targetIndex >= CurrentPixelFrame.Layers.Count)
        {
            RefreshPixelStudioView("Layer is already at the edge.");
            return;
        }

        ApplyPixelStudioChange("Moved layer.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                (frame.Layers[layerIndex], frame.Layers[targetIndex]) = (frame.Layers[targetIndex], frame.Layers[layerIndex]);
            }

            _pixelStudio.ActiveLayerIndex = targetIndex;
            return true;
        });
    }

    private void TogglePixelLayerLock(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count)
        {
            return;
        }

        ApplyPixelStudioChange(CurrentPixelFrame.Layers[layerIndex].IsLocked ? "Unlocked layer." : "Locked layer.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                frame.Layers[layerIndex].IsLocked = !frame.Layers[layerIndex].IsLocked;
            }

            return true;
        });
    }

    private void AddPixelFrame()
    {
        ApplyPixelStudioChange("Added a new frame.", () =>
        {
            PixelStudioFrameState newFrame = new()
            {
                Name = $"Frame {_pixelStudio.Frames.Count + 1}",
                Layers = CurrentPixelFrame.Layers
                    .Select(CloneLayerState)
                    .ToList()
            };

            _pixelStudio.Frames.Add(newFrame);
            _pixelStudio.ActiveFrameIndex = _pixelStudio.Frames.Count - 1;
            _pixelStudio.PreviewFrameIndex = _pixelStudio.ActiveFrameIndex;
            return true;
        });
    }

    private void DeletePixelFrame(int frameIndex)
    {
        if (_pixelStudio.Frames.Count <= 1 || frameIndex < 0 || frameIndex >= _pixelStudio.Frames.Count)
        {
            RefreshPixelStudioView("At least one frame must remain.");
            return;
        }

        ApplyPixelStudioChange("Deleted frame.", () =>
        {
            _pixelStudio.Frames.RemoveAt(frameIndex);
            _pixelStudio.ActiveFrameIndex = Math.Clamp(_pixelStudio.ActiveFrameIndex, 0, _pixelStudio.Frames.Count - 1);
            _pixelStudio.PreviewFrameIndex = Math.Clamp(_pixelStudio.PreviewFrameIndex, 0, _pixelStudio.Frames.Count - 1);
            return true;
        });
    }

    private void DuplicatePixelFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _pixelStudio.Frames.Count)
        {
            return;
        }

        ApplyPixelStudioChange("Duplicated frame.", () =>
        {
            PixelStudioFrameState sourceFrame = _pixelStudio.Frames[frameIndex];
            PixelStudioFrameState duplicate = CloneFrameState(sourceFrame);
            duplicate.Name = $"{sourceFrame.Name} Copy";
            _pixelStudio.Frames.Insert(frameIndex + 1, duplicate);
            _pixelStudio.ActiveFrameIndex = frameIndex + 1;
            _pixelStudio.PreviewFrameIndex = _pixelStudio.ActiveFrameIndex;
            return true;
        });
    }

    private void MovePixelFrame(int frameIndex, int direction)
    {
        int targetIndex = frameIndex + direction;
        if (frameIndex < 0 || frameIndex >= _pixelStudio.Frames.Count || targetIndex < 0 || targetIndex >= _pixelStudio.Frames.Count)
        {
            RefreshPixelStudioView("Frame is already at the edge.");
            return;
        }

        ApplyPixelStudioChange("Moved frame.", () =>
        {
            (_pixelStudio.Frames[frameIndex], _pixelStudio.Frames[targetIndex]) = (_pixelStudio.Frames[targetIndex], _pixelStudio.Frames[frameIndex]);
            _pixelStudio.ActiveFrameIndex = targetIndex;
            _pixelStudio.PreviewFrameIndex = targetIndex;
            return true;
        });
    }

    private void TogglePixelPlayback()
    {
        if (_pixelStudio.Frames.Count <= 1)
        {
            RefreshPixelStudioView("Add more than one frame to preview animation.");
            return;
        }

        _pixelStudio.IsPlaying = !_pixelStudio.IsPlaying;
        _pixelStudio.PreviewFrameIndex = _pixelStudio.ActiveFrameIndex;
        _lastPlaybackTick = 0;
        RefreshPixelStudioView(_pixelStudio.IsPlaying ? "Animation preview playing." : "Animation preview paused.");
    }

    private void StopPixelPlayback()
    {
        if (!_pixelStudio.IsPlaying)
        {
            return;
        }

        _pixelStudio.IsPlaying = false;
        _pixelStudio.PreviewFrameIndex = _pixelStudio.ActiveFrameIndex;
        _lastPlaybackTick = 0;
    }

    private void TogglePixelToolsCollapse()
    {
        _pixelToolsCollapsed = !_pixelToolsCollapsed;
        if (_pixelToolsCollapsed)
        {
            _previousPixelToolsPanelWidth = Math.Max(_pixelToolsPanelWidth, 164);
            _pixelToolsPanelWidth = 34;
            RefreshPixelStudioView("Collapsed tools panel.", rebuildLayout: true);
            return;
        }

        _pixelToolsPanelWidth = Math.Max(_previousPixelToolsPanelWidth, 164);
        RefreshPixelStudioView("Expanded tools panel.", rebuildLayout: true);
    }

    private void TogglePixelSidebarCollapse()
    {
        _pixelSidebarCollapsed = !_pixelSidebarCollapsed;
        if (_pixelSidebarCollapsed)
        {
            _previousPixelSidebarWidth = Math.Max(_pixelSidebarWidth, 320);
            _pixelSidebarWidth = 34;
            RefreshPixelStudioView("Collapsed palette sidebar.", rebuildLayout: true);
            return;
        }

        _pixelSidebarWidth = Math.Max(_previousPixelSidebarWidth, 320);
        RefreshPixelStudioView("Expanded palette sidebar.", rebuildLayout: true);
    }

    private void AddPaletteSwatch()
    {
        ApplyPixelStudioChange("Added a palette swatch.", () =>
        {
            if (_pixelStudio.Palette.Count >= 24)
            {
                return false;
            }

            _pixelStudio.Palette.Add(_pixelStudio.Palette[Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1)]);
            _pixelStudio.ActivePaletteIndex = _pixelStudio.Palette.Count - 1;
            MarkCurrentPaletteAsUnsaved();
            return true;
        });
    }

    private void AdjustActivePaletteColor(int deltaR, int deltaG, int deltaB)
    {
        ApplyPixelStudioChange("Updated active palette color.", () =>
        {
            int paletteIndex = Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1);
            ThemeColor current = _pixelStudio.Palette[paletteIndex];
            ThemeColor updated = new(
                ClampColorChannel(current.R, deltaR),
                ClampColorChannel(current.G, deltaG),
                ClampColorChannel(current.B, deltaB));

            if (updated == current)
            {
                return false;
            }

            _pixelStudio.Palette[paletteIndex] = updated;
            MarkCurrentPaletteAsUnsaved();
            return true;
        });
    }

    private void UndoPixelStudio()
    {
        EndPixelStroke();
        if (_pixelUndoStack.Count == 0)
        {
            RefreshPixelStudioView("Nothing to undo.");
            return;
        }

        _pixelRedoStack.Push(ClonePixelStudioState(_pixelStudio));
        RestorePixelStudioState(_pixelUndoStack.Pop());
        RefreshPixelStudioView("Undid last pixel change.", rebuildLayout: true);
    }

    private void RedoPixelStudio()
    {
        EndPixelStroke();
        if (_pixelRedoStack.Count == 0)
        {
            RefreshPixelStudioView("Nothing to redo.");
            return;
        }

        _pixelUndoStack.Push(ClonePixelStudioState(_pixelStudio));
        RestorePixelStudioState(_pixelRedoStack.Pop());
        RefreshPixelStudioView("Redid pixel change.", rebuildLayout: true);
    }

    private void ApplyPixelStudioChange(string status, Func<bool> mutation, bool rebuildLayout = true)
    {
        EndPixelStroke();
        StopPixelPlayback();
        PixelStudioState snapshot = ClonePixelStudioState(_pixelStudio);
        if (!mutation())
        {
            return;
        }

        EnsurePixelStudioIndices();
        _pixelUndoStack.Push(snapshot);
        _pixelRedoStack.Clear();
        RefreshPixelStudioView(status, rebuildLayout);
    }

    private void RefreshPixelStudioView(string? overrideStatus = null, bool rebuildLayout = false)
    {
        EnsurePixelStudioIndices();
        _uiState.PixelStudio = BuildPixelStudioViewState();
        if (overrideStatus is not null)
        {
            _uiState.StatusText = overrideStatus;
            _shell.SetStatus(_uiState.StatusText);
        }

        if (rebuildLayout && _width > 0 && _height > 0)
        {
            _layoutSnapshot = EditorLayoutEngine.Create(_width, _height, _shell.Layout, _uiState);
            SyncCanvasCameraFromLayout(overrideStatus ?? "LayoutRefresh");
            _renderer?.UpdateLayoutSnapshot(_layoutSnapshot);
        }

        _renderer?.UpdateUiState(_uiState);
    }

    private void RefreshPixelStudioInteraction(bool rebuildLayout = false)
    {
        EnsurePixelStudioIndices();
        _uiState.PixelStudio = BuildPixelStudioViewState();
        if (rebuildLayout && _width > 0 && _height > 0)
        {
            _layoutSnapshot = EditorLayoutEngine.Create(_width, _height, _shell.Layout, _uiState);
            SyncCanvasCameraFromLayout("InteractionRefresh");
            _renderer?.UpdateLayoutSnapshot(_layoutSnapshot);
        }

        _renderer?.UpdateUiState(_uiState);
    }

    private void RefreshPixelStudioPanLayout()
    {
        EnsurePixelStudioIndices();
        if (_layoutSnapshot?.PixelStudio is not null)
        {
            PixelStudioLayoutSnapshot currentLayout = _layoutSnapshot.PixelStudio;
            PixelStudioCameraState camera = PixelStudioCameraMath.Compute(
                currentLayout.CanvasClipRect,
                _pixelStudio.CanvasWidth,
                _pixelStudio.CanvasHeight,
                _pixelStudio.DesiredZoom,
                _pixelStudio.CanvasPanX,
                _pixelStudio.CanvasPanY);
            ApplyCameraState(camera);
            LogCanvasCamera("Pan", camera);
            ReplacePixelStudioLayoutSnapshot(currentLayout.WithCanvasCamera(camera));
        }

        _uiState.PixelStudio = BuildPixelStudioViewState();
        _renderer?.UpdateUiState(_uiState);
    }

    private void ReplacePixelStudioLayoutSnapshot(PixelStudioLayoutSnapshot pixelStudioLayout)
    {
        if (_layoutSnapshot is null)
        {
            return;
        }

        _layoutSnapshot = new EditorLayoutSnapshot
        {
            LeftPanelRect = _layoutSnapshot.LeftPanelRect,
            LeftPanelHeaderRect = _layoutSnapshot.LeftPanelHeaderRect,
            LeftPanelBodyRect = _layoutSnapshot.LeftPanelBodyRect,
            RightPanelRect = _layoutSnapshot.RightPanelRect,
            RightPanelHeaderRect = _layoutSnapshot.RightPanelHeaderRect,
            RightPanelBodyRect = _layoutSnapshot.RightPanelBodyRect,
            WorkspaceRect = _layoutSnapshot.WorkspaceRect,
            StatusBarRect = _layoutSnapshot.StatusBarRect,
            MenuBarRect = _layoutSnapshot.MenuBarRect,
            MenuLogoRect = _layoutSnapshot.MenuLogoRect,
            TabStripRect = _layoutSnapshot.TabStripRect,
            LeftSplitterRect = _layoutSnapshot.LeftSplitterRect,
            RightSplitterRect = _layoutSnapshot.RightSplitterRect,
            LeftCollapseHandleRect = _layoutSnapshot.LeftCollapseHandleRect,
            RightCollapseHandleRect = _layoutSnapshot.RightCollapseHandleRect,
            MenuButtons = _layoutSnapshot.MenuButtons,
            TabButtons = _layoutSnapshot.TabButtons,
            HomeHeroPanelRect = _layoutSnapshot.HomeHeroPanelRect,
            HomeActionsPanelRect = _layoutSnapshot.HomeActionsPanelRect,
            HomeRecentPanelRect = _layoutSnapshot.HomeRecentPanelRect,
            HomeCards = _layoutSnapshot.HomeCards,
            RecentProjectRows = _layoutSnapshot.RecentProjectRows,
            HomeRecentViewportRect = _layoutSnapshot.HomeRecentViewportRect,
            HomeRecentScrollTrackRect = _layoutSnapshot.HomeRecentScrollTrackRect,
            HomeRecentScrollThumbRect = _layoutSnapshot.HomeRecentScrollThumbRect,
            ProjectsFormPanelRect = _layoutSnapshot.ProjectsFormPanelRect,
            ProjectsRecentPanelRect = _layoutSnapshot.ProjectsRecentPanelRect,
            ProjectRows = _layoutSnapshot.ProjectRows,
            ProjectsRecentViewportRect = _layoutSnapshot.ProjectsRecentViewportRect,
            ProjectsRecentScrollTrackRect = _layoutSnapshot.ProjectsRecentScrollTrackRect,
            ProjectsRecentScrollThumbRect = _layoutSnapshot.ProjectsRecentScrollThumbRect,
            PreferencesGeneralPanelRect = _layoutSnapshot.PreferencesGeneralPanelRect,
            PreferencesShortcutPanelRect = _layoutSnapshot.PreferencesShortcutPanelRect,
            PreferenceRows = _layoutSnapshot.PreferenceRows,
            PreferenceViewportRect = _layoutSnapshot.PreferenceViewportRect,
            PreferenceScrollTrackRect = _layoutSnapshot.PreferenceScrollTrackRect,
            PreferenceScrollThumbRect = _layoutSnapshot.PreferenceScrollThumbRect,
            LayoutInfoPanelRect = _layoutSnapshot.LayoutInfoPanelRect,
            ScratchInfoPanelRect = _layoutSnapshot.ScratchInfoPanelRect,
            LeftPanelRecentProjectRows = _layoutSnapshot.LeftPanelRecentProjectRows,
            LeftPanelRecentViewportRect = _layoutSnapshot.LeftPanelRecentViewportRect,
            LeftPanelRecentScrollTrackRect = _layoutSnapshot.LeftPanelRecentScrollTrackRect,
            LeftPanelRecentScrollThumbRect = _layoutSnapshot.LeftPanelRecentScrollThumbRect,
            MenuEntries = _layoutSnapshot.MenuEntries,
            MenuDropdownRect = _layoutSnapshot.MenuDropdownRect,
            TabCloseButtons = _layoutSnapshot.TabCloseButtons,
            ProjectFormActions = _layoutSnapshot.ProjectFormActions,
            PreferenceActions = _layoutSnapshot.PreferenceActions,
            FolderPickerActions = _layoutSnapshot.FolderPickerActions,
            FolderPickerRows = _layoutSnapshot.FolderPickerRows,
            FolderPickerRect = _layoutSnapshot.FolderPickerRect,
            FolderPickerHeaderRect = _layoutSnapshot.FolderPickerHeaderRect,
            FolderPickerBodyRect = _layoutSnapshot.FolderPickerBodyRect,
            FolderPickerViewportRect = _layoutSnapshot.FolderPickerViewportRect,
            FolderPickerScrollTrackRect = _layoutSnapshot.FolderPickerScrollTrackRect,
            FolderPickerScrollThumbRect = _layoutSnapshot.FolderPickerScrollThumbRect,
            PixelStudio = pixelStudioLayout
        };
        _renderer?.UpdateLayoutSnapshot(_layoutSnapshot);
    }

    private void RefreshPixelStudioPixels()
    {
        EnsurePixelStudioIndices();
        _uiState.PixelStudio.CompositePixels = ComposeVisiblePixels(CurrentPixelFrame);
        _uiState.PixelStudio.PreviewPixels = ComposeVisiblePixels(PreviewPixelFrame);
        _renderer?.UpdateUiState(_uiState);
    }

    private bool FloodFillCell(int cellIndex, int paletteIndex)
    {
        int[] pixels = CurrentPixelLayer.Pixels;
        int target = pixels[cellIndex];
        if (target == paletteIndex)
        {
            return false;
        }

        bool changed = false;
        Queue<int> pending = new();
        HashSet<int> visited = [];
        pending.Enqueue(cellIndex);

        while (pending.Count > 0)
        {
            int index = pending.Dequeue();
            if (!visited.Add(index) || pixels[index] != target)
            {
                continue;
            }

            pixels[index] = paletteIndex;
            changed = true;
            int x = index % _pixelStudio.CanvasWidth;
            int y = index / _pixelStudio.CanvasWidth;

            if (x > 0)
            {
                pending.Enqueue(index - 1);
            }

            if (x < _pixelStudio.CanvasWidth - 1)
            {
                pending.Enqueue(index + 1);
            }

            if (y > 0)
            {
                pending.Enqueue(index - _pixelStudio.CanvasWidth);
            }

            if (y < _pixelStudio.CanvasHeight - 1)
            {
                pending.Enqueue(index + _pixelStudio.CanvasWidth);
            }
        }

        return changed;
    }

    private int GetTopVisiblePaletteIndex(int cellIndex)
    {
        for (int index = CurrentPixelFrame.Layers.Count - 1; index >= 0; index--)
        {
            PixelStudioLayerState layer = CurrentPixelFrame.Layers[index];
            if (!layer.IsVisible)
            {
                continue;
            }

            int paletteIndex = layer.Pixels[cellIndex];
            if (paletteIndex >= 0)
            {
                return paletteIndex;
            }
        }

        return -1;
    }

    private IEnumerable<int> EnumerateStrokeCells(int fromCellIndex, int toCellIndex)
    {
        int width = _pixelStudio.CanvasWidth;
        int x0 = fromCellIndex % width;
        int y0 = fromCellIndex / width;
        int x1 = toCellIndex % width;
        int y1 = toCellIndex / width;
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx - dy;

        while (true)
        {
            yield return (y0 * width) + x0;
            if (x0 == x1 && y0 == y1)
            {
                yield break;
            }

            int doubledError = error * 2;
            if (doubledError > -dy)
            {
                error -= dy;
                x0 += sx;
            }

            if (doubledError < dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private bool IsWithinCanvas(int x, int y)
    {
        return x >= 0 && y >= 0 && x < _pixelStudio.CanvasWidth && y < _pixelStudio.CanvasHeight;
    }

    private bool IsWithinSelection(int x, int y)
    {
        if (!_selectionActive)
        {
            return true;
        }

        int left = Math.Min(_selectionStartX, _selectionEndX);
        int right = Math.Max(_selectionStartX, _selectionEndX);
        int top = Math.Min(_selectionStartY, _selectionEndY);
        int bottom = Math.Max(_selectionStartY, _selectionEndY);
        return x >= left && x <= right && y >= top && y <= bottom;
    }

    private bool IsWithinCurrentSelection(int x, int y)
    {
        if (!_selectionActive)
        {
            return false;
        }

        int left = Math.Min(_selectionStartX, _selectionEndX);
        int right = Math.Max(_selectionStartX, _selectionEndX);
        int top = Math.Min(_selectionStartY, _selectionEndY);
        int bottom = Math.Max(_selectionStartY, _selectionEndY);
        return x >= left && x <= right && y >= top && y <= bottom;
    }

    private int GetSelectionWidth() => Math.Abs(_selectionEndX - _selectionStartX) + 1;

    private int GetSelectionHeight() => Math.Abs(_selectionEndY - _selectionStartY) + 1;

    private void ClearSelection()
    {
        _selectionActive = false;
        _selectionCommitted = false;
        _selectionDragActive = false;
        _selectionStartX = 0;
        _selectionStartY = 0;
        _selectionEndX = 0;
        _selectionEndY = 0;
    }

    private void SetBrushSizeFromSlider(UiRect sliderRect, float mouseY)
    {
        float ratio = 1f - Math.Clamp((mouseY - sliderRect.Y) / Math.Max(sliderRect.Height, 1), 0f, 1f);
        _pixelStudio.BrushSize = Math.Clamp(1 + (int)MathF.Round(ratio * 15f), 1, 16);
    }

    private PixelStudioViewState BuildPixelStudioViewState()
    {
        EnsurePixelStudioIndices();
        ThemeColor activeColor = _pixelStudio.Palette[Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1)];

        return new PixelStudioViewState
        {
            DocumentName = _pixelStudio.DocumentName,
            CanvasWidth = _pixelStudio.CanvasWidth,
            CanvasHeight = _pixelStudio.CanvasHeight,
            Zoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom),
            BrushSize = _pixelStudio.BrushSize,
            CanvasPanX = _pixelStudio.CanvasPanX,
            CanvasPanY = _pixelStudio.CanvasPanY,
            ShowGrid = _pixelStudio.ShowGrid,
            FramesPerSecond = _pixelStudio.FramesPerSecond,
            IsPlaying = _pixelStudio.IsPlaying,
            CanUndo = _pixelUndoStack.Count > 0,
            CanRedo = _pixelRedoStack.Count > 0,
            ActiveTool = _pixelStudio.ActiveTool,
            ActivePaletteIndex = _pixelStudio.ActivePaletteIndex,
            ActiveColorHex = ToHex(activeColor),
            ActiveColor = activeColor,
            ActivePaletteName = GetActivePaletteDisplayName(),
            PaletteLibraryVisible = _paletteLibraryVisible,
            PalettePromptVisible = _palettePromptVisible,
            PaletteRenameActive = _paletteRenameActive,
            LayerRenameActive = _layerRenameActive,
            FrameRenameActive = _frameRenameActive,
            HasSelection = _selectionActive,
            SelectionX = Math.Min(_selectionStartX, _selectionEndX),
            SelectionY = Math.Min(_selectionStartY, _selectionEndY),
            SelectionWidth = _selectionActive ? GetSelectionWidth() : 0,
            SelectionHeight = _selectionActive ? GetSelectionHeight() : 0,
            PromptForPaletteGenerationAfterImport = _promptForPaletteGenerationAfterImport,
            ToolsPanelPreferredWidth = _pixelToolsPanelWidth,
            SidebarPreferredWidth = _pixelSidebarWidth,
            ToolsPanelCollapsed = _pixelToolsCollapsed,
            SidebarCollapsed = _pixelSidebarCollapsed,
            TimelineVisible = _pixelTimelineVisible,
            ToolSettingsPanelOffsetX = _pixelToolSettingsPanelOffsetX,
            ToolSettingsPanelOffsetY = _pixelToolSettingsPanelOffsetY,
            PaletteSwatchScrollRow = _paletteSwatchScrollRow,
            SavedPaletteScrollRow = _savedPaletteScrollRow,
            LayerScrollRow = _layerScrollRow,
            FrameScrollRow = _frameScrollRow,
            PaletteRenameBuffer = _paletteRenameBuffer,
            LayerRenameBuffer = _layerRenameBuffer,
            FrameRenameBuffer = _frameRenameBuffer,
            ContextMenuVisible = _pixelContextMenuVisible,
            ContextMenuX = _pixelContextMenuX,
            ContextMenuY = _pixelContextMenuY,
            ContextMenuItems = BuildPixelContextMenuItems(),
            Palette = _pixelStudio.Palette.ToList(),
            CompositePixels = ComposeVisiblePixels(CurrentPixelFrame),
            PreviewPixels = ComposeVisiblePixels(PreviewPixelFrame),
            SavedPalettes = BuildSavedPaletteViews(),
            Layers = CurrentPixelFrame.Layers
                .Select((layer, index) => new PixelStudioLayerView
                {
                    Name = layer.Name,
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked,
                    IsActive = index == _pixelStudio.ActiveLayerIndex
                })
                .ToList(),
            Frames = _pixelStudio.Frames
                .Select((frame, index) => new PixelStudioFrameView
                {
                    Name = frame.Name,
                    IsActive = index == _pixelStudio.ActiveFrameIndex,
                    IsPreviewing = index == _pixelStudio.PreviewFrameIndex,
                    IsSelected = index == _pixelStudio.ActiveFrameIndex
                })
                .ToList()
        };
    }

    private bool HandlePixelStudioTextKeyDown(Key key)
    {
        if (!_paletteRenameActive && !_layerRenameActive && !_frameRenameActive)
        {
            if (key == Key.Delete && _selectionActive && !CurrentPixelLayer.IsLocked)
            {
                DeleteSelectionPixels();
                return true;
            }

            if (key == Key.Escape && _selectionActive)
            {
                ClearSelection();
                RefreshPixelStudioView("Selection cleared.");
                return true;
            }

            return false;
        }

        switch (key)
        {
            case Key.Backspace:
                if (_paletteRenameActive && _paletteRenameBuffer.Length > 0)
                {
                    _paletteRenameBuffer = _paletteRenameBuffer[..^1];
                    RefreshPixelStudioView("Editing palette name.");
                }
                else if (_layerRenameActive && _layerRenameBuffer.Length > 0)
                {
                    _layerRenameBuffer = _layerRenameBuffer[..^1];
                    RefreshPixelStudioView("Editing layer name.");
                }
                else if (_frameRenameActive && _frameRenameBuffer.Length > 0)
                {
                    _frameRenameBuffer = _frameRenameBuffer[..^1];
                    RefreshPixelStudioView("Editing frame name.");
                }

                return true;
            case Key.Enter:
                if (_paletteRenameActive)
                {
                    CommitPaletteRename();
                }
                else if (_frameRenameActive)
                {
                    CommitFrameRename();
                }
                else
                {
                    CommitLayerRename();
                }
                return true;
            case Key.Escape:
                if (_paletteRenameActive)
                {
                    CancelPaletteRename();
                }
                else if (_frameRenameActive)
                {
                    CancelFrameRename();
                }
                else
                {
                    CancelLayerRename();
                }
                return true;
            default:
                return true;
        }
    }

    private bool HandlePixelStudioTextInput(char character)
    {
        if ((!_paletteRenameActive && !_layerRenameActive && !_frameRenameActive) || char.IsControl(character))
        {
            return false;
        }

        if (_paletteRenameActive)
        {
            _paletteRenameBuffer += character;
            RefreshPixelStudioView("Editing palette name.");
        }
        else
        if (_frameRenameActive)
        {
            _frameRenameBuffer += character;
            RefreshPixelStudioView("Editing frame name.");
        }
        else
        {
            _layerRenameBuffer += character;
            RefreshPixelStudioView("Editing layer name.");
        }
        return true;
    }

    private IReadOnlyList<PixelStudioContextMenuItemView> BuildPixelContextMenuItems()
    {
        if (!_pixelContextMenuVisible)
        {
            return [];
        }

        if (_contextSelectionActive)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DisableSelection, Label = "Disable Selection", IsDestructive = true }
            ];
        }

        if (_contextPaletteIndex is not null)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RenamePalette, Label = "Rename Palette" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DeletePalette, Label = "Delete Palette", IsDestructive = true }
            ];
        }

        if (_contextLayerIndex is not null)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RenameLayer, Label = "Rename Layer" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DuplicateLayer, Label = "Duplicate Layer" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.MoveLayerUp, Label = "Move Layer Up" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.MoveLayerDown, Label = "Move Layer Down" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.ToggleLayerLock, Label = CurrentPixelFrame.Layers[Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1)].IsLocked ? "Unlock Layer" : "Lock Layer" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DeleteLayer, Label = "Delete Layer", IsDestructive = true }
            ];
        }

        if (_contextFrameIndex is not null)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RenameFrame, Label = "Rename Frame" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DuplicateFrame, Label = "Duplicate Frame" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.MoveFrameLeft, Label = "Move Frame Left" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.MoveFrameRight, Label = "Move Frame Right" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DeleteFrame, Label = "Delete Frame", IsDestructive = true }
            ];
        }

        return [];
    }

    private IReadOnlyList<PixelStudioSavedPaletteView> BuildSavedPaletteViews()
    {
        return _savedPixelPalettes
            .Select(palette => new PixelStudioSavedPaletteView
            {
                Id = palette.Id,
                Name = palette.Name,
                IsActive = string.Equals(palette.Id, _activePixelPaletteId, StringComparison.Ordinal),
                IsSelected = string.Equals(palette.Id, _selectedPixelPaletteId, StringComparison.Ordinal),
                PreviewColors = palette.Colors.Take(4).Select(ToThemeColor).ToList()
            })
            .ToList();
    }

    private void OpenPaletteContextMenu(int paletteIndex, float x, float y)
    {
        _contextPaletteIndex = paletteIndex;
        _contextLayerIndex = null;
        _contextFrameIndex = null;
        _contextSelectionActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        _layerRenameActive = false;
        RefreshPixelStudioView($"Opened palette menu for {_savedPixelPalettes[paletteIndex].Name}.", rebuildLayout: true);
    }

    private void OpenLayerContextMenu(int layerIndex, float x, float y)
    {
        _contextLayerIndex = layerIndex;
        _contextPaletteIndex = null;
        _contextFrameIndex = null;
        _contextSelectionActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        _paletteRenameActive = false;
        RefreshPixelStudioView($"Opened layer menu for {CurrentPixelFrame.Layers[layerIndex].Name}.", rebuildLayout: true);
    }

    private void OpenFrameContextMenu(int frameIndex, float x, float y)
    {
        _contextFrameIndex = frameIndex;
        _contextLayerIndex = null;
        _contextPaletteIndex = null;
        _contextSelectionActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        RefreshPixelStudioView($"Opened frame menu for {_pixelStudio.Frames[frameIndex].Name}.", rebuildLayout: true);
    }

    private void OpenSelectionContextMenu(float x, float y)
    {
        _contextFrameIndex = null;
        _contextLayerIndex = null;
        _contextPaletteIndex = null;
        _contextSelectionActive = true;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        RefreshPixelStudioView("Opened selection menu.", rebuildLayout: true);
    }

    private void ClosePixelContextMenu()
    {
        _pixelContextMenuVisible = false;
        _contextPaletteIndex = null;
        _contextLayerIndex = null;
        _contextFrameIndex = null;
        _contextSelectionActive = false;
    }

    private void ExecutePixelStudioContextMenuAction(PixelStudioContextMenuAction action)
    {
        switch (action)
        {
            case PixelStudioContextMenuAction.DisableSelection:
                ClosePixelContextMenu();
                ClearSelection();
                RefreshPixelStudioView("Selection cleared.", rebuildLayout: true);
                break;
            case PixelStudioContextMenuAction.RenamePalette:
                if (_contextPaletteIndex is not null)
                {
                    _selectedPixelPaletteId = _savedPixelPalettes[Math.Clamp(_contextPaletteIndex.Value, 0, _savedPixelPalettes.Count - 1)].Id;
                    ClosePixelContextMenu();
                    StartPaletteRename();
                }
                break;
            case PixelStudioContextMenuAction.DeletePalette:
                if (_contextPaletteIndex is not null)
                {
                    _selectedPixelPaletteId = _savedPixelPalettes[Math.Clamp(_contextPaletteIndex.Value, 0, _savedPixelPalettes.Count - 1)].Id;
                    ClosePixelContextMenu();
                    DeleteSelectedSavedPalette();
                }
                break;
            case PixelStudioContextMenuAction.RenameLayer:
                if (_contextLayerIndex is not null)
                {
                    int layerIndex = Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1);
                    _pixelStudio.ActiveLayerIndex = layerIndex;
                    ClosePixelContextMenu();
                    StartLayerRename(layerIndex);
                }
                break;
            case PixelStudioContextMenuAction.DeleteLayer:
                if (_contextLayerIndex is not null)
                {
                    int layerIndex = Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1);
                    _pixelStudio.ActiveLayerIndex = layerIndex;
                    ClosePixelContextMenu();
                    DeletePixelLayer(layerIndex);
                }
                break;
            case PixelStudioContextMenuAction.DuplicateLayer:
                if (_contextLayerIndex is not null)
                {
                    DuplicatePixelLayer(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1));
                }
                break;
            case PixelStudioContextMenuAction.MoveLayerUp:
                if (_contextLayerIndex is not null)
                {
                    MovePixelLayer(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1), -1);
                }
                break;
            case PixelStudioContextMenuAction.MoveLayerDown:
                if (_contextLayerIndex is not null)
                {
                    MovePixelLayer(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1), 1);
                }
                break;
            case PixelStudioContextMenuAction.ToggleLayerLock:
                if (_contextLayerIndex is not null)
                {
                    TogglePixelLayerLock(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1));
                }
                break;
            case PixelStudioContextMenuAction.RenameFrame:
                if (_contextFrameIndex is not null)
                {
                    StartFrameRename(Math.Clamp(_contextFrameIndex.Value, 0, _pixelStudio.Frames.Count - 1));
                }
                break;
            case PixelStudioContextMenuAction.DuplicateFrame:
                if (_contextFrameIndex is not null)
                {
                    DuplicatePixelFrame(Math.Clamp(_contextFrameIndex.Value, 0, _pixelStudio.Frames.Count - 1));
                }
                break;
            case PixelStudioContextMenuAction.MoveFrameLeft:
                if (_contextFrameIndex is not null)
                {
                    MovePixelFrame(Math.Clamp(_contextFrameIndex.Value, 0, _pixelStudio.Frames.Count - 1), -1);
                }
                break;
            case PixelStudioContextMenuAction.MoveFrameRight:
                if (_contextFrameIndex is not null)
                {
                    MovePixelFrame(Math.Clamp(_contextFrameIndex.Value, 0, _pixelStudio.Frames.Count - 1), 1);
                }
                break;
            case PixelStudioContextMenuAction.DeleteFrame:
                if (_contextFrameIndex is not null)
                {
                    DeletePixelFrame(Math.Clamp(_contextFrameIndex.Value, 0, _pixelStudio.Frames.Count - 1));
                }
                break;
        }
    }

    private void SelectAndApplySavedPalette(int index)
    {
        if (index < 0)
        {
            ApplyPixelStudioChange("Applied the default palette.", () =>
            {
                ApplyDefaultPalette();
                return true;
            }, rebuildLayout: false);
            return;
        }

        if (index >= _savedPixelPalettes.Count)
        {
            return;
        }

        SavedPixelPalette palette = _savedPixelPalettes[index];
        _selectedPixelPaletteId = palette.Id;
        ApplyPixelStudioChange($"Applied palette {palette.Name}.", () =>
        {
            ApplySavedPalette(palette);
            return true;
        }, rebuildLayout: false);
    }

    private void SaveCurrentPalette()
    {
        string name = SanitizePaletteName(BuildNextPaletteName());
        SavedPixelPalette palette = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Colors = _pixelStudio.Palette.Select(ToPaletteColorSetting).ToList()
        };

        _savedPixelPalettes.Add(palette);
        _activePixelPaletteId = palette.Id;
        _selectedPixelPaletteId = palette.Id;
        ClosePixelContextMenu();
        _paletteRenameBuffer = palette.Name;
        RefreshPixelStudioView($"Saved palette {palette.Name}.", rebuildLayout: true);
    }

    private void GeneratePaletteFromImage(string? filePath = null)
    {
        string? imagePath = filePath;
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            imagePath = _lastImportedImagePath;
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            string initialDirectory = Directory.Exists(_projectLibraryPath)
                ? _projectLibraryPath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            imagePath = ImageImporter.ShowImportDialog(initialDirectory);
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            RefreshPixelStudioView("Palette generation cancelled.");
            return;
        }

        string path = imagePath;
        ApplyPixelStudioChange($"Generated a palette from {Path.GetFileName(path)}.", () =>
        {
            IReadOnlyList<ThemeColor> generatedPalette = ImageImporter.GeneratePaletteFromImage(path, _pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight);
            ReplaceCurrentPaletteColors(generatedPalette, remapArtwork: true);
            _pixelStudio.ActivePaletteIndex = 0;
            MarkCurrentPaletteAsUnsaved();
            _lastImportedImagePath = path;
            _palettePromptVisible = false;
            _paletteLibraryVisible = true;
            _paletteRenameActive = false;
            ImageImporter.ImportIntoLayer(path, _pixelStudio, CurrentPixelLayer);
            return true;
        });
    }

    private void StartPaletteRename()
    {
        SavedPixelPalette? selected = GetSelectedSavedPalette();
        if (selected is null)
        {
            RefreshPixelStudioView("Select a saved palette to rename.");
            return;
        }

        ClosePixelContextMenu();
        _layerRenameActive = false;
        _paletteRenameActive = true;
        _paletteRenameBuffer = selected.Name;
        RefreshPixelStudioView($"Renaming {selected.Name}.", rebuildLayout: true);
    }

    private void CommitPaletteRename()
    {
        SavedPixelPalette? selected = GetSelectedSavedPalette();
        if (selected is null)
        {
            _paletteRenameActive = false;
            RefreshPixelStudioView("No saved palette selected.");
            return;
        }

        string name = SanitizePaletteName(_paletteRenameBuffer);
        ReplaceSavedPalette(new SavedPixelPalette
        {
            Id = selected.Id,
            Name = name,
            Colors = selected.Colors
                .Select(color => new PaletteColorSetting
                {
                    R = color.R,
                    G = color.G,
                    B = color.B,
                    A = color.A
                })
                .ToList()
        });
        _paletteRenameActive = false;
        _paletteRenameBuffer = name;
        ClosePixelContextMenu();
        RefreshPixelStudioView($"Renamed palette to {name}.", rebuildLayout: true);
    }

    private void CancelPaletteRename()
    {
        _paletteRenameActive = false;
        _paletteRenameBuffer = string.Empty;
        ClosePixelContextMenu();
        RefreshPixelStudioView("Palette rename cancelled.", rebuildLayout: true);
    }

    private void StartLayerRename(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count)
        {
            RefreshPixelStudioView("Select a layer to rename.");
            return;
        }

        ClosePixelContextMenu();
        _paletteRenameActive = false;
        _layerRenameActive = true;
        _layerRenameBuffer = CurrentPixelFrame.Layers[layerIndex].Name;
        RefreshPixelStudioView($"Renaming {CurrentPixelFrame.Layers[layerIndex].Name}.", rebuildLayout: true);
    }

    private void CommitLayerRename()
    {
        int layerIndex = Math.Clamp(_pixelStudio.ActiveLayerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        string name = SanitizeLayerName(_layerRenameBuffer);
        ApplyPixelStudioChange($"Renamed layer to {name}.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                frame.Layers[layerIndex].Name = name;
            }

            return true;
        });
        _layerRenameActive = false;
        _layerRenameBuffer = name;
    }

    private void CancelLayerRename()
    {
        _layerRenameActive = false;
        _layerRenameBuffer = string.Empty;
        RefreshPixelStudioView("Layer rename cancelled.", rebuildLayout: true);
    }

    private void StartFrameRename(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _pixelStudio.Frames.Count)
        {
            RefreshPixelStudioView("Select a frame to rename.");
            return;
        }

        ClosePixelContextMenu();
        _pixelStudio.ActiveFrameIndex = frameIndex;
        _pixelStudio.PreviewFrameIndex = frameIndex;
        _frameRenameActive = true;
        _frameRenameBuffer = _pixelStudio.Frames[frameIndex].Name;
        RefreshPixelStudioView($"Renaming {_pixelStudio.Frames[frameIndex].Name}.", rebuildLayout: true);
    }

    private void CommitFrameRename()
    {
        int frameIndex = Math.Clamp(_pixelStudio.ActiveFrameIndex, 0, _pixelStudio.Frames.Count - 1);
        string name = SanitizeFrameName(_frameRenameBuffer);
        ApplyPixelStudioChange($"Renamed frame to {name}.", () =>
        {
            _pixelStudio.Frames[frameIndex].Name = name;
            return true;
        });
        _frameRenameActive = false;
        _frameRenameBuffer = name;
    }

    private void CancelFrameRename()
    {
        _frameRenameActive = false;
        _frameRenameBuffer = string.Empty;
        RefreshPixelStudioView("Frame rename cancelled.", rebuildLayout: true);
    }

    private void DeleteSelectionPixels()
    {
        if (!_selectionActive)
        {
            return;
        }

        ApplyPixelStudioChange("Cleared selected pixels.", () =>
        {
            int left = Math.Min(_selectionStartX, _selectionEndX);
            int right = Math.Max(_selectionStartX, _selectionEndX);
            int top = Math.Min(_selectionStartY, _selectionEndY);
            int bottom = Math.Max(_selectionStartY, _selectionEndY);
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    CurrentPixelLayer.Pixels[(y * _pixelStudio.CanvasWidth) + x] = -1;
                }
            }

            return true;
        }, rebuildLayout: false);
    }

    private void DeleteSelectedSavedPalette()
    {
        SavedPixelPalette? selected = GetSelectedSavedPalette();
        if (selected is null)
        {
            RefreshPixelStudioView("Select a saved palette to delete.");
            return;
        }

        _savedPixelPalettes.RemoveAll(palette => string.Equals(palette.Id, selected.Id, StringComparison.Ordinal));
        if (string.Equals(_activePixelPaletteId, selected.Id, StringComparison.Ordinal))
        {
            _activePixelPaletteId = null;
        }

        _selectedPixelPaletteId = _savedPixelPalettes.FirstOrDefault()?.Id;
        _paletteRenameActive = false;
        _paletteRenameBuffer = string.Empty;
        ClosePixelContextMenu();
        RefreshPixelStudioView($"Deleted palette {selected.Name}.", rebuildLayout: true);
    }

    private List<ThemeColor?> ComposeVisiblePixels(PixelStudioFrameState frame)
    {
        List<ThemeColor?> composite = Enumerable.Repeat<ThemeColor?>(null, _pixelStudio.CanvasWidth * _pixelStudio.CanvasHeight).ToList();
        foreach (PixelStudioLayerState layer in frame.Layers)
        {
            if (!layer.IsVisible)
            {
                continue;
            }

            for (int pixelIndex = 0; pixelIndex < layer.Pixels.Length; pixelIndex++)
            {
                int paletteIndex = layer.Pixels[pixelIndex];
                if (paletteIndex >= 0 && paletteIndex < _pixelStudio.Palette.Count)
                {
                    composite[pixelIndex] = _pixelStudio.Palette[paletteIndex];
                }
            }
        }

        return composite;
    }

    private void EnsurePixelStudioIndices()
    {
        if (_pixelStudio.Palette.Count == 0)
        {
            _pixelStudio.Palette.AddRange(CreateDefaultPalette());
        }

        if (_pixelStudio.Frames.Count == 0)
        {
            ReplacePixelStudioDocument(CreateBlankPixelStudio(_pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight));
        }

        _pixelStudio.ActiveFrameIndex = Math.Clamp(_pixelStudio.ActiveFrameIndex, 0, _pixelStudio.Frames.Count - 1);
        _pixelStudio.PreviewFrameIndex = Math.Clamp(_pixelStudio.PreviewFrameIndex, 0, _pixelStudio.Frames.Count - 1);
        _pixelStudio.ActiveLayerIndex = Math.Clamp(_pixelStudio.ActiveLayerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        _pixelStudio.ActivePaletteIndex = Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1);
    }

    private PixelStudioFrameState CurrentPixelFrame => _pixelStudio.Frames[_pixelStudio.ActiveFrameIndex];

    private PixelStudioFrameState PreviewPixelFrame => _pixelStudio.Frames[_pixelStudio.PreviewFrameIndex];

    private PixelStudioLayerState CurrentPixelLayer => CurrentPixelFrame.Layers[_pixelStudio.ActiveLayerIndex];

    private static PixelStudioState CreateDefaultPixelStudio()
    {
        return CreateBlankPixelStudio(32, 32);
    }

    private static PixelStudioState CreateBlankPixelStudio(int width, int height)
    {
        return new PixelStudioState
        {
            DocumentName = "Blank Sprite",
            CanvasWidth = width,
            CanvasHeight = height,
            Palette = CreateDefaultPalette(),
            Frames =
            [
                new PixelStudioFrameState
                {
                    Name = "Frame 1",
                    Layers =
                    [
                        new PixelStudioLayerState
                        {
                            Name = "Layer 1",
                            Pixels = CreateBlankPixels(width, height)
                        }
                    ]
                }
            ]
        };
    }

    private static PixelStudioState CreateDemoPixelStudio()
    {
        return new PixelStudioState
        {
            DocumentName = $"{EditorBranding.PixelToolName} Demo",
            CanvasWidth = 16,
            CanvasHeight = 16,
            ActiveLayerIndex = 0,
            Palette = CreateDefaultPalette(),
            Frames =
            [
                CreateDemoFrame("Frame 1", false),
                CreateDemoFrame("Frame 2", true)
            ]
        };
    }

    private void ReplacePixelStudioDocument(PixelStudioState source)
    {
        ClearSelection();
        _pixelStudio.DocumentName = source.DocumentName;
        _pixelStudio.CanvasWidth = source.CanvasWidth;
        _pixelStudio.CanvasHeight = source.CanvasHeight;
        _pixelStudio.DesiredZoom = source.DesiredZoom;
        _pixelStudio.BrushSize = source.BrushSize;
        _pixelStudio.CanvasPanX = source.CanvasPanX;
        _pixelStudio.CanvasPanY = source.CanvasPanY;
        _pixelStudio.ShowGrid = source.ShowGrid;
        _pixelStudio.FramesPerSecond = source.FramesPerSecond;
        _pixelStudio.IsPlaying = source.IsPlaying;
        _pixelStudio.ActiveTool = source.ActiveTool;
        _pixelStudio.ActivePaletteIndex = source.ActivePaletteIndex;
        _pixelStudio.ActiveFrameIndex = source.ActiveFrameIndex;
        _pixelStudio.ActiveLayerIndex = source.ActiveLayerIndex;
        _pixelStudio.PreviewFrameIndex = source.PreviewFrameIndex;
        _pixelStudio.Palette.Clear();
        _pixelStudio.Palette.AddRange(source.Palette);
        _pixelStudio.Frames.Clear();
        _pixelStudio.Frames.AddRange(source.Frames.Select(CloneFrameState));
        EnsurePixelStudioIndices();
    }

    private static PixelStudioState ClonePixelStudioState(PixelStudioState source)
    {
        return new PixelStudioState
        {
            DocumentName = source.DocumentName,
            CanvasWidth = source.CanvasWidth,
            CanvasHeight = source.CanvasHeight,
            DesiredZoom = source.DesiredZoom,
            BrushSize = source.BrushSize,
            CanvasPanX = source.CanvasPanX,
            CanvasPanY = source.CanvasPanY,
            ShowGrid = source.ShowGrid,
            FramesPerSecond = source.FramesPerSecond,
            IsPlaying = source.IsPlaying,
            ActiveTool = source.ActiveTool,
            ActivePaletteIndex = source.ActivePaletteIndex,
            ActiveFrameIndex = source.ActiveFrameIndex,
            ActiveLayerIndex = source.ActiveLayerIndex,
            PreviewFrameIndex = source.PreviewFrameIndex,
            Palette = source.Palette.ToList(),
            Frames = source.Frames.Select(CloneFrameState).ToList()
        };
    }

    private void RestorePixelStudioState(PixelStudioState snapshot)
    {
        ReplacePixelStudioDocument(ClonePixelStudioState(snapshot));
        StopPixelPlayback();
    }

    private void SavePixelStudioDocument()
    {
        string? documentPath = _currentPixelDocumentPath;
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            SavePixelStudioDocumentAs();
            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(documentPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(CreateProjectDocumentSnapshot(), PixelStudioDocumentSerializerOptions);
            File.WriteAllText(documentPath, json);
            _currentPixelDocumentPath = documentPath;
            RefreshPixelStudioView($"Saved {EditorBranding.PixelToolName} file {Path.GetFileName(documentPath)}.");
        }
        catch (Exception ex)
        {
            RefreshPixelStudioView($"Could not save {EditorBranding.PixelToolName} artwork: {ex.Message}");
        }
    }

    private void SavePixelStudioDocumentAs()
    {
        string initialDirectory = ResolvePixelStudioDocumentDirectory();
        string suggestedFileName = BuildSuggestedPixelStudioFileName();
        string? documentPath = PixelStudioDocumentFilePicker.ShowSaveDialog(initialDirectory, suggestedFileName);
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            RefreshPixelStudioView("Save cancelled.");
            return;
        }

        _currentPixelDocumentPath = EnsurePixelStudioDocumentExtension(documentPath);
        _pixelStudio.DocumentName = Path.GetFileNameWithoutExtension(_currentPixelDocumentPath);
        SavePixelStudioDocument();
    }

    private void ExportPixelStudioPng()
    {
        string initialDirectory = ResolvePixelStudioDocumentDirectory();
        string suggestedFileName = $"{SanitizePixelStudioDocumentName(_pixelStudio.DocumentName)}.png";
        string? exportPath = PixelStudioDocumentFilePicker.ShowPngSaveDialog(initialDirectory, suggestedFileName);
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            RefreshPixelStudioView("PNG export cancelled.");
            return;
        }

        string outputPath = Path.GetExtension(exportPath).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? exportPath
            : $"{exportPath}.png";
        try
        {
            List<ThemeColor?> composite = ComposeVisiblePixels(CurrentPixelFrame);
            using Image<Rgba32> image = new(_pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight);
            for (int y = 0; y < _pixelStudio.CanvasHeight; y++)
            {
                for (int x = 0; x < _pixelStudio.CanvasWidth; x++)
                {
                    ThemeColor? pixelColor = composite[(y * _pixelStudio.CanvasWidth) + x];
                    if (pixelColor is null)
                    {
                        image[x, y] = new Rgba32(0, 0, 0, 0);
                        continue;
                    }

                    image[x, y] = new Rgba32(
                        ToColorByte(pixelColor.Value.R),
                        ToColorByte(pixelColor.Value.G),
                        ToColorByte(pixelColor.Value.B),
                        ToColorByte(pixelColor.Value.A));
                }
            }

            image.SaveAsPng(outputPath);
            RefreshPixelStudioView($"Exported PNG to {Path.GetFileName(outputPath)}.");
        }
        catch (Exception ex)
        {
            RefreshPixelStudioView($"PNG export failed: {ex.Message}");
        }
    }

    private void FitCanvasToViewport()
    {
        if (_layoutSnapshot?.PixelStudio is null)
        {
            return;
        }

        UiRect clipRect = _layoutSnapshot.PixelStudio.CanvasClipRect;
        _pixelStudio.DesiredZoom = PixelStudioCameraMath.ComputeFitZoom(clipRect, _pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight);
        _pixelStudio.CanvasPanX = 0;
        _pixelStudio.CanvasPanY = 0;
        LogCurrentCanvasCamera("FitRequested");
        RefreshPixelStudioView("Canvas fitted to viewport.", rebuildLayout: true);
    }

    private void ResetCanvasView()
    {
        _pixelStudio.DesiredZoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.CanvasWidth >= 128 || _pixelStudio.CanvasHeight >= 128 ? 8 : 24);
        _pixelStudio.CanvasPanX = 0;
        _pixelStudio.CanvasPanY = 0;
        LogCurrentCanvasCamera("ResetRequested");
        RefreshPixelStudioView("Canvas view reset.", rebuildLayout: true);
    }

    private void OpenPixelStudioDocument()
    {
        string initialDirectory = ResolvePixelStudioDocumentDirectory();
        string? documentPath = PixelStudioDocumentFilePicker.ShowOpenDialog(initialDirectory);
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            RefreshPixelStudioView("Open cancelled.");
            return;
        }

        if (TryLoadPixelStudioDocument(documentPath, out string status))
        {
            RefreshPixelStudioView(status, rebuildLayout: true);
            return;
        }

        RefreshPixelStudioView(status);
    }

    private bool TryLoadPixelStudioDocument(string documentPath, out string status)
    {
        if (!File.Exists(documentPath))
        {
            status = $"Could not find {Path.GetFileName(documentPath)}.";
            return false;
        }

        try
        {
            string json = File.ReadAllText(documentPath);
            PixelStudioProjectDocument? document = JsonSerializer.Deserialize<PixelStudioProjectDocument>(json, PixelStudioDocumentSerializerOptions);
            if (document is null)
            {
                status = $"Could not load {EditorBranding.PixelToolName} artwork from {Path.GetFileName(documentPath)}.";
                return false;
            }

            ReplacePixelStudioDocument(CreatePixelStudioState(document));
            _currentPixelDocumentPath = documentPath;
            _pixelStudio.DocumentName = Path.GetFileNameWithoutExtension(documentPath);
            _pixelUndoStack.Clear();
            _pixelRedoStack.Clear();
            status = $"Opened {EditorBranding.PixelToolName} file {Path.GetFileName(documentPath)}.";
            return true;
        }
        catch (Exception ex)
        {
            status = $"Could not load {EditorBranding.PixelToolName} artwork: {ex.Message}";
            return false;
        }
    }

    private PixelStudioProjectDocument CreateProjectDocumentSnapshot()
    {
        return new PixelStudioProjectDocument
        {
            DocumentName = _pixelStudio.DocumentName,
            CanvasWidth = _pixelStudio.CanvasWidth,
            CanvasHeight = _pixelStudio.CanvasHeight,
            DesiredZoom = _pixelStudio.DesiredZoom,
            BrushSize = _pixelStudio.BrushSize,
            CanvasPanX = _pixelStudio.CanvasPanX,
            CanvasPanY = _pixelStudio.CanvasPanY,
            ShowGrid = _pixelStudio.ShowGrid,
            FramesPerSecond = _pixelStudio.FramesPerSecond,
            IsPlaying = false,
            PreviewFrameIndex = _pixelStudio.PreviewFrameIndex,
            ActiveTool = _pixelStudio.ActiveTool,
            ActivePaletteIndex = _pixelStudio.ActivePaletteIndex,
            ActiveFrameIndex = _pixelStudio.ActiveFrameIndex,
            ActiveLayerIndex = _pixelStudio.ActiveLayerIndex,
            Palette = _pixelStudio.Palette.Select(ToPaletteColorSetting).ToList(),
            Frames = _pixelStudio.Frames.Select(frame => new PixelStudioProjectFrameDocument
            {
                Name = frame.Name,
                Layers = frame.Layers.Select(layer => new PixelStudioProjectLayerDocument
                {
                    Name = layer.Name,
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked,
                    Pixels = layer.Pixels.ToArray()
                }).ToList()
            }).ToList()
        };
    }

    private static PixelStudioState CreatePixelStudioState(PixelStudioProjectDocument document)
    {
        int width = Math.Clamp(document.CanvasWidth, 1, 512);
        int height = Math.Clamp(document.CanvasHeight, 1, 512);
        int pixelCount = width * height;
        List<ThemeColor> palette = document.Palette.Count > 0
            ? document.Palette.Select(ToThemeColor).ToList()
            : CreateDefaultPalette();

        List<PixelStudioFrameState> frames = document.Frames
            .Select(frame => new PixelStudioFrameState
            {
                Name = string.IsNullOrWhiteSpace(frame.Name) ? "Frame" : frame.Name,
                Layers = frame.Layers.Select(layer => new PixelStudioLayerState
                {
                    Name = string.IsNullOrWhiteSpace(layer.Name) ? "Layer" : layer.Name,
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked,
                    Pixels = NormalizePixelBuffer(layer.Pixels, pixelCount, palette.Count)
                }).ToList()
            })
            .Where(frame => frame.Layers.Count > 0)
            .ToList();

        if (frames.Count == 0)
        {
            frames.Add(new PixelStudioFrameState
            {
                Name = "Frame 1",
                Layers =
                [
                    new PixelStudioLayerState
                    {
                        Name = "Layer 1",
                        Pixels = CreateBlankPixels(width, height)
                    }
                ]
            });
        }

        return new PixelStudioState
        {
            DocumentName = string.IsNullOrWhiteSpace(document.DocumentName) ? "Blank Sprite" : document.DocumentName,
            CanvasWidth = width,
            CanvasHeight = height,
            DesiredZoom = PixelStudioCameraMath.ClampZoom(document.DesiredZoom),
            BrushSize = Math.Clamp(document.BrushSize, 1, 16),
            CanvasPanX = document.CanvasPanX,
            CanvasPanY = document.CanvasPanY,
            ShowGrid = document.ShowGrid,
            FramesPerSecond = Math.Clamp(document.FramesPerSecond, 1, 24),
            IsPlaying = false,
            PreviewFrameIndex = document.PreviewFrameIndex,
            ActiveTool = document.ActiveTool,
            ActivePaletteIndex = document.ActivePaletteIndex,
            ActiveFrameIndex = document.ActiveFrameIndex,
            ActiveLayerIndex = document.ActiveLayerIndex,
            Palette = palette,
            Frames = frames
        };
    }

    private static int[] NormalizePixelBuffer(IReadOnlyList<int> pixels, int pixelCount, int paletteCount)
    {
        int[] normalized = new int[pixelCount];
        Array.Fill(normalized, -1);
        int length = Math.Min(pixelCount, pixels.Count);
        for (int index = 0; index < length; index++)
        {
            int paletteIndex = pixels[index];
            normalized[index] = paletteIndex >= 0
                ? Math.Clamp(paletteIndex, 0, Math.Max(paletteCount - 1, 0))
                : -1;
        }

        return normalized;
    }

    private string ResolvePixelStudioDocumentDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_currentPixelDocumentPath))
        {
            string? currentDirectory = Path.GetDirectoryName(_currentPixelDocumentPath);
            if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
            {
                return currentDirectory;
            }
        }

        if (!string.IsNullOrWhiteSpace(_lastProjectPath) && Directory.Exists(_lastProjectPath))
        {
            return _lastProjectPath;
        }

        if (Directory.Exists(_projectLibraryPath))
        {
            return _projectLibraryPath;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    private string BuildSuggestedPixelStudioFileName()
    {
        string name = SanitizePixelStudioDocumentName(_pixelStudio.DocumentName).Replace(' ', '-');
        return string.IsNullOrWhiteSpace(name) ? "untitled-sprite.kearu" : $"{name}.kearu";
    }

    private static string EnsurePixelStudioDocumentExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(extension)
            ? $"{path}.kearu"
            : path;
    }

    private static string SanitizePixelStudioDocumentName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new string(value.Where(character => !invalidCharacters.Contains(character)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled-sprite" : sanitized;
    }

    private static string? FindPreferredPixelStudioDocument(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory) || !Directory.Exists(projectDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(projectDirectory, "*.kearu", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
            ?? Directory.EnumerateFiles(projectDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
    }

    private static PixelStudioFrameState CloneFrameState(PixelStudioFrameState frame)
    {
        return new PixelStudioFrameState
        {
            Name = frame.Name,
            Layers = frame.Layers.Select(CloneLayerState).ToList()
        };
    }

    private static PixelStudioLayerState CloneLayerState(PixelStudioLayerState layer)
    {
        return new PixelStudioLayerState
        {
            Name = layer.Name,
            IsVisible = layer.IsVisible,
            IsLocked = layer.IsLocked,
            Pixels = layer.Pixels.ToArray()
        };
    }

    private static List<ThemeColor> CreateDefaultPalette()
    {
        return
        [
            new ThemeColor(0.07f, 0.08f, 0.10f),
            new ThemeColor(0.98f, 0.98f, 0.97f),
            new ThemeColor(0.29f, 0.34f, 0.40f),
            new ThemeColor(0.66f, 0.72f, 0.78f),
            new ThemeColor(0.96f, 0.82f, 0.28f),
            new ThemeColor(0.95f, 0.53f, 0.22f),
            new ThemeColor(0.87f, 0.22f, 0.27f),
            new ThemeColor(0.93f, 0.55f, 0.67f),
            new ThemeColor(0.29f, 0.70f, 0.39f),
            new ThemeColor(0.20f, 0.70f, 0.72f),
            new ThemeColor(0.28f, 0.50f, 0.89f),
            new ThemeColor(0.49f, 0.34f, 0.85f),
            new ThemeColor(0.47f, 0.30f, 0.18f),
            new ThemeColor(0.98f, 0.90f, 0.75f),
            new ThemeColor(0.51f, 0.84f, 0.95f),
            new ThemeColor(0.14f, 0.17f, 0.23f)
        ];
    }

    private static PixelStudioFrameState CreateDemoFrame(string name, bool wink)
    {
        PixelStudioLayerState colorLayer = new()
        {
            Name = "Color",
            Pixels = CreateBlankPixels(16, 16)
        };

        PixelStudioLayerState detailLayer = new()
        {
            Name = "Details",
            Pixels = CreateBlankPixels(16, 16)
        };

        string[] colorPattern =
        [
            "................",
            ".....YYYYYY.....",
            "....YYYYYYYY....",
            "...YYY....YYY...",
            "..YY........YY..",
            "..YY........YY..",
            ".YY..........YY.",
            ".YY..........YY.",
            ".YY..........YY.",
            ".YY..........YY.",
            "..YY........YY..",
            "..YY........YY..",
            "...YYY....YYY...",
            "....YYYYYYYY....",
            ".....YYYYYY.....",
            "................"
        ];

        string[] detailPattern =
        [
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            wink ? ".YY..KKKK....YY." : ".YY..KK..KK..YY.",
            ".YY..KK..KK..YY.",
            ".YY..........YY.",
            ".YY..R....R..YY.",
            "..YY.RRRRRR.YY..",
            "..YY........YY..",
            "................",
            "................",
            "................",
            "................"
        ];

        ApplyPattern(colorLayer, colorPattern, new Dictionary<char, int> { ['Y'] = 4 });
        ApplyPattern(detailLayer, detailPattern, new Dictionary<char, int>
        {
            ['K'] = 0,
            ['R'] = 6
        });

        return new PixelStudioFrameState
        {
            Name = name,
            Layers = [colorLayer, detailLayer]
        };
    }

    private static void ApplyPattern(PixelStudioLayerState layer, IReadOnlyList<string> pattern, IReadOnlyDictionary<char, int> paletteMap)
    {
        for (int y = 0; y < pattern.Count; y++)
        {
            for (int x = 0; x < pattern[y].Length; x++)
            {
                char symbol = pattern[y][x];
                if (paletteMap.TryGetValue(symbol, out int paletteIndex))
                {
                    layer.Pixels[(y * 16) + x] = paletteIndex;
                }
            }
        }
    }

    private static int[] CreateBlankPixels(int width, int height)
    {
        int[] pixels = new int[width * height];
        Array.Fill(pixels, -1);
        return pixels;
    }

    private static string ToHex(ThemeColor color)
    {
        int r = Math.Clamp((int)MathF.Round(color.R * 255), 0, 255);
        int g = Math.Clamp((int)MathF.Round(color.G * 255), 0, 255);
        int b = Math.Clamp((int)MathF.Round(color.B * 255), 0, 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static float ClampColorChannel(float currentValue, int delta)
    {
        int scaled = Math.Clamp((int)MathF.Round(currentValue * 255) + delta, 0, 255);
        return scaled / 255f;
    }

    private void ApplyInitialSavedPaletteIfAvailable()
    {
        if (_savedPixelPalettes.Count == 0)
        {
            _activePixelPaletteId = null;
            _selectedPixelPaletteId = null;
            return;
        }

        SavedPixelPalette? activePalette = null;
        if (!string.IsNullOrWhiteSpace(_activePixelPaletteId))
        {
            activePalette = _savedPixelPalettes.FirstOrDefault(palette =>
                string.Equals(palette.Id, _activePixelPaletteId, StringComparison.Ordinal));
        }

        if (activePalette is null)
        {
            _activePixelPaletteId = null;
            _selectedPixelPaletteId ??= _savedPixelPalettes[0].Id;
            return;
        }

        _selectedPixelPaletteId = activePalette.Id;
        ReplaceCurrentPaletteColors(activePalette.Colors.Select(ToThemeColor).ToList(), remapArtwork: false);
        _paletteRenameBuffer = activePalette.Name;
    }

    private void MarkCurrentPaletteAsUnsaved()
    {
        _activePixelPaletteId = null;
        _selectedPixelPaletteId = DefaultPaletteSelectionId;
    }

    private string GetActivePaletteDisplayName()
    {
        if (string.IsNullOrWhiteSpace(_activePixelPaletteId))
        {
            return string.Equals(_selectedPixelPaletteId, DefaultPaletteSelectionId, StringComparison.Ordinal)
                ? "Default Palette"
                : "Current Palette";
        }

        return _savedPixelPalettes
            .FirstOrDefault(palette => string.Equals(palette.Id, _activePixelPaletteId, StringComparison.Ordinal))
            ?.Name ?? "Current Palette";
    }

    private void ApplySavedPalette(SavedPixelPalette palette)
    {
        ReplaceCurrentPaletteColors(palette.Colors.Select(ToThemeColor).ToList(), remapArtwork: false);
        _activePixelPaletteId = palette.Id;
        _selectedPixelPaletteId = palette.Id;
        _paletteRenameActive = false;
        _layerRenameActive = false;
        _paletteRenameBuffer = palette.Name;
        ClosePixelContextMenu();
    }

    private void ApplyDefaultPalette()
    {
        ReplaceCurrentPaletteColors(CreateDefaultPalette(), remapArtwork: false);
        _activePixelPaletteId = null;
        _selectedPixelPaletteId = DefaultPaletteSelectionId;
        _paletteRenameActive = false;
        _layerRenameActive = false;
        _paletteRenameBuffer = "Default Palette";
        ClosePixelContextMenu();
    }

    private SavedPixelPalette? GetSelectedSavedPalette()
    {
        if (_savedPixelPalettes.Count == 0)
        {
            return null;
        }

        SavedPixelPalette? selected = null;
        if (!string.IsNullOrWhiteSpace(_selectedPixelPaletteId))
        {
            selected = _savedPixelPalettes.FirstOrDefault(palette =>
                string.Equals(palette.Id, _selectedPixelPaletteId, StringComparison.Ordinal));
        }

        if (selected is null && !string.IsNullOrWhiteSpace(_activePixelPaletteId))
        {
            selected = _savedPixelPalettes.FirstOrDefault(palette =>
                string.Equals(palette.Id, _activePixelPaletteId, StringComparison.Ordinal));
        }

        selected ??= _savedPixelPalettes[0];
        _selectedPixelPaletteId = selected.Id;
        return selected;
    }

    private string BuildNextPaletteName()
    {
        HashSet<string> existingNames = _savedPixelPalettes
            .Select(palette => palette.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int paletteNumber = 1;
        string candidate = $"Palette {paletteNumber}";
        while (existingNames.Contains(candidate))
        {
            paletteNumber++;
            candidate = $"Palette {paletteNumber}";
        }

        return candidate;
    }

    private static string SanitizePaletteName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new string(value.Where(character => !invalidCharacters.Contains(character)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Palette" : sanitized;
    }

    private static string SanitizeLayerName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new string(value.Where(character => !invalidCharacters.Contains(character)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Layer" : sanitized;
    }

    private static string SanitizeFrameName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new string(value.Where(character => !invalidCharacters.Contains(character)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "Frame" : sanitized;
    }

    private void ReplaceSavedPalette(SavedPixelPalette palette)
    {
        int index = _savedPixelPalettes.FindIndex(existing => string.Equals(existing.Id, palette.Id, StringComparison.Ordinal));
        if (index < 0)
        {
            _savedPixelPalettes.Add(CloneSavedPixelPalette(palette));
            return;
        }

        _savedPixelPalettes[index] = CloneSavedPixelPalette(palette);
    }

    private void ReplaceCurrentPaletteColors(IReadOnlyList<ThemeColor> newPalette, bool remapArtwork)
    {
        List<ThemeColor> normalizedPalette = newPalette.Count > 0
            ? newPalette.ToList()
            : CreateDefaultPalette();

        List<ThemeColor> previousPalette = _pixelStudio.Palette.Count > 0
            ? _pixelStudio.Palette.ToList()
            : CreateDefaultPalette();

        if (remapArtwork)
        {
            RemapPixelIndices(previousPalette, normalizedPalette);
        }

        _pixelStudio.Palette.Clear();
        _pixelStudio.Palette.AddRange(normalizedPalette);
        _pixelStudio.ActivePaletteIndex = Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1);
    }

    private void RemapPixelIndices(IReadOnlyList<ThemeColor> previousPalette, IReadOnlyList<ThemeColor> newPalette)
    {
        if (previousPalette.Count == 0 || newPalette.Count == 0)
        {
            return;
        }

        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            foreach (PixelStudioLayerState layer in frame.Layers)
            {
                for (int pixelIndex = 0; pixelIndex < layer.Pixels.Length; pixelIndex++)
                {
                    int previousIndex = layer.Pixels[pixelIndex];
                    if (previousIndex < 0)
                    {
                        continue;
                    }

                    ThemeColor sourceColor = previousPalette[Math.Clamp(previousIndex, 0, previousPalette.Count - 1)];
                    layer.Pixels[pixelIndex] = FindNearestPaletteIndex(sourceColor, newPalette);
                }
            }
        }
    }

    private static int FindNearestPaletteIndex(ThemeColor sourceColor, IReadOnlyList<ThemeColor> palette)
    {
        if (palette.Count == 0)
        {
            return 0;
        }

        int bestIndex = 0;
        float bestDistance = float.MaxValue;
        for (int index = 0; index < palette.Count; index++)
        {
            ThemeColor candidate = palette[index];
            float dr = sourceColor.R - candidate.R;
            float dg = sourceColor.G - candidate.G;
            float db = sourceColor.B - candidate.B;
            float da = sourceColor.A - candidate.A;
            float distance = (dr * dr) + (dg * dg) + (db * db) + (da * da * 0.2f);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static ThemeColor ToThemeColor(PaletteColorSetting color)
    {
        return new ThemeColor(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    private static PaletteColorSetting ToPaletteColorSetting(ThemeColor color)
    {
        return new PaletteColorSetting
        {
            R = ToColorByte(color.R),
            G = ToColorByte(color.G),
            B = ToColorByte(color.B),
            A = ToColorByte(color.A)
        };
    }

    private static SavedPixelPalette CloneSavedPixelPalette(SavedPixelPalette palette)
    {
        return new SavedPixelPalette
        {
            Id = palette.Id,
            Name = palette.Name,
            Colors = palette.Colors
                .Select(color => new PaletteColorSetting
                {
                    R = color.R,
                    G = color.G,
                    B = color.B,
                    A = color.A
                })
                .ToList()
        };
    }

    private void SyncCanvasCameraFromLayout(string reason)
    {
        if (_layoutSnapshot?.PixelStudio is null)
        {
            return;
        }

        PixelStudioLayoutSnapshot layout = _layoutSnapshot.PixelStudio;
        PixelStudioCameraState camera = new(
            layout.CameraZoom,
            layout.CameraPanX,
            layout.CameraPanY,
            layout.CanvasViewportRect);
        ApplyCameraState(camera);
        LogCanvasCamera(reason, camera);
    }

    private void ApplyCameraState(PixelStudioCameraState camera)
    {
        _pixelStudio.DesiredZoom = camera.Zoom;
        _pixelStudio.CanvasPanX = camera.PanX;
        _pixelStudio.CanvasPanY = camera.PanY;
        _uiState.PixelStudio.Zoom = camera.Zoom;
        _uiState.PixelStudio.CanvasPanX = camera.PanX;
        _uiState.PixelStudio.CanvasPanY = camera.PanY;
    }

    private void LogCurrentCanvasCamera(string reason)
    {
        if (_layoutSnapshot?.PixelStudio is null)
        {
            return;
        }

        PixelStudioCameraState camera = PixelStudioCameraMath.Compute(
            _layoutSnapshot.PixelStudio.CanvasClipRect,
            _pixelStudio.CanvasWidth,
            _pixelStudio.CanvasHeight,
            _pixelStudio.DesiredZoom,
            _pixelStudio.CanvasPanX,
            _pixelStudio.CanvasPanY);
        LogCanvasCamera(reason, camera);
    }

    private static void LogCanvasCamera(string reason, PixelStudioCameraState camera)
    {
        Debug.WriteLine(
            $"{PixelStudioCameraLogPrefix} reason={reason} zoom={camera.Zoom} pan=({camera.PanX:0.##},{camera.PanY:0.##}) viewport=({camera.ViewportRect.Width:0.##}x{camera.ViewportRect.Height:0.##}) at ({camera.ViewportRect.X:0.##},{camera.ViewportRect.Y:0.##})");
    }

    private void MoveToolSettingsPanel(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        UiRect canvasBounds = layout.CanvasClipRect;
        float requestedX = _toolSettingsDragStartOffsetX + (mouseX - _toolSettingsDragStartMouseX);
        float requestedY = _toolSettingsDragStartOffsetY + (mouseY - _toolSettingsDragStartMouseY);
        const float snapThreshold = 18f;
        float maxOffsetX = Math.Max(canvasBounds.Width - layout.ToolSettingsPanelRect.Width, 0f);
        float maxOffsetY = Math.Max(canvasBounds.Height - layout.ToolSettingsPanelRect.Height, 0f);
        float clampedX = Math.Clamp(requestedX, 0f, maxOffsetX);
        float clampedY = Math.Clamp(requestedY, 0f, maxOffsetY);

        if (MathF.Abs(clampedX) <= snapThreshold)
        {
            clampedX = 0f;
        }
        else if (MathF.Abs(maxOffsetX - clampedX) <= snapThreshold)
        {
            clampedX = maxOffsetX;
        }

        if (MathF.Abs(clampedY) <= snapThreshold)
        {
            clampedY = 0f;
        }
        else if (MathF.Abs(maxOffsetY - clampedY) <= snapThreshold)
        {
            clampedY = maxOffsetY;
        }

        bool xChanged = !float.IsFinite(_pixelToolSettingsPanelOffsetX) || MathF.Abs(_pixelToolSettingsPanelOffsetX - clampedX) > 0.1f;
        bool yChanged = !float.IsFinite(_pixelToolSettingsPanelOffsetY) || MathF.Abs(_pixelToolSettingsPanelOffsetY - clampedY) > 0.1f;
        if (xChanged || yChanged)
        {
            _pixelToolSettingsPanelOffsetX = clampedX;
            _pixelToolSettingsPanelOffsetY = clampedY;
            UiRect updatedPanelRect = new(
                canvasBounds.X + clampedX,
                canvasBounds.Y + clampedY,
                layout.ToolSettingsPanelRect.Width,
                layout.ToolSettingsPanelRect.Height);
            ReplacePixelStudioLayoutSnapshot(layout.WithToolSettingsPanel(updatedPanelRect));
            RefreshPixelStudioInteraction();
        }
    }

    private static byte ToColorByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }
}
