using System.Diagnostics;
using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Themes;
using Silk.NET.Input;

namespace ProjectSPlus.App.Editor;

public sealed partial class EditorWindowScene
{
    private enum PixelStudioDragMode
    {
        None,
        ResizeToolsPanel,
        ResizeSidebar
    }

    private readonly Stack<PixelStudioState> _pixelUndoStack = [];
    private readonly Stack<PixelStudioState> _pixelRedoStack = [];

    private PixelStudioState? _strokeSnapshot;
    private bool _isPixelStrokeActive;
    private bool _strokeChanged;
    private int _lastStrokeCellIndex = -1;
    private long _lastPlaybackTick;

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
            IndexedRect? layerRow = layout.LayerRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (layerRow is not null)
            {
                DeletePixelLayer(layerRow.Index);
                return true;
            }

            return false;
        }

        if (button != MouseButton.Left)
        {
            return false;
        }

        if (layout.LeftCollapseHandleRect.Contains(mouseX, mouseY))
        {
            TogglePixelToolsCollapse();
            return true;
        }

        if (layout.RightCollapseHandleRect.Contains(mouseX, mouseY))
        {
            TogglePixelSidebarCollapse();
            return true;
        }

        if (layout.LeftSplitterRect.Contains(mouseX, mouseY))
        {
            _pixelDragMode = PixelStudioDragMode.ResizeToolsPanel;
            return true;
        }

        if (layout.RightSplitterRect.Contains(mouseX, mouseY))
        {
            _pixelDragMode = PixelStudioDragMode.ResizeSidebar;
            return true;
        }

        ActionRect<PixelStudioToolKind>? toolButton = layout.ToolButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (toolButton is not null)
        {
            ExecutePixelStudioTool(toolButton.Action);
            return true;
        }

        if (TryHandlePixelAction(layout.DocumentButtons, mouseX, mouseY)
            || TryHandlePixelAction(layout.CanvasButtons, mouseX, mouseY)
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
            RefreshPixelStudioView($"Pixel Studio color set to {BuildPixelStudioViewState().ActiveColorHex}.");
            return true;
        }

        IndexedRect? visibilityButton = layout.LayerVisibilityButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (visibilityButton is not null)
        {
            TogglePixelLayerVisibility(visibilityButton.Index);
            return true;
        }

        IndexedRect? layerRowHit = layout.LayerRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (layerRowHit is not null)
        {
            StopPixelPlayback();
            _pixelStudio.ActiveLayerIndex = Math.Clamp(layerRowHit.Index, 0, CurrentPixelFrame.Layers.Count - 1);
            RefreshPixelStudioView($"Pixel Studio layer selected: {CurrentPixelFrame.Layers[_pixelStudio.ActiveLayerIndex].Name}.");
            return true;
        }

        IndexedRect? frameRow = layout.FrameRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (frameRow is not null)
        {
            StopPixelPlayback();
            _pixelStudio.ActiveFrameIndex = Math.Clamp(frameRow.Index, 0, _pixelStudio.Frames.Count - 1);
            _pixelStudio.PreviewFrameIndex = _pixelStudio.ActiveFrameIndex;
            _pixelStudio.ActiveLayerIndex = Math.Min(_pixelStudio.ActiveLayerIndex, CurrentPixelFrame.Layers.Count - 1);
            RefreshPixelStudioView($"Pixel Studio frame selected: {CurrentPixelFrame.Name}.");
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
            _pixelDragMode = PixelStudioDragMode.None;
            EndPixelStroke();
        }
    }

    private void HandlePixelStudioMouseMove(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (!_isPixelStrokeActive || !TryGetCanvasCellIndex(layout, mouseX, mouseY, out int cellIndex))
        {
            return;
        }

        ApplyStrokeCell(cellIndex);
    }

    private bool HandlePixelStudioLayoutDrag(PixelStudioLayoutSnapshot layout, float mouseX)
    {
        if (_pixelDragMode == PixelStudioDragMode.None)
        {
            return false;
        }

        switch (_pixelDragMode)
        {
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

        ExecutePixelStudioAction(button.Action);
        return true;
    }

    private bool TryGetCanvasCellIndex(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, out int cellIndex)
    {
        cellIndex = -1;
        if (!layout.CanvasViewportRect.Contains(mouseX, mouseY))
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
        switch (_pixelStudio.ActiveTool)
        {
            case PixelStudioToolKind.Pencil:
            case PixelStudioToolKind.Eraser:
                BeginPixelStroke();
                ApplyStrokeCell(cellIndex);
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
    }

    private void ApplyStrokeCell(int cellIndex)
    {
        if (!_isPixelStrokeActive || cellIndex == _lastStrokeCellIndex || cellIndex < 0 || cellIndex >= CurrentPixelLayer.Pixels.Length)
        {
            return;
        }

        int nextValue = _pixelStudio.ActiveTool == PixelStudioToolKind.Eraser
            ? -1
            : _pixelStudio.ActivePaletteIndex;

        if (CurrentPixelLayer.Pixels[cellIndex] == nextValue)
        {
            _lastStrokeCellIndex = cellIndex;
            return;
        }

        CurrentPixelLayer.Pixels[cellIndex] = nextValue;
        _lastStrokeCellIndex = cellIndex;
        _strokeChanged = true;
        RefreshPixelStudioView(_pixelStudio.ActiveTool == PixelStudioToolKind.Eraser
            ? "Erasing pixels..."
            : $"Painting with {BuildPixelStudioViewState().ActiveColorHex}.");
    }

    private void EndPixelStroke()
    {
        if (!_isPixelStrokeActive)
        {
            return;
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
    }

    private void ExecutePixelStudioTool(PixelStudioToolKind tool)
    {
        StopPixelPlayback();
        _pixelStudio.ActiveTool = tool;
        RefreshPixelStudioView($"Pixel Studio tool: {tool}.");
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
            case PixelStudioAction.LoadDemoDocument:
                ApplyPixelStudioChange("Loaded the Project S+ smiley demo.", () =>
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
                _pixelStudio.DesiredZoom = Math.Max(_pixelStudio.DesiredZoom - 2, 2);
                RefreshPixelStudioView($"Pixel Studio zoom target set to {_pixelStudio.DesiredZoom}x.", rebuildLayout: true);
                break;
            case PixelStudioAction.ZoomIn:
                _pixelStudio.DesiredZoom = Math.Min(_pixelStudio.DesiredZoom + 2, 64);
                RefreshPixelStudioView($"Pixel Studio zoom target set to {_pixelStudio.DesiredZoom}x.", rebuildLayout: true);
                break;
            case PixelStudioAction.ToggleGrid:
                _pixelStudio.ShowGrid = !_pixelStudio.ShowGrid;
                RefreshPixelStudioView(_pixelStudio.ShowGrid ? "Pixel Studio grid enabled." : "Pixel Studio grid hidden.");
                break;
            case PixelStudioAction.TogglePaletteLibrary:
                _paletteLibraryVisible = !_paletteLibraryVisible;
                _paletteRenameActive = false;
                RefreshPixelStudioView(_paletteLibraryVisible ? "Opened palette library." : "Closed palette library.", rebuildLayout: true);
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
            _pixelStudio.DesiredZoom = Math.Min(_pixelStudio.DesiredZoom, width >= 128 || height >= 128 ? 8 : 24);
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
                ? $"Pixel Studio layer hidden: {CurrentPixelFrame.Layers[layerIndex].Name}."
                : $"Pixel Studio layer shown: {CurrentPixelFrame.Layers[layerIndex].Name}.",
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
            _renderer?.UpdateLayoutSnapshot(_layoutSnapshot);
        }

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

    private PixelStudioViewState BuildPixelStudioViewState()
    {
        EnsurePixelStudioIndices();
        ThemeColor activeColor = _pixelStudio.Palette[Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1)];

        return new PixelStudioViewState
        {
            DocumentName = _pixelStudio.DocumentName,
            CanvasWidth = _pixelStudio.CanvasWidth,
            CanvasHeight = _pixelStudio.CanvasHeight,
            Zoom = _pixelStudio.DesiredZoom,
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
            PromptForPaletteGenerationAfterImport = _promptForPaletteGenerationAfterImport,
            ToolsPanelPreferredWidth = _pixelToolsPanelWidth,
            SidebarPreferredWidth = _pixelSidebarWidth,
            ToolsPanelCollapsed = _pixelToolsCollapsed,
            SidebarCollapsed = _pixelSidebarCollapsed,
            PaletteSwatchScrollRow = _paletteSwatchScrollRow,
            SavedPaletteScrollRow = _savedPaletteScrollRow,
            LayerScrollRow = _layerScrollRow,
            FrameScrollRow = _frameScrollRow,
            PaletteRenameBuffer = _paletteRenameBuffer,
            Palette = _pixelStudio.Palette.ToList(),
            CompositePixels = ComposeVisiblePixels(CurrentPixelFrame),
            PreviewPixels = ComposeVisiblePixels(PreviewPixelFrame),
            SavedPalettes = BuildSavedPaletteViews(),
            Layers = CurrentPixelFrame.Layers
                .Select((layer, index) => new PixelStudioLayerView
                {
                    Name = layer.Name,
                    IsVisible = layer.IsVisible,
                    IsActive = index == _pixelStudio.ActiveLayerIndex
                })
                .ToList(),
            Frames = _pixelStudio.Frames
                .Select((frame, index) => new PixelStudioFrameView
                {
                    Name = frame.Name,
                    IsActive = index == _pixelStudio.ActiveFrameIndex,
                    IsPreviewing = index == _pixelStudio.PreviewFrameIndex
                })
                .ToList()
        };
    }

    private bool HandlePixelStudioTextKeyDown(Key key)
    {
        if (!_paletteRenameActive)
        {
            return false;
        }

        switch (key)
        {
            case Key.Backspace:
                if (_paletteRenameBuffer.Length > 0)
                {
                    _paletteRenameBuffer = _paletteRenameBuffer[..^1];
                    RefreshPixelStudioView("Editing palette name.");
                }

                return true;
            case Key.Enter:
                CommitPaletteRename();
                return true;
            case Key.Escape:
                CancelPaletteRename();
                return true;
            default:
                return true;
        }
    }

    private bool HandlePixelStudioTextInput(char character)
    {
        if (!_paletteRenameActive || char.IsControl(character))
        {
            return false;
        }

        _paletteRenameBuffer += character;
        RefreshPixelStudioView("Editing palette name.");
        return true;
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

    private void SelectAndApplySavedPalette(int index)
    {
        if (index < 0 || index >= _savedPixelPalettes.Count)
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
        RefreshPixelStudioView($"Renamed palette to {name}.", rebuildLayout: true);
    }

    private void CancelPaletteRename()
    {
        _paletteRenameActive = false;
        _paletteRenameBuffer = string.Empty;
        RefreshPixelStudioView("Palette rename cancelled.", rebuildLayout: true);
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
            DocumentName = "Project S+ Demo",
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
        _pixelStudio.DocumentName = source.DocumentName;
        _pixelStudio.CanvasWidth = source.CanvasWidth;
        _pixelStudio.CanvasHeight = source.CanvasHeight;
        _pixelStudio.DesiredZoom = source.DesiredZoom;
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
    }

    private string GetActivePaletteDisplayName()
    {
        if (string.IsNullOrWhiteSpace(_activePixelPaletteId))
        {
            return "Current Palette";
        }

        return _savedPixelPalettes
            .FirstOrDefault(palette => string.Equals(palette.Id, _activePixelPaletteId, StringComparison.Ordinal))
            ?.Name ?? "Current Palette";
    }

    private void ApplySavedPalette(SavedPixelPalette palette)
    {
        ReplaceCurrentPaletteColors(palette.Colors.Select(ToThemeColor).ToList(), remapArtwork: true);
        _activePixelPaletteId = palette.Id;
        _selectedPixelPaletteId = palette.Id;
        _paletteRenameActive = false;
        _paletteRenameBuffer = palette.Name;
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

    private static byte ToColorByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }
}
