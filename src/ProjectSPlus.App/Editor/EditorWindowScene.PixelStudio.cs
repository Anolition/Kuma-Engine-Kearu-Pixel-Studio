using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectSPlus.App;
using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Themes;
using Silk.NET.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;

namespace ProjectSPlus.App.Editor;

public sealed partial class EditorWindowScene
{
    private const string DefaultPaletteSelectionId = "__default_palette__";
    private const string PixelStudioCameraLogPrefix = "[PixelStudioCamera]";
    private const int MaxPixelCanvasDimension = 1024;
    private const int TransparentPixelValue = 0;
    private const int ClipboardEmptyPixel = int.MinValue;
    private static readonly TimeSpan PixelStudioAutosaveIdleDelay = TimeSpan.FromSeconds(1.35);
    private static readonly PixelStudioResizeAnchor[] TransformPivotPresetOrder =
    [
        PixelStudioResizeAnchor.TopLeft,
        PixelStudioResizeAnchor.Top,
        PixelStudioResizeAnchor.TopRight,
        PixelStudioResizeAnchor.Left,
        PixelStudioResizeAnchor.Center,
        PixelStudioResizeAnchor.Right,
        PixelStudioResizeAnchor.BottomLeft,
        PixelStudioResizeAnchor.Bottom,
        PixelStudioResizeAnchor.BottomRight
    ];

    private enum PixelStudioDragMode
    {
        None,
        PanCanvas,
        MoveSelection,
        TransformSelection,
        MoveMirrorAxis,
        ReorderPaletteSwatch,
        ReorderLayer,
        ReorderFrame,
        AdjustBrushSize,
        AdjustOnionOpacity,
        AdjustLayerOpacity,
        AdjustPaletteColorPicker,
        MoveToolSettingsDock,
        MoveNavigatorPanel,
        NavigatePreview,
        ResizeNavigatorPanel,
        ResizeToolsPanel,
        ResizeSidebar
    }

    private enum PaletteColorAdjustMode
    {
        None,
        Field,
        WheelHue,
        WheelField,
        Alpha
    }

    private readonly struct PixelStudioEditorToolState
    {
        public PixelStudioEditorToolState(
            PixelStudioToolKind activeTool,
            int brushSize,
            int pencilRoundBrushSize,
            int pencilHardBrushSize,
            int pencilPixelPerfectBrushSize,
            int eraserRoundBrushSize,
            int eraserHardBrushSize,
            int eraserPixelPerfectBrushSize,
            int lineBrushSize,
            PixelStudioBrushMode brushMode,
            int activePaletteIndex,
            PixelStudioSelectionMode selectionMode,
            PixelStudioMirrorMode mirrorMode,
            float mirrorAxisX,
            float mirrorAxisY,
            PixelStudioShapeRenderMode rectangleRenderMode,
            PixelStudioShapeRenderMode ellipseRenderMode,
            PixelStudioShapePreset shapePreset,
            PixelStudioShapeRenderMode shapeRenderMode,
            IReadOnlyList<string> collapsedLayerGroupIds)
        {
            ActiveTool = activeTool;
            BrushSize = brushSize;
            PencilRoundBrushSize = pencilRoundBrushSize;
            PencilHardBrushSize = pencilHardBrushSize;
            PencilPixelPerfectBrushSize = pencilPixelPerfectBrushSize;
            EraserRoundBrushSize = eraserRoundBrushSize;
            EraserHardBrushSize = eraserHardBrushSize;
            EraserPixelPerfectBrushSize = eraserPixelPerfectBrushSize;
            LineBrushSize = lineBrushSize;
            BrushMode = brushMode;
            ActivePaletteIndex = activePaletteIndex;
            SelectionMode = selectionMode;
            MirrorMode = mirrorMode;
            MirrorAxisX = mirrorAxisX;
            MirrorAxisY = mirrorAxisY;
            RectangleRenderMode = rectangleRenderMode;
            EllipseRenderMode = ellipseRenderMode;
            ShapePreset = shapePreset;
            ShapeRenderMode = shapeRenderMode;
            CollapsedLayerGroupIds = collapsedLayerGroupIds;
        }

        public PixelStudioToolKind ActiveTool { get; }

        public int BrushSize { get; }

        public int PencilRoundBrushSize { get; }

        public int PencilHardBrushSize { get; }

        public int PencilPixelPerfectBrushSize { get; }

        public int EraserRoundBrushSize { get; }

        public int EraserHardBrushSize { get; }

        public int EraserPixelPerfectBrushSize { get; }

        public int LineBrushSize { get; }

        public PixelStudioBrushMode BrushMode { get; }

        public int ActivePaletteIndex { get; }

        public PixelStudioSelectionMode SelectionMode { get; }

        public PixelStudioMirrorMode MirrorMode { get; }

        public float MirrorAxisX { get; }

        public float MirrorAxisY { get; }

        public PixelStudioShapeRenderMode RectangleRenderMode { get; }

        public PixelStudioShapeRenderMode EllipseRenderMode { get; }

        public PixelStudioShapePreset ShapePreset { get; }

        public PixelStudioShapeRenderMode ShapeRenderMode { get; }

        public IReadOnlyList<string> CollapsedLayerGroupIds { get; }
    }

    private enum NavigatorResizeCorner
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private enum CanvasResizeInputField
    {
        None,
        Width,
        Height
    }

    private enum PixelStudioWarningDialogKind
    {
        None,
        ResizeCanvas,
        ScaleSelectionUp,
        ScaleSelectionDown,
        HiddenLayerEdit,
        LockedLayerEdit,
        AlphaLockedLayerEdit,
        ModifyLockedPalette,
        DeleteLockedSavedPalette,
        DeleteLockedPaletteSwatch,
        DeleteUsedPaletteSwatch,
        ApplyPaletteWithArtworkColors
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

    private static readonly (float X, float Y)[] StarPolygonPoints = BuildRegularStarPolygon(5, 0.98f, 0.42f, -MathF.PI * 0.5f);
    private static readonly (float X, float Y)[] TrianglePolygonPoints =
    [
        (0f, -0.98f),
        (0.92f, 0.86f),
        (-0.92f, 0.86f)
    ];
    private static readonly (float X, float Y)[] DiamondPolygonPoints =
    [
        (0f, -0.98f),
        (0.92f, 0f),
        (0f, 0.98f),
        (-0.92f, 0f)
    ];
    private static readonly (float X, float Y)[] TeardropPolygonPoints =
    [
        (0f, -1f),
        (0.28f, -0.72f),
        (0.55f, -0.18f),
        (0.58f, 0.36f),
        (0.34f, 0.84f),
        (0f, 1f),
        (-0.34f, 0.84f),
        (-0.58f, 0.36f),
        (-0.55f, -0.18f),
        (-0.28f, -0.72f)
    ];
    private static readonly (float X, float Y)[] HeartPolygonPoints = BuildHeartPolygonPoints();

    private readonly Stack<PixelStudioState> _pixelUndoStack = [];
    private readonly Stack<PixelStudioState> _pixelRedoStack = [];
    private readonly HashSet<int> _pixelDirtyIndices = [];
    private readonly HashSet<int> _linePreviewIndices = [];
    private readonly HashSet<int> _selectionMask = [];

    private PixelStudioState? _strokeSnapshot;
    private PixelStudioFrameState? _frameClipboard;
    private bool _isPixelStrokeActive;
    private bool _strokeChanged;
    private int _lastStrokeCellIndex = -1;
    private long _lastPlaybackTick;
    private int _pixelPlaybackDirection = 1;
    private int? _contextPaletteIndex;
    private int? _contextPaletteSwatchIndex;
    private int? _contextLayerIndex;
    private string? _contextLayerGroupId;
    private int? _contextFrameIndex;
    private bool _contextSelectionActive;
    private bool _contextOnionControlsActive;
    private PixelStudioToolKind? _contextToolMenuTool;
    private string? _currentPixelDocumentPath;
    private string? _pixelStudioLastSavedSnapshotJson;
    private string? _pixelStudioLastAutosavedSnapshotJson;
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
    private bool _frameDurationFieldActive;
    private string _frameDurationBuffer = string.Empty;
    private bool _layerRenameTargetsGroup;
    private string? _layerRenameGroupId;
    private bool _selectionActive;
    private bool _selectionCommitted;
    private bool _selectionDragActive;
    private int _selectionStartX;
    private int _selectionStartY;
    private int _selectionEndX;
    private int _selectionEndY;
    private bool _selectionMoveActive;
    private int _selectionMovePointerCellX;
    private int _selectionMovePointerCellY;
    private int _selectionMoveOriginLeft;
    private int _selectionMoveOriginTop;
    private int _selectionMoveCurrentLeft;
    private int _selectionMoveCurrentTop;
    private int _selectionMoveWidth;
    private int _selectionMoveHeight;
    private int[]? _selectionMovePixels;
    private int[]? _selectionMoveLayerSnapshot;
    private int[]? _selectionMoveSourceIndices;
    private bool _selectionMoveUsesMask;
    private PixelStudioState? _selectionMoveSnapshot;
    private int[]? _selectionClipboardPixels;
    private int _selectionClipboardWidth;
    private int _selectionClipboardHeight;
    private PixelStudioSelectionMode _selectionMode = PixelStudioSelectionMode.Box;
    private PixelStudioMirrorMode _mirrorMode;
    private PixelStudioBrushMode _brushMode = PixelStudioBrushMode.Round;
    private int _pencilRoundBrushSize = 1;
    private int _pencilHardBrushSize = 1;
    private int _pencilPixelPerfectBrushSize = 1;
    private int _eraserRoundBrushSize = 1;
    private int _eraserHardBrushSize = 1;
    private int _eraserPixelPerfectBrushSize = 1;
    private int _lineBrushSize = 1;
    private PixelStudioShapeRenderMode _rectangleRenderMode = PixelStudioShapeRenderMode.Outline;
    private PixelStudioShapeRenderMode _ellipseRenderMode = PixelStudioShapeRenderMode.Outline;
    private PixelStudioShapePreset _shapePreset = PixelStudioShapePreset.Star;
    private PixelStudioShapeRenderMode _shapeRenderMode = PixelStudioShapeRenderMode.Outline;
    private bool _selectionTransformModeActive;
    private bool _layerOpacityControlsVisible;
    private int _selectionMaskLeft;
    private int _selectionMaskTop;
    private int _selectionMaskRight;
    private int _selectionMaskBottom;
    private bool _shiftPressed;
    private bool _altPressed;
    private bool _canvasResizeDialogVisible;
    private string _canvasResizeWidthBuffer = "32";
    private string _canvasResizeHeightBuffer = "32";
    private CanvasResizeInputField _canvasResizeActiveField = CanvasResizeInputField.Width;
    private PixelStudioResizeAnchor _canvasResizeAnchor = PixelStudioResizeAnchor.TopLeft;
    private bool _canvasResizeWouldCrop;
    private int _canvasResizeCroppedPixelCount;
    private bool _pixelWarningDialogVisible;
    private PixelStudioWarningDialogKind _pixelWarningDialogKind;
    private string _pixelWarningDialogTitle = string.Empty;
    private string _pixelWarningDialogMessage = string.Empty;
    private int _pixelWarningTargetLayerIndex = -1;
    private int _pixelWarningResizeWidth;
    private int _pixelWarningResizeHeight;
    private int _pixelWarningPaletteSwatchIndex = -1;
    private int _pixelWarningPaletteSwapTargetIndex = -1;
    private int _pixelWarningPaletteTargetIndex = int.MinValue;
    private Action? _pixelWarningConfirmAction;
    private string? _lockedPixelPaletteSourceId;
    private List<ThemeColor?> _pixelCompositePixels = [];
    private List<ThemeColor?> _pixelOnionPreviousPixels = [];
    private List<ThemeColor?> _pixelOnionNextPixels = [];
    private List<ThemeColor?> _pixelPreviewPixels = [];
    private int _pixelCompositeRevision;
    private int _pixelOnionPreviousRevision;
    private int _pixelOnionNextRevision;
    private int _pixelPreviewRevision;
    private int _pixelCompositeFrameIndex = -1;
    private int _pixelOnionPreviousFrameIndex = -1;
    private int _pixelOnionNextFrameIndex = -1;
    private int _pixelPreviewFrameIndex = -1;
    private bool _contextClipboardActive;
    private bool _pixelStudioAutosavePending;
    private bool _pixelRecoveryOwnedByCurrentSession;
    private float _navigatorDragStartMouseX;
    private float _navigatorDragStartMouseY;
    private float _navigatorDragStartOffsetX;
    private float _navigatorDragStartOffsetY;
    private bool _pixelAnimationPreviewVisible = true;
    private float _pixelAnimationPreviewPanelOffsetX = float.NaN;
    private float _pixelAnimationPreviewPanelOffsetY = float.NaN;
    private float _pixelAnimationPreviewPanelWidth = float.NaN;
    private float _pixelAnimationPreviewPanelHeight = float.NaN;
    private int _layerDragSourceIndex = -1;
    private int _layerDragInsertIndex = -1;
    private float _layerDragStartMouseX;
    private float _layerDragStartMouseY;
    private bool _layerDragMoved;
    private string? _layerDragJoinGroupId;
    private int _layerDragJoinAnchorLayerIndex = -1;
    private int _paletteSwatchDragSourceIndex = -1;
    private int _paletteSwatchDragTargetIndex = -1;
    private float _paletteSwatchDragStartMouseX;
    private float _paletteSwatchDragStartMouseY;
    private bool _paletteSwatchDragMoved;
    private int _frameDragSourceIndex = -1;
    private int _frameDragInsertIndex = -1;
    private float _frameDragStartMouseX;
    private float _frameDragStartMouseY;
    private bool _frameDragMoved;
    private NavigatorResizeCorner _navigatorResizeCorner;
    private float _navigatorResizeStartMouseX;
    private float _navigatorResizeStartMouseY;
    private DateTimeOffset _pixelStudioLastMutationAt = DateTimeOffset.MinValue;
    private DateTimeOffset _pixelStudioLastAutosaveAt = DateTimeOffset.MinValue;
    private DateTimeOffset _pixelStudioAutosaveIndicatorUntil = DateTimeOffset.MinValue;
    private int _pixelRecoveryBackupCount = PixelStudioRecoveryManager.GetBackupCount();
    private float _navigatorResizeStartOffsetX;
    private float _navigatorResizeStartOffsetY;
    private float _navigatorResizeStartWidth;
    private float _navigatorResizeStartHeight;
    private PixelStudioState? _paletteColorAdjustSnapshot;
    private PixelStudioState? _layerOpacityAdjustSnapshot;
    private bool _paletteColorAdjustChanged;
    private bool _layerOpacityAdjustChanged;
    private PaletteColorAdjustMode _paletteColorAdjustMode;
    private string _hoveredPixelHelpTitle = string.Empty;
    private string _hoveredPixelHelpBody = string.Empty;
    private long _hoveredPixelHelpStartTick;
    private bool _hoveredPixelToolTooltipVisible;
    private float _pixelMouseX;
    private float _pixelMouseY;
    private ThemeColor _secondaryPaletteColor;
    private bool _secondaryPaletteColorInitialized;
    private readonly List<ThemeColor> _recentPaletteColors = [];
    private PixelStudioSelectionHandleKind _selectionTransformHandleKind;
    private int _selectionTransformSourceLeft;
    private int _selectionTransformSourceTop;
    private int _selectionTransformSourceWidth;
    private int _selectionTransformSourceHeight;
    private int _selectionTransformPreviewLeft;
    private int _selectionTransformPreviewTop;
    private int _selectionTransformPreviewWidth;
    private int _selectionTransformPreviewHeight;
    private float _selectionTransformPreviewRotationDegrees;
    private bool _selectionTransformPreviewVisible;
    private float _selectionTransformPivotX = float.NaN;
    private float _selectionTransformPivotY = float.NaN;
    private bool _selectionTransformAngleFieldActive;
    private string _selectionTransformAngleBuffer = string.Empty;
    private bool _selectionTransformScaleXFieldActive;
    private bool _selectionTransformScaleYFieldActive;
    private string _selectionTransformScaleXBuffer = string.Empty;
    private string _selectionTransformScaleYBuffer = string.Empty;
    private float _selectionTransformPreviewScaleX = 1f;
    private float _selectionTransformPreviewScaleY = 1f;
    private bool _pixelRecoveryBannerVisible;
    private float _mirrorAxisX = float.NaN;
    private float _mirrorAxisY = float.NaN;
    private PixelStudioMirrorAxisKind _mirrorAxisDragKind;
    private readonly List<int> _pixelPerfectStrokeCells = [];
    private readonly HashSet<string> _collapsedLayerGroupIds = new(StringComparer.Ordinal);

    private bool HandlePixelStudioKeyDown(IKeyboard keyboard, Key key)
    {
        bool controlPressed = keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight);
        if (HandleSelectionTransformKeyLockout(controlPressed, key))
        {
            return true;
        }

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

        if (controlPressed && key == Key.C)
        {
            CopySelectionPixels();
            return true;
        }

        if (controlPressed && key == Key.X)
        {
            CutSelectionPixels();
            return true;
        }

        if (controlPressed && key == Key.V)
        {
            PasteSelectionPixels();
            return true;
        }

        if (controlPressed && key == Key.D)
        {
            ExecutePixelStudioAction(PixelStudioAction.ClearSelection);
            return true;
        }

        if (!controlPressed && _selectionCommitted)
        {
            switch (key)
            {
                case Key.Left:
                    ExecutePixelStudioAction(PixelStudioAction.NudgeSelectionLeft);
                    return true;
                case Key.Right:
                    ExecutePixelStudioAction(PixelStudioAction.NudgeSelectionRight);
                    return true;
                case Key.Up:
                    ExecutePixelStudioAction(PixelStudioAction.NudgeSelectionUp);
                    return true;
                case Key.Down:
                    ExecutePixelStudioAction(PixelStudioAction.NudgeSelectionDown);
                    return true;
            }
        }

        if (key == Key.Space)
        {
            ExecutePixelStudioAction(PixelStudioAction.TogglePlayback);
            return true;
        }

        if (!controlPressed && MatchesShortcut(ShortcutAction.SwapSecondaryColor, key))
        {
            ExecutePixelStudioAction(PixelStudioAction.SwapSecondaryColor);
            return true;
        }

        return false;
    }

    private void NormalizeLoopRangeState()
    {
        if (_pixelStudio.Frames.Count == 0)
        {
            _pixelStudio.LoopRangeEnabled = false;
            _pixelStudio.LoopStartFrameIndex = 0;
            _pixelStudio.LoopEndFrameIndex = 0;
            return;
        }

        int lastFrameIndex = _pixelStudio.Frames.Count - 1;
        int start = Math.Clamp(_pixelStudio.LoopStartFrameIndex, 0, lastFrameIndex);
        int end = Math.Clamp(_pixelStudio.LoopEndFrameIndex, 0, lastFrameIndex);
        if (start > end)
        {
            (start, end) = (end, start);
        }

        bool fullRange = start == 0 && end == lastFrameIndex;
        _pixelStudio.LoopRangeEnabled = _pixelStudio.LoopRangeEnabled && !fullRange;
        _pixelStudio.LoopStartFrameIndex = _pixelStudio.LoopRangeEnabled ? start : 0;
        _pixelStudio.LoopEndFrameIndex = _pixelStudio.LoopRangeEnabled ? end : lastFrameIndex;
    }

    private void NormalizeOnionDisplayState()
    {
        if (_pixelStudio.AllowDualOnion)
        {
            if (_pixelStudio.ShowOnionSkin
                && !_pixelStudio.ShowPreviousOnion
                && !_pixelStudio.ShowNextOnion)
            {
                _pixelStudio.ShowPreviousOnion = true;
            }

            return;
        }

        if (_pixelStudio.ShowPreviousOnion && _pixelStudio.ShowNextOnion)
        {
            _pixelStudio.ShowNextOnion = false;
        }

        if (_pixelStudio.ShowOnionSkin
            && !_pixelStudio.ShowPreviousOnion
            && !_pixelStudio.ShowNextOnion)
        {
            _pixelStudio.ShowPreviousOnion = true;
        }
    }

    private void SetOnionDisplayMode(bool showPrevious, bool showNext, bool allowDual, string status)
    {
        _pixelStudio.AllowDualOnion = allowDual;
        _pixelStudio.ShowPreviousOnion = showPrevious;
        _pixelStudio.ShowNextOnion = showNext;
        NormalizeOnionDisplayState();
        RefreshPixelStudioView(status, rebuildLayout: true, refreshPixelBuffers: true);
    }

    private bool TryGetLoopRange(out int startFrameIndex, out int endFrameIndex)
    {
        NormalizeLoopRangeState();
        if (_pixelStudio.Frames.Count == 0 || !_pixelStudio.LoopRangeEnabled)
        {
            startFrameIndex = 0;
            endFrameIndex = Math.Max(_pixelStudio.Frames.Count - 1, 0);
            return false;
        }

        startFrameIndex = Math.Clamp(_pixelStudio.LoopStartFrameIndex, 0, _pixelStudio.Frames.Count - 1);
        endFrameIndex = Math.Clamp(_pixelStudio.LoopEndFrameIndex, startFrameIndex, _pixelStudio.Frames.Count - 1);
        return true;
    }

    private int GetPlaybackStartFrameIndex()
    {
        if (!TryGetLoopRange(out int startFrameIndex, out int endFrameIndex))
        {
            return Math.Clamp(_pixelStudio.ActiveFrameIndex, 0, Math.Max(_pixelStudio.Frames.Count - 1, 0));
        }

        int activeFrameIndex = Math.Clamp(_pixelStudio.ActiveFrameIndex, 0, _pixelStudio.Frames.Count - 1);
        return activeFrameIndex >= startFrameIndex && activeFrameIndex <= endFrameIndex
            ? activeFrameIndex
            : startFrameIndex;
    }

    private int GetNextPlaybackFrameIndex(int currentFrameIndex, int playbackStartFrameIndex, int playbackEndFrameIndex)
    {
        if (_pixelStudio.PlaybackLoopMode != PixelStudioPlaybackLoopMode.PingPong || playbackStartFrameIndex >= playbackEndFrameIndex)
        {
            _pixelPlaybackDirection = 1;
            return currentFrameIndex >= playbackEndFrameIndex
                ? playbackStartFrameIndex
                : currentFrameIndex + 1;
        }

        if (_pixelPlaybackDirection >= 0)
        {
            if (currentFrameIndex >= playbackEndFrameIndex)
            {
                _pixelPlaybackDirection = -1;
                return Math.Max(playbackEndFrameIndex - 1, playbackStartFrameIndex);
            }

            return currentFrameIndex + 1;
        }

        if (currentFrameIndex <= playbackStartFrameIndex)
        {
            _pixelPlaybackDirection = 1;
            return Math.Min(playbackStartFrameIndex + 1, playbackEndFrameIndex);
        }

        return currentFrameIndex - 1;
    }

    private void UpdatePixelStudioPlayback()
    {
        if (!_pixelStudio.IsPlaying || _pixelStudio.Frames.Count <= 1)
        {
            _lastPlaybackTick = 0;
            _pixelPlaybackDirection = 1;
            return;
        }

        int playbackStartFrameIndex = 0;
        int playbackEndFrameIndex = _pixelStudio.Frames.Count - 1;
        if (TryGetLoopRange(out int loopStartFrameIndex, out int loopEndFrameIndex))
        {
            playbackStartFrameIndex = loopStartFrameIndex;
            playbackEndFrameIndex = loopEndFrameIndex;
        }

        if (_pixelStudio.PreviewFrameIndex < playbackStartFrameIndex || _pixelStudio.PreviewFrameIndex > playbackEndFrameIndex)
        {
            _pixelStudio.PreviewFrameIndex = playbackStartFrameIndex;
            _pixelPlaybackDirection = 1;
        }

        long currentTicks = Stopwatch.GetTimestamp();
        if (_lastPlaybackTick == 0)
        {
            _lastPlaybackTick = currentTicks;
            return;
        }

        double elapsedMilliseconds = (currentTicks - _lastPlaybackTick) * 1000.0 / Stopwatch.Frequency;
        int advancedFrames = 0;
        while (elapsedMilliseconds >= GetFrameDurationMilliseconds(_pixelStudio.PreviewFrameIndex) && advancedFrames <= _pixelStudio.Frames.Count * 3)
        {
            elapsedMilliseconds -= GetFrameDurationMilliseconds(_pixelStudio.PreviewFrameIndex);
            _pixelStudio.PreviewFrameIndex = GetNextPlaybackFrameIndex(_pixelStudio.PreviewFrameIndex, playbackStartFrameIndex, playbackEndFrameIndex);
            advancedFrames++;
        }

        if (advancedFrames == 0)
        {
            return;
        }

        _lastPlaybackTick = currentTicks - (long)(elapsedMilliseconds * Stopwatch.Frequency / 1000.0);
        RefreshPixelStudioView();
    }

    private bool HandlePixelStudioMouse(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, MouseButton button)
    {
        if (layout.CanvasResizeDialogRect is not null)
        {
            if (button == MouseButton.Left && TryHandlePixelAction(layout.CanvasResizeDialogButtons, mouseX, mouseY))
            {
                return true;
            }

            return true;
        }

        if (_selectionTransformModeActive
            && (_selectionActive || _selectionCommitted)
            && HandleSelectionTransformModeMouse(layout, mouseX, mouseY, button))
        {
            return true;
        }

        if (button == MouseButton.Right)
        {
            if (layout.ContextMenuRect is not null && layout.ContextMenuRect.Value.Contains(mouseX, mouseY))
            {
                return true;
            }

            IndexedRect? paletteContextSwatch = layout.PaletteSwatches.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (paletteContextSwatch is not null)
            {
                TrySelectActivePaletteIndex(paletteContextSwatch.Index);
                OpenPaletteSwatchContextMenu(paletteContextSwatch.Index, mouseX, mouseY);
                return true;
            }

            IndexedRect? savedPaletteRow = layout.SavedPaletteRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (savedPaletteRow is not null)
            {
                SelectPaletteLibraryRow(savedPaletteRow.Index);
                OpenPaletteContextMenu(savedPaletteRow.Index, mouseX, mouseY);
                return true;
            }

            IndexedRect? layerRow = layout.LayerRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            PixelStudioLayerGroupHeaderRect? layerGroupRow = layout.LayerGroupRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (layerGroupRow is not null)
            {
                StopPixelPlayback();
                OpenLayerGroupContextMenu(layerGroupRow.GroupId, mouseX, mouseY);
                return true;
            }

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

            ActionRect<PixelStudioToolKind>? toolContextButton = layout.ToolButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (toolContextButton is not null && SupportsToolContextMenu(toolContextButton.Action))
            {
                OpenToolContextMenu(toolContextButton.Action, mouseX, mouseY);
                return true;
            }

            ActionRect<PixelStudioAction>? timelineContextButton = layout.TimelineButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
            if (timelineContextButton is not null
                && timelineContextButton.Action is PixelStudioAction.ToggleOnionSkin
                    or PixelStudioAction.ToggleOnionPrevious
                    or PixelStudioAction.ToggleOnionNext)
            {
                OpenOnionContextMenu(mouseX, mouseY);
                return true;
            }

            if (layout.CanvasClipRect.Contains(mouseX, mouseY))
            {
                if (_selectionActive)
                {
                    OpenSelectionContextMenu(mouseX, mouseY);
                    return true;
                }

                if (HasSelectionClipboard())
                {
                    OpenClipboardContextMenu(mouseX, mouseY);
                    return true;
                }
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

        if (_frameDurationFieldActive
            && (layout.FrameDurationFieldRect is null || !layout.FrameDurationFieldRect.Value.Contains(mouseX, mouseY)))
        {
            CommitFrameDurationEdit();
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

        if (layout.PaletteColorFieldRect is not null && layout.PaletteColorFieldRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            BeginPaletteColorAdjustment();
            _paletteColorAdjustMode = PaletteColorAdjustMode.Field;
            UpdatePaletteColorFromField(layout.PaletteColorFieldRect.Value, mouseX, mouseY);
            _pixelDragMode = PixelStudioDragMode.AdjustPaletteColorPicker;
            return true;
        }

        if (layout.PaletteColorWheelRect is not null && layout.PaletteColorWheelFieldRect is not null && layout.PaletteColorWheelRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            if (TryBeginPaletteColorWheelAdjustment(layout.PaletteColorWheelRect.Value, layout.PaletteColorWheelFieldRect.Value, mouseX, mouseY))
            {
                _pixelDragMode = PixelStudioDragMode.AdjustPaletteColorPicker;
                return true;
            }

            return true;
        }

        if (layout.PaletteAlphaSliderRect is not null && layout.PaletteAlphaSliderRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            BeginPaletteColorAdjustment();
            _paletteColorAdjustMode = PaletteColorAdjustMode.Alpha;
            UpdatePaletteAlphaFromSlider(layout.PaletteAlphaSliderRect.Value, mouseX);
            _pixelDragMode = PixelStudioDragMode.AdjustPaletteColorPicker;
            return true;
        }

        if (layout.OnionOpacitySliderRect is not null && layout.OnionOpacitySliderRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            SetOnionOpacityFromSlider(layout.OnionOpacitySliderRect.Value, mouseX);
            _pixelDragMode = PixelStudioDragMode.AdjustOnionOpacity;
            return true;
        }

        if (layout.LayerOpacitySliderRect is not null && layout.LayerOpacitySliderRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            BeginLayerOpacityAdjustment();
            SetLayerOpacityFromSlider(layout.LayerOpacitySliderRect.Value, mouseX);
            _pixelDragMode = PixelStudioDragMode.AdjustLayerOpacity;
            return true;
        }

        if (layout.NavigatorPanelRect is not null && layout.NavigatorPanelRect.Value.Contains(mouseX, mouseY))
        {
            if (TryGetNavigatorResizeCorner(layout.NavigatorPanelRect.Value, mouseX, mouseY, out NavigatorResizeCorner resizeCorner))
            {
                ClosePixelContextMenu();
                _pixelDragMode = PixelStudioDragMode.ResizeNavigatorPanel;
                _navigatorResizeCorner = resizeCorner;
                _navigatorResizeStartMouseX = mouseX;
                _navigatorResizeStartMouseY = mouseY;
                _navigatorResizeStartOffsetX = float.IsFinite(_pixelNavigatorPanelOffsetX) ? _pixelNavigatorPanelOffsetX : layout.NavigatorPanelRect.Value.X - layout.CanvasClipRect.X;
                _navigatorResizeStartOffsetY = float.IsFinite(_pixelNavigatorPanelOffsetY) ? _pixelNavigatorPanelOffsetY : layout.NavigatorPanelRect.Value.Y - layout.CanvasClipRect.Y;
                _navigatorResizeStartWidth = layout.NavigatorPanelRect.Value.Width;
                _navigatorResizeStartHeight = layout.NavigatorPanelRect.Value.Height;
                return true;
            }

            if (layout.NavigatorPreviewRect is not null && layout.NavigatorPreviewRect.Value.Contains(mouseX, mouseY))
            {
                ClosePixelContextMenu();
                _pixelDragMode = PixelStudioDragMode.NavigatePreview;
                UpdateNavigatorCamera(layout, mouseX, mouseY);
                return true;
            }

            ClosePixelContextMenu();
            _pixelDragMode = PixelStudioDragMode.MoveNavigatorPanel;
            _navigatorDragStartMouseX = mouseX;
            _navigatorDragStartMouseY = mouseY;
            _navigatorDragStartOffsetX = float.IsFinite(_pixelNavigatorPanelOffsetX) ? _pixelNavigatorPanelOffsetX : layout.NavigatorPanelRect.Value.X - layout.CanvasClipRect.X;
            _navigatorDragStartOffsetY = float.IsFinite(_pixelNavigatorPanelOffsetY) ? _pixelNavigatorPanelOffsetY : layout.NavigatorPanelRect.Value.Y - layout.CanvasClipRect.Y;
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

        if (layout.FrameDurationFieldRect is not null
            && layout.FrameDurationFieldRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            ActivateFrameDurationField();
            return true;
        }

        if (layout.SelectionTransformAngleFieldRect is not null
            && layout.SelectionTransformAngleFieldRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            ActivateSelectionTransformAngleField();
            return true;
        }

        if (layout.SelectionTransformScaleXFieldRect is not null
            && layout.SelectionTransformScaleXFieldRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            ActivateSelectionTransformScaleField(horizontal: true);
            return true;
        }

        if (layout.SelectionTransformScaleYFieldRect is not null
            && layout.SelectionTransformScaleYFieldRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            ActivateSelectionTransformScaleField(horizontal: false);
            return true;
        }

        ActionRect<PixelStudioResizeAnchor>? pivotPresetButton = layout.SelectionTransformPivotPresetButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (pivotPresetButton is not null)
        {
            ClosePixelContextMenu();
            SetSelectionTransformPivotPreset(pivotPresetButton.Action);
            return true;
        }

        PixelStudioSelectionHandleRect? selectionHandle = layout.SelectionHandleRects.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (selectionHandle is not null)
        {
            ClosePixelContextMenu();
            BeginSelectionTransform(selectionHandle.Kind);
            return true;
        }

        if (TryGetMirrorAxisHandle(layout, mouseX, mouseY, out PixelStudioMirrorAxisKind mirrorAxisKind))
        {
            ClosePixelContextMenu();
            _mirrorAxisDragKind = mirrorAxisKind;
            _pixelDragMode = PixelStudioDragMode.MoveMirrorAxis;
            UpdateMirrorAxisFromMouse(layout, mouseX, mouseY);
            return true;
        }

        if (_selectionTransformModeActive && layout.CanvasClipRect.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            if (_selectionTransformPreviewVisible)
            {
                CommitSelectionTransform();
                RefreshPixelStudioView("Transform applied.", rebuildLayout: true);
            }
            else
            {
                RefreshPixelStudioView("Transform mode active. Drag handles, edit values, press Enter to apply, or Esc to cancel.", rebuildLayout: true);
            }

            return true;
        }

        if (TryHandlePixelAction(layout.DocumentButtons, mouseX, mouseY)
            || TryHandlePixelAction(layout.CanvasButtons, mouseX, mouseY)
            || TryHandlePixelAction(layout.SelectionButtons, mouseX, mouseY)
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
            if (layout.PaletteRenameFieldRect is not null && layout.PaletteRenameFieldRect.Value.Contains(mouseX, mouseY))
            {
                if (_paletteRenameActive)
                {
                    SelectAllText(EditableTextTarget.PaletteRename);
                    RefreshPixelStudioView("Editing palette name.");
                }

                return true;
            }

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
            ClosePixelContextMenu();
            _paletteSwatchDragSourceIndex = Math.Clamp(paletteSwatch.Index, 0, Math.Max(_pixelStudio.Palette.Count - 1, 0));
            _paletteSwatchDragTargetIndex = _paletteSwatchDragSourceIndex;
            _paletteSwatchDragStartMouseX = mouseX;
            _paletteSwatchDragStartMouseY = mouseY;
            _paletteSwatchDragMoved = false;
            _pixelDragMode = PixelStudioDragMode.ReorderPaletteSwatch;
            return true;
        }

        IndexedRect? recentColorSwatch = layout.RecentColorSwatches.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (recentColorSwatch is not null)
        {
            ApplyRecentPaletteColor(recentColorSwatch.Index);
            ClosePixelContextMenu();
            return true;
        }

        if (layout.SecondaryColorRect.Contains(mouseX, mouseY))
        {
            ExecutePixelStudioAction(PixelStudioAction.SwapSecondaryColor);
            ClosePixelContextMenu();
            return true;
        }

        if (layout.LayerRenameFieldRect is not null && layout.LayerRenameFieldRect.Value.Contains(mouseX, mouseY))
        {
            if (_layerRenameActive)
            {
                SelectAllText(EditableTextTarget.LayerRename);
                RefreshPixelStudioView("Editing layer name.");
            }

            return true;
        }

        IndexedRect? visibilityButton = layout.LayerVisibilityButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (visibilityButton is not null)
        {
            ClosePixelContextMenu();
            TogglePixelLayerVisibility(visibilityButton.Index);
            return true;
        }

        PixelStudioLayerGroupHeaderRect? layerGroupHeader = layout.LayerGroupRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (layerGroupHeader is not null)
        {
            ToggleLayerGroupCollapsed(layerGroupHeader.GroupId);
            return true;
        }

        IndexedRect? layerRowHit = layout.LayerRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (layerRowHit is not null)
        {
            StopPixelPlayback();
            _pixelStudio.ActiveLayerIndex = Math.Clamp(layerRowHit.Index, 0, CurrentPixelFrame.Layers.Count - 1);
            ClosePixelContextMenu();
            _layerDragSourceIndex = _pixelStudio.ActiveLayerIndex;
            _layerDragInsertIndex = _pixelStudio.ActiveLayerIndex;
            _layerDragStartMouseX = mouseX;
            _layerDragStartMouseY = mouseY;
            _layerDragMoved = false;
            _layerDragJoinGroupId = null;
            _layerDragJoinAnchorLayerIndex = -1;
            _pixelDragMode = PixelStudioDragMode.ReorderLayer;
            RefreshPixelStudioView($"{EditorBranding.PixelToolName} layer selected: {CurrentPixelFrame.Layers[_pixelStudio.ActiveLayerIndex].Name}.");
            return true;
        }

        if (layout.FrameRenameFieldRect is not null && layout.FrameRenameFieldRect.Value.Contains(mouseX, mouseY))
        {
            if (_frameRenameActive)
            {
                SelectAllText(EditableTextTarget.FrameRename);
                RefreshPixelStudioView("Editing frame name.");
            }

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
            _frameDragSourceIndex = _pixelStudio.ActiveFrameIndex;
            _frameDragInsertIndex = _pixelStudio.ActiveFrameIndex;
            _frameDragStartMouseX = mouseX;
            _frameDragStartMouseY = mouseY;
            _frameDragMoved = false;
            _pixelDragMode = PixelStudioDragMode.ReorderFrame;
            RefreshPixelStudioView($"{EditorBranding.PixelToolName} frame selected: {CurrentPixelFrame.Name}.");
            return true;
        }

        if (_pixelTimelineVisible && layout.PlaybackPreviewRect.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            return true;
        }

        if (_pixelStudio.ActiveTool == PixelStudioToolKind.Hand && layout.CanvasClipRect.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            if (!_selectionTransformModeActive
                && TryGetCanvasCellCoordinates(layout, mouseX, mouseY, out int handCellX, out int handCellY, clampToCanvas: true)
                && _selectionCommitted
                && IsWithinCurrentSelection(handCellX, handCellY))
            {
                if (!CanTransformCurrentLayer("moving the selection"))
                {
                    return true;
                }

                BeginSelectionMove(handCellX, handCellY);
                return true;
            }

            BeginCanvasPan(mouseX, mouseY);
            return true;
        }

        if (TryGetCanvasCellIndex(layout, mouseX, mouseY, out int cellIndex))
        {
            return HandlePixelCanvasPress(cellIndex);
        }

        return false;
    }

    private bool HandleSelectionTransformKeyLockout(bool controlPressed, Key key)
    {
        if (!_selectionTransformModeActive || IsSelectionTransformInputActive())
        {
            return false;
        }

        if ((controlPressed && key is Key.Z or Key.Y or Key.C or Key.X or Key.V or Key.D)
            || key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Delete or Key.Space)
        {
            RefreshPixelStudioView("Transform mode active. Apply, cancel, or exit transform before using other editing commands.", rebuildLayout: true);
            return true;
        }

        return false;
    }

    private bool HandleSelectionTransformModeMouse(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, MouseButton button)
    {
        if (button == MouseButton.Middle)
        {
            return layout.CanvasPanelRect.Contains(mouseX, mouseY)
                || layout.CanvasClipRect.Contains(mouseX, mouseY);
        }

        if (button == MouseButton.Right)
        {
            ClosePixelContextMenu();
            if (_selectionTransformPreviewVisible)
            {
                CommitSelectionTransform();
                RefreshPixelStudioView("Transform applied. Transform mode is still active.", rebuildLayout: true);
            }
            else
            {
                RefreshPixelStudioView("Transform mode active. Drag handles, edit values, press Enter to apply, click Transform again to exit, or Esc to cancel.", rebuildLayout: true);
            }

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

        ActionRect<PixelStudioAction>? transformToggleButton = layout.SelectionButtons.FirstOrDefault(
            entry => entry.Action == PixelStudioAction.ToggleSelectionTransformMode && entry.Rect.Contains(mouseX, mouseY));
        if (transformToggleButton is not null)
        {
            ClosePixelContextMenu();
            if (_selectionTransformPreviewVisible)
            {
                CommitSelectionTransform();
            }

            ExecutePixelStudioAction(transformToggleButton.Action);
            return true;
        }

        if (layout.SelectionTransformAngleFieldRect is not null
            && layout.SelectionTransformAngleFieldRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            ActivateSelectionTransformAngleField();
            return true;
        }

        if (layout.SelectionTransformScaleXFieldRect is not null
            && layout.SelectionTransformScaleXFieldRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            ActivateSelectionTransformScaleField(horizontal: true);
            return true;
        }

        if (layout.SelectionTransformScaleYFieldRect is not null
            && layout.SelectionTransformScaleYFieldRect.Value.Contains(mouseX, mouseY))
        {
            ClosePixelContextMenu();
            ActivateSelectionTransformScaleField(horizontal: false);
            return true;
        }

        ActionRect<PixelStudioResizeAnchor>? pivotPresetButton = layout.SelectionTransformPivotPresetButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (pivotPresetButton is not null)
        {
            ClosePixelContextMenu();
            SetSelectionTransformPivotPreset(pivotPresetButton.Action);
            return true;
        }

        PixelStudioSelectionHandleRect? selectionHandle = layout.SelectionHandleRects.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (selectionHandle is not null)
        {
            ClosePixelContextMenu();
            BeginSelectionTransform(selectionHandle.Kind);
            return true;
        }

        ClosePixelContextMenu();
        if (_selectionTransformPreviewVisible)
        {
            CommitSelectionTransform();
            RefreshPixelStudioView("Transform applied. Transform mode is still active.", rebuildLayout: true);
        }
        else
        {
            RefreshPixelStudioView("Transform mode active. Drag handles, edit values, press Enter to apply, click Transform again to exit, or Esc to cancel.", rebuildLayout: true);
        }

        return true;
    }

    private void HandlePixelStudioMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            bool wasCanvasPan = _pixelDragMode == PixelStudioDragMode.PanCanvas;
            bool wasPaletteSwatchReorder = _pixelDragMode == PixelStudioDragMode.ReorderPaletteSwatch;
            bool wasLayerReorder = _pixelDragMode == PixelStudioDragMode.ReorderLayer;
            bool wasFrameReorder = _pixelDragMode == PixelStudioDragMode.ReorderFrame;
            bool wasBrushSizeDrag = _pixelDragMode == PixelStudioDragMode.AdjustBrushSize;
            bool wasOnionOpacityDrag = _pixelDragMode == PixelStudioDragMode.AdjustOnionOpacity;
            bool wasLayerOpacityDrag = _pixelDragMode == PixelStudioDragMode.AdjustLayerOpacity;
            bool wasPaletteColorDrag = _pixelDragMode == PixelStudioDragMode.AdjustPaletteColorPicker;
            bool wasToolSettingsDrag = _pixelDragMode == PixelStudioDragMode.MoveToolSettingsDock;
            bool wasNavigatorDrag = _pixelDragMode == PixelStudioDragMode.MoveNavigatorPanel;
            bool wasNavigatorPreviewDrag = _pixelDragMode == PixelStudioDragMode.NavigatePreview;
            bool wasNavigatorResizeDrag = _pixelDragMode == PixelStudioDragMode.ResizeNavigatorPanel;
            bool wasSelectionTransform = _pixelDragMode == PixelStudioDragMode.TransformSelection;
            bool wasSelectionMove = _pixelDragMode == PixelStudioDragMode.MoveSelection;
            bool wasMirrorAxisDrag = _pixelDragMode == PixelStudioDragMode.MoveMirrorAxis;
            _pixelDragMode = PixelStudioDragMode.None;
            if (wasSelectionTransform)
            {
                CommitSelectionTransform();
                return;
            }

            if (_selectionMoveActive || _pixelStudio.ActiveTool == PixelStudioToolKind.Select)
            {
                CommitSelection();
            }
            EndPixelStroke();
            if (wasBrushSizeDrag)
            {
                MarkPixelStudioRecoveryDirty();
                RefreshPixelStudioView($"Brush size set to {_pixelStudio.BrushSize}px.");
                return;
            }

            if (wasPaletteColorDrag)
            {
                CommitPaletteColorAdjustment();
                return;
            }

            if (wasOnionOpacityDrag)
            {
                MarkPixelStudioRecoveryDirty();
                RefreshPixelStudioView($"Onion opacity set to {MathF.Round(_pixelStudio.OnionOpacity * 100f)}%.");
                return;
            }

            if (wasLayerOpacityDrag)
            {
                CommitLayerOpacityAdjustment();
                return;
            }

            if (wasCanvasPan)
            {
                LogCurrentCanvasCamera("PanComplete");
                RefreshPixelStudioCameraStatus("Canvas pan set.");
                return;
            }

            if (wasToolSettingsDrag)
            {
                RefreshPixelStudioInteraction(rebuildLayout: true);
            }

            if (wasNavigatorDrag)
            {
                RefreshPixelStudioInteraction(rebuildLayout: true);
                return;
            }

            if (wasNavigatorPreviewDrag)
            {
                RefreshPixelStudioCameraStatus("Navigator moved canvas.");
                return;
            }

            if (wasNavigatorResizeDrag)
            {
                _navigatorResizeCorner = NavigatorResizeCorner.None;
                RefreshPixelStudioInteraction(rebuildLayout: true);
                return;
            }

            if (wasMirrorAxisDrag)
            {
                RefreshPixelStudioView("Mirror axis moved.", rebuildLayout: false);
                return;
            }

            if (wasLayerReorder)
            {
                CompleteLayerReorderDrag();
                return;
            }

            if (wasPaletteSwatchReorder)
            {
                CompletePaletteSwatchReorderDrag();
                return;
            }

            if (wasFrameReorder)
            {
                CompleteFrameReorderDrag();
                return;
            }

            if (wasSelectionMove)
            {
                return;
            }

            return;
        }

        if (button == MouseButton.Middle && _pixelDragMode == PixelStudioDragMode.PanCanvas)
        {
            _pixelDragMode = PixelStudioDragMode.None;
            LogCurrentCanvasCamera("PanComplete");
            RefreshPixelStudioCameraStatus("Canvas pan set.");
        }
    }

    private void HandlePixelStudioMouseMove(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        _pixelMouseX = mouseX;
        _pixelMouseY = mouseY;
        UpdateHoveredPixelTool(layout, mouseX, mouseY);

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

        if (_pixelDragMode == PixelStudioDragMode.AdjustOnionOpacity && layout.OnionOpacitySliderRect is not null)
        {
            SetOnionOpacityFromSlider(layout.OnionOpacitySliderRect.Value, mouseX);
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.AdjustLayerOpacity && layout.LayerOpacitySliderRect is not null)
        {
            SetLayerOpacityFromSlider(layout.LayerOpacitySliderRect.Value, mouseX);
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.AdjustPaletteColorPicker)
        {
            UpdatePaletteColorPicker(layout, mouseX, mouseY);
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.MoveNavigatorPanel)
        {
            MoveNavigatorPanel(layout, mouseX, mouseY);
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.ResizeNavigatorPanel)
        {
            ResizeNavigatorPanel(layout, mouseX, mouseY);
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.NavigatePreview)
        {
            UpdateNavigatorCamera(layout, mouseX, mouseY);
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.MoveToolSettingsDock)
        {
            MoveToolSettingsPanel(layout, mouseX, mouseY);
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.ReorderPaletteSwatch)
        {
            UpdatePaletteSwatchReorderDrag(layout, mouseX, mouseY);
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.ReorderLayer)
        {
            UpdateLayerReorderDrag(layout, mouseX, mouseY);
            return;
        }

        if (_pixelDragMode == PixelStudioDragMode.ReorderFrame)
        {
            UpdateFrameReorderDrag(layout, mouseX, mouseY);
            return;
        }

        if (_pixelStudio.ActiveTool == PixelStudioToolKind.Select)
        {
            if (_pixelDragMode == PixelStudioDragMode.TransformSelection)
            {
                UpdateSelectionTransformPreview(layout, mouseX, mouseY);
                return;
            }

            if (TryGetCanvasCellIndex(layout, mouseX, mouseY, out int selectionCellIndex))
            {
                UpdateSelection(selectionCellIndex);
            }

            return;
        }

        if (_pixelStudio.ActiveTool == PixelStudioToolKind.Hand)
        {
            return;
        }

        if (!_isPixelStrokeActive || !TryGetCanvasCellIndex(layout, mouseX, mouseY, out int cellIndex))
        {
            return;
        }

        ApplyStrokePath(cellIndex);
    }

    private void UpdatePixelStudioModifierState(IKeyboard keyboard, Key key, bool isPressed)
    {
        if (key is Key.ShiftLeft or Key.ShiftRight)
        {
            _shiftPressed = isPressed || keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight);
        }
        else if (key is Key.AltLeft or Key.AltRight)
        {
            _altPressed = isPressed || keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight);
        }
        else if (CurrentPage == EditorPageKind.PixelStudio)
        {
            _shiftPressed = keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight);
            _altPressed = keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight);
        }

        if (_isPixelStrokeActive && SupportsConstrainedShapePreview(_pixelStudio.ActiveTool))
        {
            if (_layoutSnapshot?.PixelStudio is { } pixelLayout
                && TryGetCanvasCellCoordinates(pixelLayout, _pixelMouseX, _pixelMouseY, out int previewX, out int previewY, clampToCanvas: true))
            {
                PreviewShapeStroke((previewY * _pixelStudio.CanvasWidth) + previewX);
            }
            else
            {
                RefreshPixelStudioInteraction();
            }
        }

        if (_pixelDragMode == PixelStudioDragMode.TransformSelection
            && _selectionTransformHandleKind == PixelStudioSelectionHandleKind.Rotate
            && _layoutSnapshot?.PixelStudio is { } transformLayout)
        {
            UpdateSelectionTransformPreview(transformLayout, _pixelMouseX, _pixelMouseY);
        }
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
            case PixelStudioDragMode.MoveSelection:
                UpdateSelectionMove(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.TransformSelection:
                UpdateSelectionTransformPreview(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.MoveMirrorAxis:
                UpdateMirrorAxisFromMouse(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.MoveNavigatorPanel:
                MoveNavigatorPanel(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.ResizeNavigatorPanel:
                ResizeNavigatorPanel(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.NavigatePreview:
                UpdateNavigatorCamera(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.MoveToolSettingsDock:
                MoveToolSettingsPanel(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.ReorderPaletteSwatch:
                UpdatePaletteSwatchReorderDrag(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.ReorderLayer:
                UpdateLayerReorderDrag(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.ReorderFrame:
                UpdateFrameReorderDrag(layout, mouseX, mouseY);
                return true;
            case PixelStudioDragMode.ResizeToolsPanel:
                _pixelToolsCollapsed = false;
                _pixelToolsPanelWidth = Math.Clamp(mouseX - layout.ToolbarRect.X, 38, 120);
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
            int targetZoom = direction > 0
                ? PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom - 2)
                : PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom + 2);

            ApplyWheelCanvasZoom(layout, mouseX, mouseY, targetZoom);

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
        if (!TryGetCanvasCellCoordinates(layout, mouseX, mouseY, out int x, out int y))
        {
            return false;
        }

        cellIndex = (y * _pixelStudio.CanvasWidth) + x;
        return true;
    }

    private bool TryGetCanvasCellCoordinates(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, out int x, out int y, bool clampToCanvas = false)
    {
        x = 0;
        y = 0;
        if (_pixelStudio.CanvasWidth <= 0 || _pixelStudio.CanvasHeight <= 0)
        {
            return false;
        }

        if (!clampToCanvas && !layout.CanvasClipRect.Contains(mouseX, mouseY))
        {
            return false;
        }

        int cellSize = Math.Max(layout.CanvasCellSize, 1);
        x = (int)MathF.Floor((mouseX - layout.CanvasViewportRect.X) / cellSize);
        y = (int)MathF.Floor((mouseY - layout.CanvasViewportRect.Y) / cellSize);

        if (clampToCanvas)
        {
            x = Math.Clamp(x, 0, _pixelStudio.CanvasWidth - 1);
            y = Math.Clamp(y, 0, _pixelStudio.CanvasHeight - 1);
            return true;
        }

        return x >= 0 && y >= 0 && x < _pixelStudio.CanvasWidth && y < _pixelStudio.CanvasHeight;
    }

    private bool TryGetCanvasPointerPosition(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, out float canvasX, out float canvasY)
    {
        canvasX = 0f;
        canvasY = 0f;
        if (_pixelStudio.CanvasWidth <= 0 || _pixelStudio.CanvasHeight <= 0)
        {
            return false;
        }

        float viewportRight = layout.CanvasViewportRect.X + layout.CanvasViewportRect.Width;
        float viewportBottom = layout.CanvasViewportRect.Y + layout.CanvasViewportRect.Height;
        if (mouseX < layout.CanvasViewportRect.X
            || mouseY < layout.CanvasViewportRect.Y
            || mouseX > viewportRight
            || mouseY > viewportBottom)
        {
            return false;
        }

        int cellSize = Math.Max(layout.CanvasCellSize, 1);
        canvasX = (mouseX - layout.CanvasViewportRect.X) / cellSize;
        canvasY = (mouseY - layout.CanvasViewportRect.Y) / cellSize;
        if (!float.IsFinite(canvasX) || !float.IsFinite(canvasY))
        {
            canvasX = 0f;
            canvasY = 0f;
            return false;
        }

        float maxCanvasX = Math.Max(_pixelStudio.CanvasWidth - 0.001f, 0f);
        float maxCanvasY = Math.Max(_pixelStudio.CanvasHeight - 0.001f, 0f);
        canvasX = Math.Clamp(canvasX, 0f, maxCanvasX);
        canvasY = Math.Clamp(canvasY, 0f, maxCanvasY);
        return true;
    }

    private void ApplyWheelCanvasZoom(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, int targetZoom)
    {
        int previousZoom = _pixelStudio.DesiredZoom;
        int resolvedZoom = PixelStudioCameraMath.ClampZoom(targetZoom);
        if (resolvedZoom == previousZoom)
        {
            return;
        }

        bool hasPointerAnchor = TryGetCanvasPointerPosition(layout, mouseX, mouseY, out float canvasX, out float canvasY);
        _pixelStudio.DesiredZoom = resolvedZoom;

        if (hasPointerAnchor)
        {
            float baseViewportX = layout.CanvasClipRect.X + ((layout.CanvasClipRect.Width - (_pixelStudio.CanvasWidth * resolvedZoom)) * 0.5f);
            float baseViewportY = layout.CanvasClipRect.Y + ((layout.CanvasClipRect.Height - (_pixelStudio.CanvasHeight * resolvedZoom)) * 0.5f);
            _pixelStudio.CanvasPanX = mouseX - (canvasX * resolvedZoom) - baseViewportX;
            _pixelStudio.CanvasPanY = mouseY - (canvasY * resolvedZoom) - baseViewportY;
        }

        ClosePixelContextMenu();
        RefreshPixelStudioCameraLayout($"{EditorBranding.PixelToolName} zoom target set to {_pixelStudio.DesiredZoom}x.");
    }

    private bool HandlePixelCanvasPress(int cellIndex)
    {
        StopPixelPlayback();

        switch (_pixelStudio.ActiveTool)
        {
            case PixelStudioToolKind.Select:
                BeginSelection(cellIndex);
                return true;
            case PixelStudioToolKind.Hand:
                return true;
            case PixelStudioToolKind.Picker:
                ThemeColor? pickedColor = GetTopVisiblePixelColor(cellIndex);
                if (pickedColor is not null)
                {
                    ActivatePaletteColor(pickedColor.Value);
                    RefreshPixelStudioView($"Picked color {BuildPixelStudioViewState().ActiveColorHex}.", rebuildLayout: true);
                }

                return true;
        }

        if (!EnsureCurrentLayerVisibleForEditing("editing"))
        {
            return true;
        }

        if (!EnsureLayerUnlockedForEditing(
                _pixelStudio.ActiveLayerIndex,
                "Active Layer Is Locked",
                $"The active layer \"{CurrentPixelLayer.Name}\" is locked. Unlock it before editing?"))
        {
            return true;
        }

        switch (_pixelStudio.ActiveTool)
        {
            case PixelStudioToolKind.Pencil:
            case PixelStudioToolKind.Eraser:
                BeginPixelStroke();
                ApplyStrokePath(cellIndex);
                return true;
            case PixelStudioToolKind.Line:
                BeginPixelStroke();
                _strokeAnchorCellIndex = cellIndex;
                PreviewLineStroke(cellIndex);
                return true;
            case PixelStudioToolKind.Rectangle:
            case PixelStudioToolKind.Ellipse:
            case PixelStudioToolKind.Shape:
                BeginPixelStroke();
                _strokeAnchorCellIndex = cellIndex;
                PreviewShapeStroke(cellIndex);
                return true;
            case PixelStudioToolKind.Fill:
                if (SelectionUsesMask())
                {
                    int selectionX = cellIndex % _pixelStudio.CanvasWidth;
                    int selectionY = cellIndex / _pixelStudio.CanvasWidth;
                    if (IsWithinCurrentSelection(selectionX, selectionY))
                    {
                        ApplyPixelStudioChange(
                            $"Filled selection with {BuildPixelStudioViewState().ActiveColorHex}.",
                            () => FillSelectionPixels(GetCurrentToolPixelValue()),
                            rebuildLayout: false);
                        return true;
                    }
                }

                ApplyPixelStudioChange(
                    $"Filled region with {BuildPixelStudioViewState().ActiveColorHex}.",
                    () => FloodFillCell(cellIndex, GetCurrentToolPixelValue()),
                    rebuildLayout: false);
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

        _strokeSnapshot = CapturePixelStudioState();
        _isPixelStrokeActive = true;
        _strokeChanged = false;
        _lastStrokeCellIndex = -1;
        _strokeAnchorCellIndex = -1;
        _pixelDirtyIndices.Clear();
        _linePreviewIndices.Clear();
        _pixelPerfectStrokeCells.Clear();
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

        if ((_pixelStudio.ActiveTool == PixelStudioToolKind.Line
                || _pixelStudio.ActiveTool == PixelStudioToolKind.Rectangle
                || _pixelStudio.ActiveTool == PixelStudioToolKind.Ellipse
                || _pixelStudio.ActiveTool == PixelStudioToolKind.Shape)
            && _strokeAnchorCellIndex >= 0
            && _strokeSnapshot is not null)
        {
            if (_pixelStudio.ActiveTool == PixelStudioToolKind.Line)
            {
                PreviewLineStroke(targetCellIndex);
            }
            else
            {
                PreviewShapeStroke(targetCellIndex);
            }
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
            RefreshPixelStudioPixels(_pixelDirtyIndices);
            _pixelDirtyIndices.Clear();
        }
    }

    private void ApplyBrushStamp(int centerCellIndex)
    {
        if (_brushMode == PixelStudioBrushMode.PixelPerfect
            && _pixelStudio.BrushSize == 1
            && _pixelStudio.ActiveTool is PixelStudioToolKind.Pencil or PixelStudioToolKind.Eraser)
        {
            ApplyPixelPerfectBrushStamp(centerCellIndex);
            return;
        }

        int radius = Math.Max(_pixelStudio.BrushSize - 1, 0) / 2;
        int centerX = centerCellIndex % _pixelStudio.CanvasWidth;
        int centerY = centerCellIndex / _pixelStudio.CanvasWidth;
        int nextValue = GetCurrentToolPixelValue();
        HashSet<int> targetIndices = [];

        for (int offsetY = -radius; offsetY <= radius; offsetY++)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                if (_brushMode == PixelStudioBrushMode.Round
                    && (_pixelStudio.BrushSize > 1)
                    && ((offsetX * offsetX) + (offsetY * offsetY) > ((radius + 0.5f) * (radius + 0.5f))))
                {
                    continue;
                }

                int x = centerX + offsetX;
                int y = centerY + offsetY;
                AddMirroredBrushTargets(targetIndices, x, y);
            }
        }

        foreach (int cellIndex in targetIndices)
        {
            if (CurrentPixelLayer.Pixels[cellIndex] == nextValue)
            {
                continue;
            }

            if (TryWritePixelToCurrentLayer(cellIndex, nextValue))
            {
                _strokeChanged = true;
                _pixelDirtyIndices.Add(cellIndex);
            }
        }

        _lastStrokeCellIndex = centerCellIndex;
    }

    private void ApplyPixelPerfectBrushStamp(int centerCellIndex)
    {
        if (centerCellIndex < 0 || centerCellIndex >= CurrentPixelLayer.Pixels.Length)
        {
            return;
        }

        if (_pixelPerfectStrokeCells.Count >= 2)
        {
            int cellA = _pixelPerfectStrokeCells[^2];
            int cellB = _pixelPerfectStrokeCells[^1];
            if (FormsPixelPerfectCorner(cellA, cellB, centerCellIndex))
            {
                _pixelPerfectStrokeCells.RemoveAt(_pixelPerfectStrokeCells.Count - 1);
                RestoreBrushTargetsFromStrokeSnapshot(cellB);
            }
        }

        if (_pixelPerfectStrokeCells.Count == 0 || _pixelPerfectStrokeCells[^1] != centerCellIndex)
        {
            _pixelPerfectStrokeCells.Add(centerCellIndex);
        }

        ApplySingleBrushCell(centerCellIndex);
        _lastStrokeCellIndex = centerCellIndex;
    }

    private void ApplySingleBrushCell(int centerCellIndex)
    {
        int centerX = centerCellIndex % _pixelStudio.CanvasWidth;
        int centerY = centerCellIndex / _pixelStudio.CanvasWidth;
        int nextValue = GetCurrentToolPixelValue();
        HashSet<int> targetIndices = [];
        AddMirroredBrushTargets(targetIndices, centerX, centerY);
        foreach (int cellIndex in targetIndices)
        {
            if (CurrentPixelLayer.Pixels[cellIndex] == nextValue)
            {
                continue;
            }

            if (TryWritePixelToCurrentLayer(cellIndex, nextValue))
            {
                _strokeChanged = true;
                _pixelDirtyIndices.Add(cellIndex);
            }
        }
    }

    private void ApplyShapeCell(int centerCellIndex)
    {
        int centerX = centerCellIndex % _pixelStudio.CanvasWidth;
        int centerY = centerCellIndex / _pixelStudio.CanvasWidth;
        int nextValue = GetCurrentToolPixelValue();
        HashSet<int> targetIndices = [];

        AddMirroredBrushTargets(targetIndices, centerX, centerY);

        foreach (int cellIndex in targetIndices)
        {
            if (CurrentPixelLayer.Pixels[cellIndex] == nextValue)
            {
                continue;
            }

            if (TryWritePixelToCurrentLayer(cellIndex, nextValue))
            {
                _strokeChanged = true;
                _pixelDirtyIndices.Add(cellIndex);
            }
        }

        _lastStrokeCellIndex = centerCellIndex;
    }

    private void AddMirroredBrushTargets(HashSet<int> targetIndices, int x, int y)
    {
        AddBrushTarget(targetIndices, x, y);

        bool mirrorHorizontal = _mirrorMode is PixelStudioMirrorMode.Horizontal or PixelStudioMirrorMode.Both;
        bool mirrorVertical = _mirrorMode is PixelStudioMirrorMode.Vertical or PixelStudioMirrorMode.Both;
        float mirrorAxisX = GetMirrorAxisX();
        float mirrorAxisY = GetMirrorAxisY();
        if (mirrorHorizontal)
        {
            AddBrushTarget(targetIndices, PixelStudioMirrorAxisMath.MirrorCoordinate(x, mirrorAxisX), y);
        }

        if (mirrorVertical)
        {
            AddBrushTarget(targetIndices, x, PixelStudioMirrorAxisMath.MirrorCoordinate(y, mirrorAxisY));
        }

        if (mirrorHorizontal && mirrorVertical)
        {
            AddBrushTarget(
                targetIndices,
                PixelStudioMirrorAxisMath.MirrorCoordinate(x, mirrorAxisX),
                PixelStudioMirrorAxisMath.MirrorCoordinate(y, mirrorAxisY));
        }
    }

    private void AddMirroredCanvasTargets(HashSet<int> targetIndices, int x, int y)
    {
        AddCanvasTarget(targetIndices, x, y);

        bool mirrorHorizontal = _mirrorMode is PixelStudioMirrorMode.Horizontal or PixelStudioMirrorMode.Both;
        bool mirrorVertical = _mirrorMode is PixelStudioMirrorMode.Vertical or PixelStudioMirrorMode.Both;
        float mirrorAxisX = GetMirrorAxisX();
        float mirrorAxisY = GetMirrorAxisY();
        if (mirrorHorizontal)
        {
            AddCanvasTarget(targetIndices, PixelStudioMirrorAxisMath.MirrorCoordinate(x, mirrorAxisX), y);
        }

        if (mirrorVertical)
        {
            AddCanvasTarget(targetIndices, x, PixelStudioMirrorAxisMath.MirrorCoordinate(y, mirrorAxisY));
        }

        if (mirrorHorizontal && mirrorVertical)
        {
            AddCanvasTarget(
                targetIndices,
                PixelStudioMirrorAxisMath.MirrorCoordinate(x, mirrorAxisX),
                PixelStudioMirrorAxisMath.MirrorCoordinate(y, mirrorAxisY));
        }
    }

    private float GetMirrorAxisX()
    {
        _mirrorAxisX = PixelStudioMirrorAxisMath.NormalizeAxis(_mirrorAxisX, _pixelStudio.CanvasWidth);
        return _mirrorAxisX;
    }

    private float GetMirrorAxisY()
    {
        _mirrorAxisY = PixelStudioMirrorAxisMath.NormalizeAxis(_mirrorAxisY, _pixelStudio.CanvasHeight);
        return _mirrorAxisY;
    }

    private void RestoreBrushTargetsFromStrokeSnapshot(int centerCellIndex)
    {
        if (_strokeSnapshot is null)
        {
            return;
        }

        PixelStudioLayerState sourceLayer = _strokeSnapshot.Frames[_pixelStudio.ActiveFrameIndex].Layers[_pixelStudio.ActiveLayerIndex];
        int centerX = centerCellIndex % _pixelStudio.CanvasWidth;
        int centerY = centerCellIndex / _pixelStudio.CanvasWidth;
        HashSet<int> targetIndices = [];
        AddMirroredBrushTargets(targetIndices, centerX, centerY);
        foreach (int cellIndex in targetIndices)
        {
            if (cellIndex < 0 || cellIndex >= CurrentPixelLayer.Pixels.Length)
            {
                continue;
            }

            int sourceValue = sourceLayer.Pixels[cellIndex];
            if (CurrentPixelLayer.Pixels[cellIndex] == sourceValue)
            {
                continue;
            }

            CurrentPixelLayer.Pixels[cellIndex] = sourceValue;
            _strokeChanged = true;
            _pixelDirtyIndices.Add(cellIndex);
        }
    }

    private bool FormsPixelPerfectCorner(int cellA, int cellB, int cellC)
    {
        int ax = cellA % _pixelStudio.CanvasWidth;
        int ay = cellA / _pixelStudio.CanvasWidth;
        int bx = cellB % _pixelStudio.CanvasWidth;
        int by = cellB / _pixelStudio.CanvasWidth;
        int cx = cellC % _pixelStudio.CanvasWidth;
        int cy = cellC / _pixelStudio.CanvasWidth;

        int abDistance = Math.Abs(ax - bx) + Math.Abs(ay - by);
        int bcDistance = Math.Abs(bx - cx) + Math.Abs(by - cy);
        bool acDiagonal = Math.Abs(ax - cx) == 1 && Math.Abs(ay - cy) == 1;
        bool bridgeCell = (bx == ax && by == cy) || (by == ay && bx == cx);

        return abDistance == 1 && bcDistance == 1 && acDiagonal && bridgeCell;
    }

    private void AddCanvasTarget(HashSet<int> targetIndices, int x, int y)
    {
        if (!IsWithinCanvas(x, y))
        {
            return;
        }

        targetIndices.Add((y * _pixelStudio.CanvasWidth) + x);
    }

    private void AddBrushTarget(HashSet<int> targetIndices, int x, int y)
    {
        if (!IsWithinCanvas(x, y) || !IsWithinSelection(x, y))
        {
            return;
        }

        targetIndices.Add((y * _pixelStudio.CanvasWidth) + x);
    }

    private void PreviewLineStroke(int targetCellIndex)
    {
        if (_strokeSnapshot is null || _strokeAnchorCellIndex < 0)
        {
            return;
        }

        PixelStudioLayerState sourceLayer = _strokeSnapshot.Frames[_pixelStudio.ActiveFrameIndex].Layers[_pixelStudio.ActiveLayerIndex];
        foreach (int dirtyIndex in _linePreviewIndices)
        {
            if (dirtyIndex < 0 || dirtyIndex >= CurrentPixelLayer.Pixels.Length)
            {
                continue;
            }

            CurrentPixelLayer.Pixels[dirtyIndex] = sourceLayer.Pixels[dirtyIndex];
            _pixelDirtyIndices.Add(dirtyIndex);
        }

        _linePreviewIndices.Clear();
        _strokeChanged = false;
        foreach (int cellIndex in EnumerateStrokeCells(_strokeAnchorCellIndex, targetCellIndex))
        {
            ApplyBrushStamp(cellIndex);
        }

        _linePreviewIndices.UnionWith(_pixelDirtyIndices);
        if (_strokeChanged)
        {
            RefreshPixelStudioPixels(_pixelDirtyIndices);
            _pixelDirtyIndices.Clear();
        }
    }

    private void PreviewShapeStroke(int targetCellIndex)
    {
        if (_strokeSnapshot is null || _strokeAnchorCellIndex < 0)
        {
            return;
        }

        PixelStudioLayerState sourceLayer = _strokeSnapshot.Frames[_pixelStudio.ActiveFrameIndex].Layers[_pixelStudio.ActiveLayerIndex];
        foreach (int dirtyIndex in _linePreviewIndices)
        {
            if (dirtyIndex < 0 || dirtyIndex >= CurrentPixelLayer.Pixels.Length)
            {
                continue;
            }

            CurrentPixelLayer.Pixels[dirtyIndex] = sourceLayer.Pixels[dirtyIndex];
            _pixelDirtyIndices.Add(dirtyIndex);
        }

        _linePreviewIndices.Clear();
        _strokeChanged = false;
        targetCellIndex = ResolveShapeTargetCellIndex(targetCellIndex);
        IEnumerable<int> shapeCells = EnumerateActiveShapeCells(_strokeAnchorCellIndex, targetCellIndex);
        foreach (int cellIndex in shapeCells)
        {
            ApplyShapeCell(cellIndex);
        }

        _linePreviewIndices.UnionWith(_pixelDirtyIndices);
        if (_strokeChanged)
        {
            RefreshPixelStudioPixels(_pixelDirtyIndices);
            _pixelDirtyIndices.Clear();
        }
    }

    private int ResolveShapeTargetCellIndex(int targetCellIndex)
    {
        if (!SupportsConstrainedShapePreview(_pixelStudio.ActiveTool) || !_shiftPressed || _strokeAnchorCellIndex < 0)
        {
            return targetCellIndex;
        }

        int startX = _strokeAnchorCellIndex % _pixelStudio.CanvasWidth;
        int startY = _strokeAnchorCellIndex / _pixelStudio.CanvasWidth;
        int targetX = targetCellIndex % _pixelStudio.CanvasWidth;
        int targetY = targetCellIndex / _pixelStudio.CanvasWidth;
        int deltaX = targetX - startX;
        int deltaY = targetY - startY;
        int dominantDistance = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        int directionX = deltaX == 0 ? 1 : Math.Sign(deltaX);
        int directionY = deltaY == 0 ? 1 : Math.Sign(deltaY);
        int resolvedX = Math.Clamp(startX + (directionX * dominantDistance), 0, _pixelStudio.CanvasWidth - 1);
        int resolvedY = Math.Clamp(startY + (directionY * dominantDistance), 0, _pixelStudio.CanvasHeight - 1);
        return (resolvedY * _pixelStudio.CanvasWidth) + resolvedX;
    }

    private IEnumerable<int> EnumerateActiveShapeCells(int startCellIndex, int endCellIndex)
    {
        return _pixelStudio.ActiveTool switch
        {
            PixelStudioToolKind.Rectangle => _rectangleRenderMode == PixelStudioShapeRenderMode.Filled
                ? EnumerateRectangleFilledCells(startCellIndex, endCellIndex)
                : EnumerateRectangleOutlineCells(startCellIndex, endCellIndex),
            PixelStudioToolKind.Ellipse => _ellipseRenderMode == PixelStudioShapeRenderMode.Filled
                ? EnumerateEllipseFilledCells(startCellIndex, endCellIndex)
                : EnumerateEllipseOutlineCells(startCellIndex, endCellIndex),
            PixelStudioToolKind.Shape => EnumeratePresetShapeCells(startCellIndex, endCellIndex),
            _ => []
        };
    }

    private IEnumerable<int> EnumerateRectangleOutlineCells(int startCellIndex, int endCellIndex)
    {
        GetShapeBounds(startCellIndex, endCellIndex, out int left, out int right, out int top, out int bottom, out _, out _);
        HashSet<int> outline = [];
        for (int x = left; x <= right; x++)
        {
            outline.Add((top * _pixelStudio.CanvasWidth) + x);
            outline.Add((bottom * _pixelStudio.CanvasWidth) + x);
        }

        for (int y = top; y <= bottom; y++)
        {
            outline.Add((y * _pixelStudio.CanvasWidth) + left);
            outline.Add((y * _pixelStudio.CanvasWidth) + right);
        }

        return outline;
    }

    private IEnumerable<int> EnumerateRectangleFilledCells(int startCellIndex, int endCellIndex)
    {
        GetShapeBounds(startCellIndex, endCellIndex, out int left, out int right, out int top, out int bottom, out _, out _);
        List<int> filled = [];
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                filled.Add((y * _pixelStudio.CanvasWidth) + x);
            }
        }

        return filled;
    }

    private IEnumerable<int> EnumerateEllipseOutlineCells(int startCellIndex, int endCellIndex)
    {
        GetShapeBounds(startCellIndex, endCellIndex, out int left, out int right, out int top, out int bottom, out int width, out int height);
        if (width <= 2 || height <= 2)
        {
            return EnumerateRectangleOutlineCells(startCellIndex, endCellIndex);
        }

        if (IsTinyShape(width, height))
        {
            return EnumerateTinyEllipseCells(left, right, top, bottom, filled: false);
        }

        float radiusX = width * 0.5f;
        float radiusY = height * 0.5f;
        float centerX = left + radiusX;
        float centerY = top + radiusY;
        HashSet<int> outline = [];

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (!IsEllipseCellInside(x, y, centerX, centerY, radiusX, radiusY))
                {
                    continue;
                }

                if (IsEllipseBoundaryCell(x, y, left, right, top, bottom, centerX, centerY, radiusX, radiusY))
                {
                    outline.Add((y * _pixelStudio.CanvasWidth) + x);
                }
            }
        }

        return outline;
    }

    private IEnumerable<int> EnumerateEllipseFilledCells(int startCellIndex, int endCellIndex)
    {
        GetShapeBounds(startCellIndex, endCellIndex, out int left, out int right, out int top, out int bottom, out int width, out int height);
        if (width <= 2 || height <= 2)
        {
            return EnumerateRectangleFilledCells(startCellIndex, endCellIndex);
        }

        if (IsTinyShape(width, height))
        {
            return EnumerateTinyEllipseCells(left, right, top, bottom, filled: true);
        }

        float radiusX = width * 0.5f;
        float radiusY = height * 0.5f;
        float centerX = left + radiusX;
        float centerY = top + radiusY;
        List<int> filled = [];
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (IsEllipseCellInside(x, y, centerX, centerY, radiusX, radiusY))
                {
                    filled.Add((y * _pixelStudio.CanvasWidth) + x);
                }
            }
        }

        return filled;
    }

    private IEnumerable<int> EnumeratePresetShapeCells(int startCellIndex, int endCellIndex)
    {
        GetShapeBounds(startCellIndex, endCellIndex, out int left, out int right, out int top, out int bottom, out int width, out int height);
        if (IsTinyShape(width, height))
        {
            return _shapeRenderMode == PixelStudioShapeRenderMode.Filled
                ? EnumerateRectangleFilledCells(startCellIndex, endCellIndex)
                : EnumerateRectangleOutlineCells(startCellIndex, endCellIndex);
        }

        HashSet<int> filledCells = BuildPresetShapeFilledSet(startCellIndex, endCellIndex);
        if (_shapeRenderMode == PixelStudioShapeRenderMode.Filled)
        {
            return filledCells;
        }

        return BuildPresetShapeOutlineSet(startCellIndex, endCellIndex);
    }

    private HashSet<int> BuildPresetShapeFilledSet(int startCellIndex, int endCellIndex)
    {
        GetShapeBounds(startCellIndex, endCellIndex, out int left, out int right, out int top, out int bottom, out int width, out int height);
        HashSet<int> filled = [];
        if (width <= 0 || height <= 0)
        {
            return filled;
        }

        (float X, float Y)[] polygon = BuildScaledPresetShapePolygon(startCellIndex, endCellIndex);
        if (polygon.Length == 0)
        {
            return filled;
        }

        foreach (int index in BuildPresetShapeOutlineSet(startCellIndex, endCellIndex))
        {
            filled.Add(index);
        }

        for (int y = top; y <= bottom; y++)
        {
            float scanY = y + 0.5f;
            List<float> intersections = [];
            for (int index = 0; index < polygon.Length; index++)
            {
                (float X, float Y) from = polygon[index];
                (float X, float Y) to = polygon[(index + 1) % polygon.Length];
                bool crossesScanline =
                    (from.Y <= scanY && to.Y > scanY)
                    || (to.Y <= scanY && from.Y > scanY);
                if (!crossesScanline)
                {
                    continue;
                }

                float intersectionX = from.X + (((scanY - from.Y) / (to.Y - from.Y)) * (to.X - from.X));
                intersections.Add(intersectionX);
            }

            intersections.Sort();
            for (int index = 0; index + 1 < intersections.Count; index += 2)
            {
                int startX = Math.Clamp((int)MathF.Ceiling(intersections[index] - 0.5f), left, right);
                int endX = Math.Clamp((int)MathF.Floor(intersections[index + 1] - 0.5f), left, right);
                for (int x = startX; x <= endX; x++)
                {
                    filled.Add((y * _pixelStudio.CanvasWidth) + x);
                }
            }
        }

        return filled;
    }

    private IEnumerable<int> BuildPresetShapeOutlineSet(int startCellIndex, int endCellIndex)
    {
        (float X, float Y)[] polygon = BuildScaledPresetShapePolygon(startCellIndex, endCellIndex);
        if (polygon.Length == 0)
        {
            return [];
        }

        HashSet<int> outline = [];
        for (int index = 0; index < polygon.Length; index++)
        {
            (float X, float Y) from = polygon[index];
            (float X, float Y) to = polygon[(index + 1) % polygon.Length];
            int fromX = Math.Clamp((int)MathF.Round(from.X - 0.5f), 0, _pixelStudio.CanvasWidth - 1);
            int fromY = Math.Clamp((int)MathF.Round(from.Y - 0.5f), 0, _pixelStudio.CanvasHeight - 1);
            int toX = Math.Clamp((int)MathF.Round(to.X - 0.5f), 0, _pixelStudio.CanvasWidth - 1);
            int toY = Math.Clamp((int)MathF.Round(to.Y - 0.5f), 0, _pixelStudio.CanvasHeight - 1);
            int fromCellIndex = (fromY * _pixelStudio.CanvasWidth) + fromX;
            int toCellIndex = (toY * _pixelStudio.CanvasWidth) + toX;
            foreach (int cellIndex in EnumerateStrokeCells(fromCellIndex, toCellIndex))
            {
                outline.Add(cellIndex);
            }
        }

        return outline;
    }

    private IEnumerable<int> EnumerateTinyEllipseCells(int left, int right, int top, int bottom, bool filled)
    {
        int width = Math.Max(right - left + 1, 1);
        int height = Math.Max(bottom - top + 1, 1);
        if (width <= 1 || height <= 1)
        {
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    yield return (y * _pixelStudio.CanvasWidth) + x;
                }
            }

            yield break;
        }

        float centerX = (left + right) * 0.5f;
        float centerY = (top + bottom) * 0.5f;
        float radiusX = Math.Max((width - 1) * 0.5f, 0.5f);
        float radiusY = Math.Max((height - 1) * 0.5f, 0.5f);
        HashSet<int> inside = [];
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                float normalizedX = (x - centerX) / radiusX;
                float normalizedY = (y - centerY) / radiusY;
                if ((normalizedX * normalizedX) + (normalizedY * normalizedY) <= 1.15f)
                {
                    inside.Add((y * _pixelStudio.CanvasWidth) + x);
                }
            }
        }

        if (filled || inside.Count <= 2)
        {
            foreach (int index in inside)
            {
                yield return index;
            }

            yield break;
        }

        foreach (int index in inside)
        {
            int x = index % _pixelStudio.CanvasWidth;
            int y = index / _pixelStudio.CanvasWidth;
            bool hasOutsideNeighbor =
                x <= left
                || x >= right
                || y <= top
                || y >= bottom
                || !inside.Contains((y * _pixelStudio.CanvasWidth) + (x - 1))
                || !inside.Contains((y * _pixelStudio.CanvasWidth) + (x + 1))
                || !inside.Contains(((y - 1) * _pixelStudio.CanvasWidth) + x)
                || !inside.Contains(((y + 1) * _pixelStudio.CanvasWidth) + x);
            if (hasOutsideNeighbor)
            {
                yield return index;
            }
        }
    }

    private static bool IsWithinPresetShape(PixelStudioShapePreset preset, float normalizedX, float normalizedY)
    {
        return preset switch
        {
            PixelStudioShapePreset.Heart => IsWithinHeartShape(normalizedX, normalizedY),
            PixelStudioShapePreset.Teardrop => IsWithinTeardropShape(normalizedX, normalizedY),
            PixelStudioShapePreset.Triangle => IsPointInPolygon(TrianglePolygonPoints, normalizedX, normalizedY),
            PixelStudioShapePreset.Diamond => IsPointInPolygon(DiamondPolygonPoints, normalizedX, normalizedY),
            _ => IsWithinStarShape(normalizedX, normalizedY)
        };
    }

    private static bool IsWithinHeartShape(float normalizedX, float normalizedY)
    {
        return IsPointInPolygon(HeartPolygonPoints, normalizedX, normalizedY);
    }

    private static bool IsWithinTeardropShape(float normalizedX, float normalizedY)
    {
        return IsPointInPolygon(TeardropPolygonPoints, normalizedX, normalizedY);
    }

    private static bool IsWithinStarShape(float normalizedX, float normalizedY)
    {
        return IsPointInPolygon(StarPolygonPoints, normalizedX, normalizedY);
    }

    private (float X, float Y)[] BuildScaledPresetShapePolygon(int startCellIndex, int endCellIndex)
    {
        GetShapeBounds(startCellIndex, endCellIndex, out int left, out int right, out int top, out int bottom, out int width, out int height);
        (float X, float Y)[]? normalizedPolygon = GetPresetShapePolygon(_shapePreset);
        if (normalizedPolygon is null || normalizedPolygon.Length == 0)
        {
            return [];
        }

        float widthSpan = Math.Max(width - 1, 0);
        float heightSpan = Math.Max(height - 1, 0);
        (float X, float Y)[] scaled = new (float X, float Y)[normalizedPolygon.Length];
        for (int index = 0; index < normalizedPolygon.Length; index++)
        {
            (float normalizedX, float normalizedY) = normalizedPolygon[index];
            float x = left + 0.5f + (((normalizedX + 1f) * 0.5f) * widthSpan);
            float y = top + 0.5f + (((normalizedY + 1f) * 0.5f) * heightSpan);
            scaled[index] = (x, y);
        }

        return scaled;
    }

    private static (float X, float Y)[]? GetPresetShapePolygon(PixelStudioShapePreset preset)
    {
        return preset switch
        {
            PixelStudioShapePreset.Star => StarPolygonPoints,
            PixelStudioShapePreset.Heart => HeartPolygonPoints,
            PixelStudioShapePreset.Teardrop => TeardropPolygonPoints,
            PixelStudioShapePreset.Triangle => TrianglePolygonPoints,
            PixelStudioShapePreset.Diamond => DiamondPolygonPoints,
            _ => null
        };
    }

    private static bool IsPointInPolygon((float X, float Y)[] polygon, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            (float ix, float iy) = polygon[i];
            (float jx, float jy) = polygon[j];
            bool intersects = ((iy > y) != (jy > y))
                && (x < ((jx - ix) * (y - iy) / MathF.Max(jy - iy, 0.0001f)) + ix);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static (float X, float Y)[] BuildRegularStarPolygon(int points, float outerRadius, float innerRadius, float startAngle)
    {
        (float X, float Y)[] polygon = new (float X, float Y)[points * 2];
        for (int index = 0; index < polygon.Length; index++)
        {
            float angle = startAngle + (index * MathF.PI / points);
            float radius = index % 2 == 0 ? outerRadius : innerRadius;
            polygon[index] = (MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
        }

        return polygon;
    }

    private static (float X, float Y)[] BuildHeartPolygonPoints()
    {
        List<(float X, float Y)> points = [];
        for (int index = 0; index < 72; index++)
        {
            float t = index / 72f * MathF.PI * 2f;
            float x = 16f * MathF.Pow(MathF.Sin(t), 3f);
            float y = 13f * MathF.Cos(t) - (5f * MathF.Cos(2f * t)) - (2f * MathF.Cos(3f * t)) - MathF.Cos(4f * t);
            points.Add((x, -y));
        }

        return NormalizePolygonPoints(points);
    }

    private static (float X, float Y)[] NormalizePolygonPoints(IReadOnlyList<(float X, float Y)> points)
    {
        float minX = points.Min(point => point.X);
        float maxX = points.Max(point => point.X);
        float minY = points.Min(point => point.Y);
        float maxY = points.Max(point => point.Y);
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        float scaleX = MathF.Max((maxX - minX) * 0.5f, 0.0001f);
        float scaleY = MathF.Max((maxY - minY) * 0.5f, 0.0001f);
        float scale = MathF.Max(scaleX, scaleY);
        return points
            .Select(point => (((point.X - centerX) / scale) * 0.96f, ((point.Y - centerY) / scale) * 0.96f))
            .ToArray();
    }

    private void GetShapeBounds(
        int startCellIndex,
        int endCellIndex,
        out int left,
        out int right,
        out int top,
        out int bottom,
        out int width,
        out int height)
    {
        int startX = startCellIndex % _pixelStudio.CanvasWidth;
        int startY = startCellIndex / _pixelStudio.CanvasWidth;
        int endX = endCellIndex % _pixelStudio.CanvasWidth;
        int endY = endCellIndex / _pixelStudio.CanvasWidth;
        if (_altPressed && SupportsConstrainedShapePreview(_pixelStudio.ActiveTool))
        {
            int deltaX = endX - startX;
            int deltaY = endY - startY;
            left = Math.Clamp(startX - Math.Abs(deltaX), 0, _pixelStudio.CanvasWidth - 1);
            right = Math.Clamp(startX + Math.Abs(deltaX), 0, _pixelStudio.CanvasWidth - 1);
            top = Math.Clamp(startY - Math.Abs(deltaY), 0, _pixelStudio.CanvasHeight - 1);
            bottom = Math.Clamp(startY + Math.Abs(deltaY), 0, _pixelStudio.CanvasHeight - 1);
        }
        else
        {
            left = Math.Min(startX, endX);
            right = Math.Max(startX, endX);
            top = Math.Min(startY, endY);
            bottom = Math.Max(startY, endY);
        }

        width = Math.Max(right - left + 1, 1);
        height = Math.Max(bottom - top + 1, 1);
    }

    private static bool SupportsConstrainedShapePreview(PixelStudioToolKind tool)
        => tool is PixelStudioToolKind.Rectangle or PixelStudioToolKind.Ellipse or PixelStudioToolKind.Shape;

    private static bool IsTinyShape(int width, int height)
        => width <= 3 || height <= 3;

    private static bool SupportsToolContextMenu(PixelStudioToolKind tool)
        => tool is PixelStudioToolKind.Select
            or PixelStudioToolKind.Pencil
            or PixelStudioToolKind.Eraser
            or PixelStudioToolKind.Rectangle
            or PixelStudioToolKind.Ellipse
            or PixelStudioToolKind.Shape;

    private static bool IsEllipseCellInside(int x, int y, float centerX, float centerY, float radiusX, float radiusY)
    {
        if (radiusX <= 0f || radiusY <= 0f)
        {
            return false;
        }

        float normalizedX = ((x + 0.5f) - centerX) / radiusX;
        float normalizedY = ((y + 0.5f) - centerY) / radiusY;
        return (normalizedX * normalizedX) + (normalizedY * normalizedY) <= 1f;
    }

    private static bool IsEllipseBoundaryCell(
        int x,
        int y,
        int left,
        int right,
        int top,
        int bottom,
        float centerX,
        float centerY,
        float radiusX,
        float radiusY)
    {
        if (x <= left || x >= right || y <= top || y >= bottom)
        {
            return true;
        }

        return !IsEllipseCellInside(x - 1, y, centerX, centerY, radiusX, radiusY)
            || !IsEllipseCellInside(x + 1, y, centerX, centerY, radiusX, radiusY)
            || !IsEllipseCellInside(x, y - 1, centerX, centerY, radiusX, radiusY)
            || !IsEllipseCellInside(x, y + 1, centerX, centerY, radiusX, radiusY);
    }

    private void BeginSelection(int cellIndex)
    {
        int x = cellIndex % _pixelStudio.CanvasWidth;
        int y = cellIndex / _pixelStudio.CanvasWidth;

        if (_selectionTransformModeActive)
        {
            RefreshPixelStudioView("Transform mode is active. Use the transform handles, press Enter to apply, or Esc to cancel.");
            return;
        }

        if (_selectionCommitted && !_selectionDragActive && IsWithinCurrentSelection(x, y))
        {
            if (!CanTransformCurrentLayer("moving the selection"))
            {
                return;
            }

            BeginSelectionMove(x, y);
            return;
        }

        if (_selectionMode is PixelStudioSelectionMode.AutoGlobal or PixelStudioSelectionMode.AutoLocal)
        {
            _selectionTransformModeActive = false;
            ClearSelectionTransformPreview();
            CreateAutomaticSelection(cellIndex, _selectionMode == PixelStudioSelectionMode.AutoGlobal);
            return;
        }

        ResetSelectionMoveState();
        _selectionTransformModeActive = false;
        ClearSelectionTransformPreview();
        _selectionMask.Clear();
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
        if (_selectionMoveActive)
        {
            CommitSelectionMove();
            return;
        }

        if (!_selectionActive || !_selectionDragActive)
        {
            return;
        }

        _selectionDragActive = false;
        _selectionCommitted = true;
        RefreshPixelStudioView($"Selection set to {GetSelectionWidth()}x{GetSelectionHeight()}.", rebuildLayout: true);
    }

    private void CreateAutomaticSelection(int cellIndex, bool global)
    {
        int targetPixel = CurrentPixelLayer.Pixels[cellIndex];
        if (IsTransparentPixel(targetPixel))
        {
            RefreshPixelStudioView("Pick a painted pixel on the active layer for automatic selection.");
            return;
        }

        HashSet<int> selectedIndices = global
            ? BuildGlobalSelectionMask(targetPixel)
            : BuildLocalSelectionMask(cellIndex, targetPixel);
        if (selectedIndices.Count == 0)
        {
            RefreshPixelStudioView("No matching pixels were found for automatic selection.");
            return;
        }

        ResetSelectionMoveState();
        _selectionTransformModeActive = false;
        ClearSelectionTransformPreview();
        ApplySelectionMask(selectedIndices, committed: true);
        string modeLabel = global ? "global" : "local";
        RefreshPixelStudioView($"Automatic {modeLabel} selection captured {selectedIndices.Count} pixel(s).", rebuildLayout: true);
    }

    private HashSet<int> BuildGlobalSelectionMask(int pixelValue)
    {
        HashSet<int> indices = [];
        int[] pixels = CurrentPixelLayer.Pixels;
        for (int index = 0; index < pixels.Length; index++)
        {
            if (pixels[index] == pixelValue)
            {
                indices.Add(index);
            }
        }

        return indices;
    }

    private HashSet<int> BuildLocalSelectionMask(int startIndex, int pixelValue)
    {
        HashSet<int> indices = [];
        Queue<int> pending = new();
        pending.Enqueue(startIndex);
        while (pending.Count > 0)
        {
            int index = pending.Dequeue();
            if (!indices.Add(index))
            {
                continue;
            }

            if (index < 0 || index >= CurrentPixelLayer.Pixels.Length || CurrentPixelLayer.Pixels[index] != pixelValue)
            {
                indices.Remove(index);
                continue;
            }

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

        return indices;
    }

    private void BeginSelectionMove(int cellX, int cellY)
    {
        if (!_selectionActive || !_selectionCommitted)
        {
            return;
        }

        _selectionMoveActive = true;
        _pixelDragMode = PixelStudioDragMode.MoveSelection;
        _selectionMovePointerCellX = cellX;
        _selectionMovePointerCellY = cellY;
        _selectionMoveOriginLeft = GetSelectionLeft();
        _selectionMoveOriginTop = GetSelectionTop();
        _selectionMoveCurrentLeft = _selectionMoveOriginLeft;
        _selectionMoveCurrentTop = _selectionMoveOriginTop;
        _selectionMoveWidth = GetSelectionWidth();
        _selectionMoveHeight = GetSelectionHeight();
        _selectionMoveUsesMask = SelectionUsesMask();
        _selectionMoveSourceIndices = EnumerateSelectedIndices()
            .Where(index => index >= 0 && index < CurrentPixelLayer.Pixels.Length)
            .Distinct()
            .ToArray();
        if (_selectionMoveSourceIndices.Length == 0)
        {
            ResetSelectionMoveState();
            return;
        }

        _selectionMoveSnapshot = CapturePixelStudioState();
        _selectionMoveLayerSnapshot = CurrentPixelLayer.Pixels.ToArray();
        _selectionMovePixels = BuildSelectionClipboardBuffer(_selectionMoveOriginLeft, _selectionMoveOriginTop, _selectionMoveWidth, _selectionMoveHeight);

        RefreshPixelStudioInteraction();
    }

    private void UpdateSelectionMove(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (!_selectionMoveActive || _selectionMovePixels is null || _selectionMoveLayerSnapshot is null)
        {
            return;
        }

        if (!TryGetCanvasCellCoordinates(layout, mouseX, mouseY, out int cellX, out int cellY, clampToCanvas: true))
        {
            return;
        }

        int deltaX = cellX - _selectionMovePointerCellX;
        int deltaY = cellY - _selectionMovePointerCellY;
        int maxLeft = Math.Max(_pixelStudio.CanvasWidth - _selectionMoveWidth, 0);
        int maxTop = Math.Max(_pixelStudio.CanvasHeight - _selectionMoveHeight, 0);
        int targetLeft = Math.Clamp(_selectionMoveOriginLeft + deltaX, 0, maxLeft);
        int targetTop = Math.Clamp(_selectionMoveOriginTop + deltaY, 0, maxTop);
        if (targetLeft == _selectionMoveCurrentLeft && targetTop == _selectionMoveCurrentTop)
        {
            return;
        }

        ApplySelectionMovePreview(targetLeft, targetTop);
    }

    private void ApplySelectionMovePreview(int targetLeft, int targetTop)
    {
        if (_selectionMovePixels is null || _selectionMoveLayerSnapshot is null || _selectionMoveSourceIndices is null)
        {
            return;
        }

        Array.Copy(_selectionMoveLayerSnapshot, CurrentPixelLayer.Pixels, _selectionMoveLayerSnapshot.Length);

        foreach (int sourceIndex in _selectionMoveSourceIndices)
        {
                CurrentPixelLayer.Pixels[sourceIndex] = TransparentPixelValue;
        }

        HashSet<int>? movedIndices = _selectionMoveUsesMask ? [] : null;
        for (int y = 0; y < _selectionMoveHeight; y++)
        {
            for (int x = 0; x < _selectionMoveWidth; x++)
            {
                int sourceValue = _selectionMovePixels[(y * _selectionMoveWidth) + x];
                if (sourceValue == ClipboardEmptyPixel)
                {
                    continue;
                }

                int targetIndex = ((targetTop + y) * _pixelStudio.CanvasWidth) + (targetLeft + x);
                CurrentPixelLayer.Pixels[targetIndex] = sourceValue;
                movedIndices?.Add(targetIndex);
            }
        }

        _selectionMoveCurrentLeft = targetLeft;
        _selectionMoveCurrentTop = targetTop;
        if (_selectionMoveUsesMask && movedIndices is not null)
        {
            ApplySelectionMask(movedIndices, committed: true);
        }
        else
        {
            SetSelectionRect(targetLeft, targetTop, _selectionMoveWidth, _selectionMoveHeight);
        }
        RefreshPixelStudioPixels();
        RefreshPixelStudioInteraction();
    }

    private void CommitSelectionMove()
    {
        bool moved = _selectionMoveCurrentLeft != _selectionMoveOriginLeft || _selectionMoveCurrentTop != _selectionMoveOriginTop;
        int targetLeft = _selectionMoveCurrentLeft;
        int targetTop = _selectionMoveCurrentTop;
        PixelStudioState? moveSnapshot = _selectionMoveSnapshot;
        ResetSelectionMoveState();
        _selectionCommitted = true;
        _selectionDragActive = false;

        if (!moved || moveSnapshot is null)
        {
            RefreshPixelStudioInteraction();
            return;
        }

        _pixelUndoStack.Push(moveSnapshot);
        _pixelRedoStack.Clear();
        MarkPixelStudioRecoveryDirty();
        RefreshPixelStudioView($"Moved selection to {targetLeft + 1},{targetTop + 1}.");
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
            MarkPixelStudioRecoveryDirty();
            string status = _pixelStudio.ActiveTool switch
            {
                PixelStudioToolKind.Eraser => "Erased pixel stroke.",
                PixelStudioToolKind.Line => "Committed line stroke.",
                PixelStudioToolKind.Rectangle => "Committed rectangle stroke.",
                PixelStudioToolKind.Ellipse => "Committed ellipse stroke.",
                _ => "Painted pixel stroke."
            };
            RefreshPixelStudioView(status);
        }

        _strokeSnapshot = null;
        _isPixelStrokeActive = false;
        _strokeChanged = false;
        _lastStrokeCellIndex = -1;
        _strokeAnchorCellIndex = -1;
        _pixelDirtyIndices.Clear();
        _linePreviewIndices.Clear();
    }

    private void ExecutePixelStudioTool(PixelStudioToolKind tool)
    {
        StopPixelPlayback();
        if (tool != PixelStudioToolKind.Select && _selectionActive && !_selectionCommitted)
        {
            ClearSelection();
        }

        if (tool != PixelStudioToolKind.Select && _selectionTransformModeActive)
        {
            _selectionTransformModeActive = false;
            ClearSelectionTransformPreview();
        }

        ActivatePixelStudioTool(tool);
        RefreshPixelStudioView($"{EditorBranding.PixelToolName} tool: {GetPixelStudioToolStatusLabel(tool)}.");
    }

    private void ExecutePixelStudioAction(PixelStudioAction action)
    {
        switch (action)
        {
            case PixelStudioAction.NewBlankDocument:
                ApplyPixelStudioChange("Created a blank sprite document.", () =>
                {
                    ReplacePixelStudioDocument(CreateBlankPixelStudio(32, 32));
                    _currentPixelDocumentPath = null;
                    _pixelRecoveryBannerVisible = false;
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
                    _currentPixelDocumentPath = null;
                    _pixelRecoveryBannerVisible = false;
                    return true;
                });
                break;
            case PixelStudioAction.ImportImage:
                ImportPixelStudioImage();
                break;
            case PixelStudioAction.ExportSpriteStrip:
                ExportPixelStudioSpriteStrip();
                break;
            case PixelStudioAction.ExportPngSequence:
                ExportPixelStudioPngSequence();
                break;
            case PixelStudioAction.ExportGif:
                ExportPixelStudioGif();
                break;
            case PixelStudioAction.ToggleNavigatorPanel:
                _pixelNavigatorVisible = !_pixelNavigatorVisible;
                RefreshPixelStudioView(_pixelNavigatorVisible ? "Opened navigator preview." : "Hidden navigator preview.", rebuildLayout: true);
                break;
            case PixelStudioAction.ToggleAnimationPreviewPanel:
                RefreshPixelStudioView("Playback preview stays docked in the Frames panel.");
                break;
            case PixelStudioAction.ToggleOnionSkin:
                _pixelStudio.ShowOnionSkin = !_pixelStudio.ShowOnionSkin;
                NormalizeOnionDisplayState();
                RefreshPixelStudioView(_pixelStudio.ShowOnionSkin ? "Onion skin enabled." : "Onion skin disabled.", rebuildLayout: true, refreshPixelBuffers: true);
                break;
            case PixelStudioAction.ToggleOnionPrevious:
                if (_pixelStudio.AllowDualOnion)
                {
                    _pixelStudio.ShowPreviousOnion = !_pixelStudio.ShowPreviousOnion;
                    NormalizeOnionDisplayState();
                    RefreshPixelStudioView(
                        _pixelStudio.ShowPreviousOnion ? "Previous onion enabled." : "Previous onion hidden.",
                        rebuildLayout: true,
                        refreshPixelBuffers: true);
                }
                else
                {
                    SetOnionDisplayMode(showPrevious: true, showNext: false, allowDual: false, "Showing the previous frame onion.");
                }

                break;
            case PixelStudioAction.ToggleOnionNext:
                if (_pixelStudio.AllowDualOnion)
                {
                    _pixelStudio.ShowNextOnion = !_pixelStudio.ShowNextOnion;
                    NormalizeOnionDisplayState();
                    RefreshPixelStudioView(
                        _pixelStudio.ShowNextOnion ? "Next onion enabled." : "Next onion hidden.",
                        rebuildLayout: true,
                        refreshPixelBuffers: true);
                }
                else
                {
                    SetOnionDisplayMode(showPrevious: false, showNext: true, allowDual: false, "Showing the next frame onion.");
                }

                break;
            case PixelStudioAction.ClearSelection:
                if (_selectionActive)
                {
                    ClearSelection();
                    RefreshPixelStudioView("Selection cleared.", rebuildLayout: true);
                }
                else
                {
                    RefreshPixelStudioView("No active selection to clear.");
                }
                break;
            case PixelStudioAction.ToggleSelectionTransformMode:
                if (!_selectionActive || !_selectionCommitted)
                {
                    RefreshPixelStudioView("Make a selection before enabling transform.");
                    break;
                }

                ActivatePixelStudioTool(PixelStudioToolKind.Select);
                _selectionTransformModeActive = !_selectionTransformModeActive;
                ClearSelectionTransformPreview();
                ResetSelectionTransformInputEditing();
                ResetSelectionTransformPivotToSelectionCenter();
                RefreshPixelStudioView(
                    _selectionTransformModeActive
                        ? $"Transform enabled. Drag handles to scale, drag the pivot or top grip to rotate, hold Shift to snap to {_transformRotationSnapDegrees} deg, Enter to apply, Esc to cancel."
                        : "Transform handles hidden.",
                    rebuildLayout: true);
                break;
            case PixelStudioAction.ActivateTransformAngleField:
                ActivateSelectionTransformAngleField();
                break;
            case PixelStudioAction.CopySelection:
                CopySelectionPixels();
                break;
            case PixelStudioAction.CutSelection:
                CutSelectionPixels();
                break;
            case PixelStudioAction.PasteSelection:
                PasteSelectionPixels();
                break;
            case PixelStudioAction.FlipSelectionHorizontal:
                FlipSelectionPixels(horizontal: true);
                break;
            case PixelStudioAction.FlipSelectionVertical:
                FlipSelectionPixels(horizontal: false);
                break;
            case PixelStudioAction.RotateSelectionClockwise:
                RotateSelectionPixels(clockwise: true);
                break;
            case PixelStudioAction.RotateSelectionCounterClockwise:
                RotateSelectionPixels(clockwise: false);
                break;
            case PixelStudioAction.ScaleSelectionUp:
                ScaleSelectionPixels(scaleUp: true);
                break;
            case PixelStudioAction.ScaleSelectionDown:
                ScaleSelectionPixels(scaleUp: false);
                break;
            case PixelStudioAction.ConfirmWarningDialog:
                ConfirmPixelWarningDialog();
                break;
            case PixelStudioAction.AlternateWarningDialog:
                ExecuteAlternatePixelWarningDialog();
                break;
            case PixelStudioAction.CancelWarningDialog:
                ClosePixelWarningDialog("Warning dismissed.");
                break;
            case PixelStudioAction.NudgeSelectionLeft:
                NudgeSelectionBy(-1, 0);
                break;
            case PixelStudioAction.NudgeSelectionRight:
                NudgeSelectionBy(1, 0);
                break;
            case PixelStudioAction.NudgeSelectionUp:
                NudgeSelectionBy(0, -1);
                break;
            case PixelStudioAction.NudgeSelectionDown:
                NudgeSelectionBy(0, 1);
                break;
            case PixelStudioAction.OpenCanvasResizeDialog:
                OpenCanvasResizeDialog(_pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight);
                break;
            case PixelStudioAction.ResizeCanvas16:
                RequestPixelCanvasResize(16, 16);
                break;
            case PixelStudioAction.ResizeCanvas32:
                RequestPixelCanvasResize(32, 32);
                break;
            case PixelStudioAction.ResizeCanvas64:
                RequestPixelCanvasResize(64, 64);
                break;
            case PixelStudioAction.ResizeCanvas128:
                RequestPixelCanvasResize(128, 128);
                break;
            case PixelStudioAction.ResizeCanvas256:
                RequestPixelCanvasResize(256, 256);
                break;
            case PixelStudioAction.ResizeCanvas512:
                RequestPixelCanvasResize(512, 512);
                break;
            case PixelStudioAction.ActivateCanvasResizeWidthField:
                _canvasResizeActiveField = CanvasResizeInputField.Width;
                SelectAllText(EditableTextTarget.CanvasResizeWidth);
                RefreshPixelStudioView("Editing canvas width.", rebuildLayout: true);
                break;
            case PixelStudioAction.ActivateCanvasResizeHeightField:
                _canvasResizeActiveField = CanvasResizeInputField.Height;
                SelectAllText(EditableTextTarget.CanvasResizeHeight);
                RefreshPixelStudioView("Editing canvas height.", rebuildLayout: true);
                break;
            case PixelStudioAction.SetCanvasResizeAnchorTopLeft:
                SetCanvasResizeAnchor(PixelStudioResizeAnchor.TopLeft);
                break;
            case PixelStudioAction.SetCanvasResizeAnchorTop:
                SetCanvasResizeAnchor(PixelStudioResizeAnchor.Top);
                break;
            case PixelStudioAction.SetCanvasResizeAnchorTopRight:
                SetCanvasResizeAnchor(PixelStudioResizeAnchor.TopRight);
                break;
            case PixelStudioAction.SetCanvasResizeAnchorLeft:
                SetCanvasResizeAnchor(PixelStudioResizeAnchor.Left);
                break;
            case PixelStudioAction.SetCanvasResizeAnchorCenter:
                SetCanvasResizeAnchor(PixelStudioResizeAnchor.Center);
                break;
            case PixelStudioAction.SetCanvasResizeAnchorRight:
                SetCanvasResizeAnchor(PixelStudioResizeAnchor.Right);
                break;
            case PixelStudioAction.SetCanvasResizeAnchorBottomLeft:
                SetCanvasResizeAnchor(PixelStudioResizeAnchor.BottomLeft);
                break;
            case PixelStudioAction.SetCanvasResizeAnchorBottom:
                SetCanvasResizeAnchor(PixelStudioResizeAnchor.Bottom);
                break;
            case PixelStudioAction.SetCanvasResizeAnchorBottomRight:
                SetCanvasResizeAnchor(PixelStudioResizeAnchor.BottomRight);
                break;
            case PixelStudioAction.ApplyCanvasResize:
                ApplyCanvasResizeFromDialog();
                break;
            case PixelStudioAction.CancelCanvasResize:
                CloseCanvasResizeDialog("Canvas resize cancelled.");
                break;
            case PixelStudioAction.ZoomOut:
                _pixelStudio.DesiredZoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom - 2);
                RefreshPixelStudioCameraLayout($"{EditorBranding.PixelToolName} zoom target set to {_pixelStudio.DesiredZoom}x.");
                break;
            case PixelStudioAction.ZoomIn:
                _pixelStudio.DesiredZoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom + 2);
                RefreshPixelStudioCameraLayout($"{EditorBranding.PixelToolName} zoom target set to {_pixelStudio.DesiredZoom}x.");
                break;
            case PixelStudioAction.ToggleGrid:
                _pixelStudio.ShowGrid = !_pixelStudio.ShowGrid;
                RefreshPixelStudioView(_pixelStudio.ShowGrid ? $"{EditorBranding.PixelToolName} grid enabled." : $"{EditorBranding.PixelToolName} grid hidden.");
                break;
            case PixelStudioAction.CycleMirrorMode:
                _mirrorMode = GetNextMirrorMode(_mirrorMode);
                RefreshPixelStudioView(BuildMirrorModeStatusText());
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
                SetCurrentBrushSize(_pixelStudio.BrushSize - 1);
                RefreshPixelStudioView($"Brush size set to {_pixelStudio.BrushSize}px.");
                break;
            case PixelStudioAction.IncreaseBrushSize:
                SetCurrentBrushSize(_pixelStudio.BrushSize + 1);
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
            case PixelStudioAction.NewBlankPalette:
                CreateBlankPalette();
                break;
            case PixelStudioAction.AddPaletteSwatch:
                AddPaletteSwatch();
                break;
            case PixelStudioAction.GeneratePaletteRamp:
                GeneratePaletteRamp();
                break;
            case PixelStudioAction.SaveCurrentPalette:
                SaveCurrentPalette();
                break;
            case PixelStudioAction.UpdateSelectedPalette:
                UpdateSelectedPalette();
                break;
            case PixelStudioAction.DuplicateSelectedPalette:
                DuplicateSelectedPalette();
                break;
            case PixelStudioAction.ImportPalette:
                ImportPaletteDocument();
                break;
            case PixelStudioAction.ExportPalette:
                ExportSelectedPalette();
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
            case PixelStudioAction.SwapSecondaryColor:
                SwapSecondaryPaletteColor();
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
            case PixelStudioAction.ToggleLayerOpacityControls:
                _layerOpacityControlsVisible = !_layerOpacityControlsVisible;
                RefreshPixelStudioView(_layerOpacityControlsVisible ? "Layer opacity controls shown." : "Layer opacity controls hidden.", rebuildLayout: true);
                break;
            case PixelStudioAction.ToggleLayerAlphaLock:
                TogglePixelLayerAlphaLock(_pixelStudio.ActiveLayerIndex);
                break;
            case PixelStudioAction.DeleteLayer:
                DeletePixelLayer(_pixelStudio.ActiveLayerIndex);
                break;
            case PixelStudioAction.AddFrame:
                AddPixelFrame();
                break;
            case PixelStudioAction.DuplicateFrame:
                DuplicatePixelFrame(_pixelStudio.ActiveFrameIndex);
                break;
            case PixelStudioAction.CopyFrame:
                CopyPixelFrame(_pixelStudio.ActiveFrameIndex);
                break;
            case PixelStudioAction.PasteFrame:
                PastePixelFrame(_pixelStudio.ActiveFrameIndex);
                break;
            case PixelStudioAction.DeleteFrame:
                DeletePixelFrame(_pixelStudio.ActiveFrameIndex);
                break;
            case PixelStudioAction.TogglePlayback:
                TogglePixelPlayback();
                break;
            case PixelStudioAction.DecreaseFrameRate:
                SetGlobalFrameRate(_pixelStudio.FramesPerSecond - 1);
                break;
            case PixelStudioAction.IncreaseFrameRate:
                SetGlobalFrameRate(_pixelStudio.FramesPerSecond + 1);
                break;
            case PixelStudioAction.SetLoopStart:
                SetLoopBoundary(setStartBoundary: true);
                break;
            case PixelStudioAction.SetLoopEnd:
                SetLoopBoundary(setStartBoundary: false);
                break;
            case PixelStudioAction.CyclePlaybackLoopMode:
                CyclePlaybackLoopMode();
                break;
            case PixelStudioAction.DecreaseFrameDuration:
                AdjustActiveFrameDuration(-20);
                break;
            case PixelStudioAction.IncreaseFrameDuration:
                AdjustActiveFrameDuration(20);
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

    private void RequestPixelCanvasResize(int width, int height, bool requireWarning = true)
    {
        width = Math.Clamp(width, 1, MaxPixelCanvasDimension);
        height = Math.Clamp(height, 1, MaxPixelCanvasDimension);
        if (_pixelStudio.CanvasWidth == width && _pixelStudio.CanvasHeight == height)
        {
            RefreshPixelStudioView($"Canvas is already {width}x{height}.");
            return;
        }

        bool isDownsizing = width < _pixelStudio.CanvasWidth || height < _pixelStudio.CanvasHeight;
        if (requireWarning && isDownsizing)
        {
            int croppedPixelCount = CountCroppedPixelsForResize(width, height, PixelStudioResizeAnchor.TopLeft);
            string message = croppedPixelCount > 0
                ? $"Reducing the canvas to {width}x{height} can remove {croppedPixelCount} painted pixel{(croppedPixelCount == 1 ? string.Empty : "s")} outside the new bounds. Continue and review the crop anchor next."
                : $"Reducing the canvas to {width}x{height} can trim your working space. Continue with the smaller canvas size?";
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.ResizeCanvas,
                "Reduce Canvas Size?",
                message,
                width,
                height);
            return;
        }

        ApplyRequestedPixelCanvasResize(width, height);
    }

    private void ApplyRequestedPixelCanvasResize(int width, int height)
    {
        _canvasResizeWidthBuffer = width.ToString();
        _canvasResizeHeightBuffer = height.ToString();
        UpdateCanvasResizePreviewState();
        if (_canvasResizeWouldCrop)
        {
            OpenCanvasResizeDialog(width, height, $"Shrinking to {width}x{height} may crop art. Choose an anchor and confirm.");
            return;
        }

        ResizePixelCanvas(width, height, PixelStudioResizeAnchor.TopLeft);
    }

    private void ResizePixelCanvas(int width, int height, PixelStudioResizeAnchor anchor)
    {
        if (_pixelStudio.CanvasWidth == width && _pixelStudio.CanvasHeight == height)
        {
            RefreshPixelStudioView($"Canvas is already {width}x{height}.");
            return;
        }

        ClearSelection();
        ApplyPixelStudioChange($"Resized canvas to {width}x{height}.", () =>
        {
            int sourceWidth = _pixelStudio.CanvasWidth;
            int sourceHeight = _pixelStudio.CanvasHeight;
            ComputeCanvasResizeOffset(sourceWidth, sourceHeight, width, height, anchor, out int offsetX, out int offsetY);
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                foreach (PixelStudioLayerState layer in frame.Layers)
                {
                    int[] resizedPixels = CreateBlankPixels(width, height);
                    for (int sourceY = 0; sourceY < sourceHeight; sourceY++)
                    {
                        int targetY = sourceY + offsetY;
                        if (targetY < 0 || targetY >= height)
                        {
                            continue;
                        }

                        for (int sourceX = 0; sourceX < sourceWidth; sourceX++)
                        {
                            int targetX = sourceX + offsetX;
                            if (targetX < 0 || targetX >= width)
                            {
                                continue;
                            }

                            resizedPixels[(targetY * width) + targetX] = layer.Pixels[(sourceY * sourceWidth) + sourceX];
                        }
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

    private int CountCroppedPixelsForResize(int targetWidth, int targetHeight, PixelStudioResizeAnchor anchor)
    {
        int sourceWidth = _pixelStudio.CanvasWidth;
        int sourceHeight = _pixelStudio.CanvasHeight;
        ComputeCanvasResizeOffset(sourceWidth, sourceHeight, targetWidth, targetHeight, anchor, out int offsetX, out int offsetY);
        int croppedPixels = 0;

        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            foreach (PixelStudioLayerState layer in frame.Layers)
            {
                for (int sourceY = 0; sourceY < sourceHeight; sourceY++)
                {
                    int targetY = sourceY + offsetY;
                    bool yOutside = targetY < 0 || targetY >= targetHeight;
                    for (int sourceX = 0; sourceX < sourceWidth; sourceX++)
                    {
                        int index = (sourceY * sourceWidth) + sourceX;
                        if (IsTransparentPixel(layer.Pixels[index]))
                        {
                            continue;
                        }

                        int targetX = sourceX + offsetX;
                        if (yOutside || targetX < 0 || targetX >= targetWidth)
                        {
                            croppedPixels++;
                        }
                    }
                }
            }
        }

        return croppedPixels;
    }

    private static void ComputeCanvasResizeOffset(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, PixelStudioResizeAnchor anchor, out int offsetX, out int offsetY)
    {
        (float anchorX, float anchorY) = anchor switch
        {
            PixelStudioResizeAnchor.TopLeft => (0f, 0f),
            PixelStudioResizeAnchor.Top => (0.5f, 0f),
            PixelStudioResizeAnchor.TopRight => (1f, 0f),
            PixelStudioResizeAnchor.Left => (0f, 0.5f),
            PixelStudioResizeAnchor.Center => (0.5f, 0.5f),
            PixelStudioResizeAnchor.Right => (1f, 0.5f),
            PixelStudioResizeAnchor.BottomLeft => (0f, 1f),
            PixelStudioResizeAnchor.Bottom => (0.5f, 1f),
            PixelStudioResizeAnchor.BottomRight => (1f, 1f),
            _ => (0f, 0f)
        };

        offsetX = (int)MathF.Round((targetWidth - sourceWidth) * anchorX);
        offsetY = (int)MathF.Round((targetHeight - sourceHeight) * anchorY);
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
                    IsSharedAcrossFrames = false,
                    Opacity = 1f,
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

        ClosePixelContextMenu();
        _layerRenameActive = false;
        ApplyPixelStudioChange("Duplicated layer.", () =>
        {
            bool sharedAcrossFrames = CurrentPixelFrame.Layers[layerIndex].IsSharedAcrossFrames;
            string duplicateLayerName = BuildUniqueLayerName($"{CurrentPixelFrame.Layers[layerIndex].Name} Copy");
            int[]? sharedPixels = sharedAcrossFrames
                ? CurrentPixelFrame.Layers[layerIndex].Pixels.ToArray()
                : null;
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                PixelStudioLayerState sourceLayer = frame.Layers[layerIndex];
                PixelStudioLayerState duplicateLayer = CloneLayerState(sourceLayer);
                if (sharedAcrossFrames && sharedPixels is not null)
                {
                    duplicateLayer.IsSharedAcrossFrames = true;
                    duplicateLayer.Pixels = sharedPixels;
                }

                frame.Layers.Insert(layerIndex + 1, duplicateLayer);
                frame.Layers[layerIndex + 1].Name = duplicateLayerName;
            }

            _pixelStudio.ActiveLayerIndex = Math.Min(layerIndex + 1, CurrentPixelFrame.Layers.Count - 1);
            return true;
        });
    }

    private void MergePixelLayerDown(int layerIndex)
    {
        if (_pixelStudio.Frames.Count == 0 || CurrentPixelFrame.Layers.Count <= 1)
        {
            RefreshPixelStudioView("Add another layer before merging.");
            return;
        }

        int targetIndex = layerIndex + 1;
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count || targetIndex >= CurrentPixelFrame.Layers.Count)
        {
            RefreshPixelStudioView("Layer is already at the bottom.");
            return;
        }

        PixelStudioLayerState sourceLayer = CurrentPixelFrame.Layers[layerIndex];
        PixelStudioLayerState targetLayer = CurrentPixelFrame.Layers[targetIndex];
        if (!EnsureLayerUnlockedForEditing(
                layerIndex,
                "Source Layer Is Locked",
                $"The source layer \"{sourceLayer.Name}\" is locked. Unlock it before merging down?"))
        {
            return;
        }

        if (!EnsureLayerUnlockedForEditing(
                targetIndex,
                "Lower Layer Is Locked",
                $"The lower layer \"{targetLayer.Name}\" is locked. Unlock it before merging down?"))
        {
            return;
        }

        if (!EnsureLayerAlphaUnlockedForEditing(
                targetIndex,
                "Alpha Lock Blocks The Merge",
                $"Alpha lock is enabled on the lower layer \"{targetLayer.Name}\". Disable alpha lock before merging down so the combined pixels can replace transparency?"))
        {
            return;
        }

        if (!sourceLayer.IsSharedAcrossFrames && targetLayer.IsSharedAcrossFrames)
        {
            RefreshPixelStudioView($"Can't merge frame-local layer \"{sourceLayer.Name}\" into shared layer \"{targetLayer.Name}\". Make the lower layer frame-local first.");
            return;
        }

        ClosePixelContextMenu();
        _layerRenameActive = false;
        ClearSelection();
        ApplyPixelStudioChange($"Merged {sourceLayer.Name} into {targetLayer.Name}.", () =>
        {
            bool sourceShared = CurrentPixelFrame.Layers[layerIndex].IsSharedAcrossFrames;
            bool targetShared = CurrentPixelFrame.Layers[targetIndex].IsSharedAcrossFrames;
            if (sourceShared && targetShared)
            {
                MergeLayerPixelsIntoDestination(_pixelStudio.Frames[0].Layers[layerIndex], _pixelStudio.Frames[0].Layers[targetIndex]);
            }
            else
            {
                foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
                {
                    MergeLayerPixelsIntoDestination(frame.Layers[layerIndex], frame.Layers[targetIndex]);
                }
            }

            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                PixelStudioLayerState frameSource = frame.Layers[layerIndex];
                PixelStudioLayerState frameTarget = frame.Layers[targetIndex];
                frameTarget.IsVisible = frameTarget.IsVisible || frameSource.IsVisible;
                frameTarget.Opacity = 1f;
                frame.Layers.RemoveAt(layerIndex);
            }

            _pixelStudio.ActiveLayerIndex = Math.Clamp(layerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
            RelinkSharedLayerReferences(_pixelStudio.Frames);
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

        MovePixelLayerToIndex(layerIndex, targetIndex, "Moved layer.");
    }

    private void MovePixelLayerToIndex(int sourceIndex, int targetIndex, string status)
    {
        if (sourceIndex < 0 || sourceIndex >= CurrentPixelFrame.Layers.Count)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        if (targetIndex == sourceIndex)
        {
            RefreshPixelStudioView("Layer order unchanged.");
            return;
        }

        string? originalGroupId = CurrentPixelFrame.Layers[sourceIndex].GroupId;
        bool keepGroupMembership = !string.IsNullOrWhiteSpace(originalGroupId)
            && ShouldRetainLayerGroupAfterMove(sourceIndex, targetIndex, originalGroupId);
        ApplyPixelStudioChange(status, () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                PixelStudioLayerState layer = frame.Layers[sourceIndex];
                frame.Layers.RemoveAt(sourceIndex);
                frame.Layers.Insert(targetIndex, layer);
                if (!keepGroupMembership && !string.IsNullOrWhiteSpace(layer.GroupId))
                {
                    layer.GroupId = null;
                    layer.GroupName = null;
                }
            }

            _pixelStudio.ActiveLayerIndex = targetIndex;
            if (!keepGroupMembership && !string.IsNullOrWhiteSpace(originalGroupId))
            {
                _collapsedLayerGroupIds.Remove(originalGroupId);
            }

            return true;
        });
    }

    private bool ShouldRetainLayerGroupAfterMove(int sourceIndex, int targetIndex, string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId) || CurrentPixelFrame.Layers.Count <= 1)
        {
            return false;
        }

        List<int> remainingIndices = Enumerable.Range(0, CurrentPixelFrame.Layers.Count)
            .Where(index => index != sourceIndex)
            .ToList();
        int insertionIndex = Math.Clamp(targetIndex, 0, remainingIndices.Count);

        bool beforeMatches = insertionIndex > 0
            && string.Equals(CurrentPixelFrame.Layers[remainingIndices[insertionIndex - 1]].GroupId, groupId, StringComparison.Ordinal);
        bool afterMatches = insertionIndex < remainingIndices.Count
            && string.Equals(CurrentPixelFrame.Layers[remainingIndices[insertionIndex]].GroupId, groupId, StringComparison.Ordinal);
        return beforeMatches || afterMatches;
    }

    private void CreateLayerGroup(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count)
        {
            return;
        }

        ClosePixelContextMenu();
        string groupId = $"layer-group-{Guid.NewGuid():N}";
        string groupName = BuildUniqueLayerGroupName("Group");
        ApplyPixelStudioChange($"Created {groupName}.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                frame.Layers[layerIndex].GroupId = groupId;
                frame.Layers[layerIndex].GroupName = groupName;
            }

            if (!string.IsNullOrWhiteSpace(groupId))
            {
                _collapsedLayerGroupIds.Remove(groupId);
            }
            return true;
        });
    }

    private void JoinAdjacentLayerGroup(int layerIndex, int direction)
    {
        if (!TryGetAdjacentLayerGroupId(layerIndex, direction, out string? groupId))
        {
            RefreshPixelStudioView("No adjacent group is available.");
            return;
        }

        ClosePixelContextMenu();
        string groupName = GetLayerGroupDisplayName(groupId) ?? "Group";
        ApplyPixelStudioChange($"Added layer to {groupName}.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                frame.Layers[layerIndex].GroupId = groupId;
                frame.Layers[layerIndex].GroupName = groupName;
            }

            if (!string.IsNullOrWhiteSpace(groupId))
            {
                _collapsedLayerGroupIds.Remove(groupId);
            }
            return true;
        });
    }

    private void RemoveLayerFromGroup(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count)
        {
            return;
        }

        string? groupId = CurrentPixelFrame.Layers[layerIndex].GroupId;
        if (string.IsNullOrWhiteSpace(groupId))
        {
            RefreshPixelStudioView("Layer is not in a group.");
            return;
        }

        ClosePixelContextMenu();
        ApplyPixelStudioChange($"Removed {CurrentPixelFrame.Layers[layerIndex].Name} from its group.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                frame.Layers[layerIndex].GroupId = null;
                frame.Layers[layerIndex].GroupName = null;
            }

            if (!CurrentPixelFrame.Layers.Any(layer => string.Equals(layer.GroupId, groupId, StringComparison.Ordinal)))
            {
                _collapsedLayerGroupIds.Remove(groupId);
            }

            return true;
        });
    }

    private void UngroupLayerGroup(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        ClosePixelContextMenu();
        string groupName = GetLayerGroupDisplayName(groupId) ?? "group";
        ApplyPixelStudioChange($"Ungrouped {groupName}.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                foreach (PixelStudioLayerState layer in frame.Layers.Where(layer => string.Equals(layer.GroupId, groupId, StringComparison.Ordinal)))
                {
                    layer.GroupId = null;
                    layer.GroupName = null;
                }
            }

            _collapsedLayerGroupIds.Remove(groupId);
            return true;
        });
    }

    private void ToggleLayerGroupCollapsed(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        ClosePixelContextMenu();
        if (_collapsedLayerGroupIds.Contains(groupId))
        {
            _collapsedLayerGroupIds.Remove(groupId);
            RefreshPixelStudioView($"Expanded {GetLayerGroupDisplayName(groupId) ?? "group"}.", rebuildLayout: true);
        }
        else
        {
            _collapsedLayerGroupIds.Add(groupId);
            RefreshPixelStudioView($"Collapsed {GetLayerGroupDisplayName(groupId) ?? "group"}.", rebuildLayout: true);
        }
    }

    private bool TryGetAdjacentLayerGroupId(int layerIndex, int direction, out string? groupId)
    {
        groupId = null;
        int adjacentIndex = layerIndex + direction;
        if (adjacentIndex < 0 || adjacentIndex >= CurrentPixelFrame.Layers.Count)
        {
            return false;
        }

        string? adjacentGroupId = CurrentPixelFrame.Layers[adjacentIndex].GroupId;
        if (string.IsNullOrWhiteSpace(adjacentGroupId))
        {
            return false;
        }

        groupId = adjacentGroupId;
        return true;
    }

    private string? GetLayerGroupDisplayName(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return null;
        }

        return CurrentPixelFrame.Layers
            .Where(layer => string.Equals(layer.GroupId, groupId, StringComparison.Ordinal))
            .Select(layer => layer.GroupName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
    }

    private string BuildUniqueLayerGroupName(string baseName, string? existingGroupId = null)
    {
        string requested = SanitizeLayerName(baseName == "Group" ? baseName : baseName);
        requested = string.IsNullOrWhiteSpace(requested) || string.Equals(requested, "Layer", StringComparison.OrdinalIgnoreCase)
            ? "Group"
            : requested;
        HashSet<string> existingNames = CurrentPixelFrame.Layers
            .Where(layer => !string.IsNullOrWhiteSpace(layer.GroupId)
                && !string.Equals(layer.GroupId, existingGroupId, StringComparison.Ordinal))
            .Select(layer => layer.GroupName ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(requested))
        {
            return requested;
        }

        int suffix = 2;
        string candidate = $"{requested} {suffix}";
        while (existingNames.Contains(candidate))
        {
            suffix++;
            candidate = $"{requested} {suffix}";
        }

        return candidate;
    }

    private void PruneCollapsedLayerGroups()
    {
        _collapsedLayerGroupIds.RemoveWhere(groupId => !CurrentPixelFrame.Layers.Any(layer => string.Equals(layer.GroupId, groupId, StringComparison.Ordinal)));
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

    private void TogglePixelLayerAlphaLock(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count)
        {
            return;
        }

        ApplyPixelStudioChange(CurrentPixelFrame.Layers[layerIndex].IsAlphaLocked ? "Alpha lock disabled." : "Alpha lock enabled.", () =>
        {
            bool nextAlphaLock = !CurrentPixelFrame.Layers[layerIndex].IsAlphaLocked;
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                frame.Layers[layerIndex].IsAlphaLocked = nextAlphaLock;
            }

            return true;
        }, rebuildLayout: false);
    }

    private void TogglePixelLayerSharedAcrossFrames(int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count)
        {
            return;
        }

        bool currentlyShared = CurrentPixelFrame.Layers[layerIndex].IsSharedAcrossFrames;
        ApplyPixelStudioChange(
            currentlyShared ? "Layer is now frame-local." : "Layer now shares art across all frames.",
            () =>
            {
                if (currentlyShared)
                {
                    foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
                    {
                        PixelStudioLayerState layer = frame.Layers[layerIndex];
                        layer.IsSharedAcrossFrames = false;
                        layer.Pixels = layer.Pixels.ToArray();
                    }
                }
                else
                {
                    PixelStudioLayerState sourceLayer = CurrentPixelFrame.Layers[layerIndex];
                    int[] sharedPixels = sourceLayer.Pixels;
                    foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
                    {
                        PixelStudioLayerState layer = frame.Layers[layerIndex];
                        layer.IsSharedAcrossFrames = true;
                        layer.Pixels = sharedPixels;
                    }
                }

                return true;
            },
            rebuildLayout: false);
    }

    private void AddPixelFrame()
    {
        ApplyPixelStudioChange("Added a new frame.", () =>
        {
            PixelStudioFrameState newFrame = new()
            {
                Name = $"Frame {_pixelStudio.Frames.Count + 1}",
                DurationMilliseconds = GetDefaultFrameDurationMilliseconds(),
                Layers = CurrentPixelFrame.Layers
                    .Select(layer => new PixelStudioLayerState
                    {
                        Name = layer.Name,
                        GroupId = layer.GroupId,
                        GroupName = layer.GroupName,
                        IsVisible = layer.IsVisible,
                        IsLocked = layer.IsLocked,
                        IsAlphaLocked = layer.IsAlphaLocked,
                        IsSharedAcrossFrames = layer.IsSharedAcrossFrames,
                        Opacity = NormalizeLayerOpacity(layer.Opacity),
                        Pixels = layer.IsSharedAcrossFrames
                            ? layer.Pixels
                            : CreateBlankPixels(_pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight)
                    })
                    .ToList()
            };

            _pixelStudio.Frames.Add(newFrame);
            ShiftLoopRangeForInsertedFrame(_pixelStudio.Frames.Count - 1);
            _pixelStudio.ActiveFrameIndex = _pixelStudio.Frames.Count - 1;
            _pixelStudio.PreviewFrameIndex = _pixelStudio.ActiveFrameIndex;
            return true;
        });
    }

    private void CopyPixelFrame(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _pixelStudio.Frames.Count)
        {
            RefreshPixelStudioView("Select a frame to copy.");
            return;
        }

        _frameClipboard = CloneFrameState(_pixelStudio.Frames[frameIndex]);
        RefreshPixelStudioView($"Copied {_pixelStudio.Frames[frameIndex].Name}.");
    }

    private void PastePixelFrame(int frameIndex)
    {
        if (_frameClipboard is null)
        {
            RefreshPixelStudioView("Copy a frame first.");
            return;
        }

        int insertIndex = Math.Clamp(frameIndex + 1, 0, _pixelStudio.Frames.Count);
        ApplyPixelStudioChange("Pasted frame.", () =>
        {
            PixelStudioFrameState pastedFrame = CloneFrameState(_frameClipboard);
            pastedFrame.Name = BuildUniqueFrameName(string.IsNullOrWhiteSpace(pastedFrame.Name) ? "Frame" : pastedFrame.Name);
            _pixelStudio.Frames.Insert(insertIndex, pastedFrame);
            RelinkSharedLayerReferences(_pixelStudio.Frames);
            ShiftLoopRangeForInsertedFrame(insertIndex);
            _pixelStudio.ActiveFrameIndex = insertIndex;
            _pixelStudio.PreviewFrameIndex = insertIndex;
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
            ShiftLoopRangeForDeletedFrame(frameIndex);
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
            duplicate.Name = BuildUniqueFrameName($"{sourceFrame.Name} Copy");
            _pixelStudio.Frames.Insert(frameIndex + 1, duplicate);
            RelinkSharedLayerReferences(_pixelStudio.Frames);
            ShiftLoopRangeForInsertedFrame(frameIndex + 1);
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

        MovePixelFrameToIndex(frameIndex, targetIndex, "Moved frame.");
    }

    private void MovePixelFrameToIndex(int sourceIndex, int targetIndex, string status)
    {
        if (sourceIndex < 0 || sourceIndex >= _pixelStudio.Frames.Count)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, _pixelStudio.Frames.Count - 1);
        if (targetIndex == sourceIndex)
        {
            RefreshPixelStudioView("Frame order unchanged.");
            return;
        }

        ApplyPixelStudioChange(status, () =>
        {
            PixelStudioFrameState frame = _pixelStudio.Frames[sourceIndex];
            _pixelStudio.Frames.RemoveAt(sourceIndex);
            _pixelStudio.Frames.Insert(targetIndex, frame);
            ShiftLoopRangeForMovedFrame(sourceIndex, targetIndex);
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
        _pixelStudio.PreviewFrameIndex = GetPlaybackStartFrameIndex();
        _lastPlaybackTick = 0;
        _pixelPlaybackDirection = 1;
        RefreshPixelStudioView(_pixelStudio.IsPlaying ? "Animation preview playing." : "Animation preview paused.");
    }

    private void CyclePlaybackLoopMode()
    {
        PixelStudioPlaybackLoopMode nextMode = _pixelStudio.PlaybackLoopMode == PixelStudioPlaybackLoopMode.Forward
            ? PixelStudioPlaybackLoopMode.PingPong
            : PixelStudioPlaybackLoopMode.Forward;

        ApplyPixelStudioChange(
            nextMode == PixelStudioPlaybackLoopMode.PingPong
                ? "Playback mode set to Bounce."
                : "Playback mode set to Forward.",
            () =>
            {
                _pixelStudio.PlaybackLoopMode = nextMode;
                _pixelPlaybackDirection = 1;
                if (_pixelStudio.IsPlaying)
                {
                    _pixelStudio.PreviewFrameIndex = GetPlaybackStartFrameIndex();
                    _lastPlaybackTick = 0;
                }

                return true;
            },
            rebuildLayout: false);
    }

    private void SetGlobalFrameRate(int nextFrameRate)
    {
        int clampedRate = Math.Clamp(nextFrameRate, 1, 24);
        if (clampedRate == _pixelStudio.FramesPerSecond)
        {
            RefreshPixelStudioView($"Animation timing already uses {_pixelStudio.FramesPerSecond} FPS.");
            return;
        }

        int targetDuration = GetFrameDurationFromFramesPerSecond(clampedRate);
        ApplyPixelStudioChange($"Set animation timing to {clampedRate} FPS.", () =>
        {
            _pixelStudio.FramesPerSecond = clampedRate;
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                frame.DurationMilliseconds = targetDuration;
            }

            return true;
        });
    }

    private void AdjustActiveFrameDuration(int deltaMilliseconds)
    {
        if (_pixelStudio.ActiveFrameIndex < 0 || _pixelStudio.ActiveFrameIndex >= _pixelStudio.Frames.Count)
        {
            return;
        }

        int currentDuration = GetFrameDurationMilliseconds(_pixelStudio.ActiveFrameIndex);
        int nextDuration = Math.Clamp(currentDuration + deltaMilliseconds, 40, 1000);
        if (nextDuration == currentDuration)
        {
            RefreshPixelStudioView($"Frame timing is already {currentDuration} ms.");
            return;
        }

        ApplyPixelStudioChange($"Set {CurrentPixelFrame.Name} to {nextDuration} ms.", () =>
        {
            CurrentPixelFrame.DurationMilliseconds = nextDuration;
            return true;
        });
    }

    private void SetLoopBoundary(bool setStartBoundary)
    {
        if (_pixelStudio.Frames.Count == 0)
        {
            RefreshPixelStudioView("Add a frame before setting a loop range.");
            return;
        }

        int activeFrameIndex = Math.Clamp(_pixelStudio.ActiveFrameIndex, 0, _pixelStudio.Frames.Count - 1);
        int lastFrameIndex = _pixelStudio.Frames.Count - 1;
        bool hasLoopRange = TryGetLoopRange(out int loopStartFrameIndex, out int loopEndFrameIndex);
        int currentStart = hasLoopRange ? loopStartFrameIndex : 0;
        int currentEnd = hasLoopRange ? loopEndFrameIndex : lastFrameIndex;
        int nextStart = currentStart;
        int nextEnd = currentEnd;

        if (setStartBoundary)
        {
            if (hasLoopRange && currentStart == activeFrameIndex)
            {
                if (currentStart == currentEnd)
                {
                    nextStart = 0;
                    nextEnd = lastFrameIndex;
                }
                else
                {
                    nextStart = 0;
                }
            }
            else
            {
                nextStart = activeFrameIndex;
            }
        }
        else
        {
            if (hasLoopRange && currentEnd == activeFrameIndex)
            {
                if (currentStart == currentEnd)
                {
                    nextStart = 0;
                    nextEnd = lastFrameIndex;
                }
                else
                {
                    nextEnd = lastFrameIndex;
                }
            }
            else
            {
                nextEnd = activeFrameIndex;
            }
        }

        if (nextStart > nextEnd)
        {
            if (setStartBoundary)
            {
                nextEnd = nextStart;
            }
            else
            {
                nextStart = nextEnd;
            }
        }

        bool nextClearsLoop = nextStart == 0 && nextEnd == lastFrameIndex;
        bool changed =
            nextStart != currentStart
            || nextEnd != currentEnd
            || hasLoopRange == nextClearsLoop;
        if (!changed)
        {
            RefreshPixelStudioView(setStartBoundary
                ? "Loop in is already set there."
                : "Loop out is already set there.");
            return;
        }

        bool clearsLoop = nextClearsLoop;
        string status = clearsLoop
            ? "Loop range cleared."
            : nextStart == nextEnd
                ? $"Looping only frame {nextStart + 1}."
                : $"Loop range set to frames {nextStart + 1}-{nextEnd + 1}.";

        ApplyPixelStudioChange(status, () =>
        {
            _pixelStudio.LoopRangeEnabled = !clearsLoop;
            _pixelStudio.LoopStartFrameIndex = nextStart;
            _pixelStudio.LoopEndFrameIndex = nextEnd;
            NormalizeLoopRangeState();
            if (_pixelStudio.IsPlaying)
            {
                _pixelStudio.PreviewFrameIndex = GetPlaybackStartFrameIndex();
                _lastPlaybackTick = 0;
                _pixelPlaybackDirection = 1;
            }

            return true;
        }, rebuildLayout: false);
    }

    private void ShiftLoopRangeForInsertedFrame(int insertIndex)
    {
        if (_pixelStudio.Frames.Count == 0)
        {
            _pixelStudio.LoopRangeEnabled = false;
            _pixelStudio.LoopStartFrameIndex = 0;
            _pixelStudio.LoopEndFrameIndex = 0;
            return;
        }

        if (!_pixelStudio.LoopRangeEnabled)
        {
            _pixelStudio.LoopStartFrameIndex = 0;
            _pixelStudio.LoopEndFrameIndex = _pixelStudio.Frames.Count - 1;
            return;
        }

        insertIndex = Math.Clamp(insertIndex, 0, _pixelStudio.Frames.Count - 1);
        if (insertIndex <= _pixelStudio.LoopStartFrameIndex)
        {
            _pixelStudio.LoopStartFrameIndex++;
        }

        if (insertIndex <= _pixelStudio.LoopEndFrameIndex)
        {
            _pixelStudio.LoopEndFrameIndex++;
        }

        NormalizeLoopRangeState();
    }

    private void ShiftLoopRangeForDeletedFrame(int deletedIndex)
    {
        if (_pixelStudio.Frames.Count == 0)
        {
            _pixelStudio.LoopRangeEnabled = false;
            _pixelStudio.LoopStartFrameIndex = 0;
            _pixelStudio.LoopEndFrameIndex = 0;
            return;
        }

        if (!_pixelStudio.LoopRangeEnabled)
        {
            _pixelStudio.LoopStartFrameIndex = 0;
            _pixelStudio.LoopEndFrameIndex = _pixelStudio.Frames.Count - 1;
            return;
        }

        int lastFrameIndex = _pixelStudio.Frames.Count - 1;
        _pixelStudio.LoopStartFrameIndex = RemapLoopIndexForDelete(_pixelStudio.LoopStartFrameIndex, deletedIndex, lastFrameIndex);
        _pixelStudio.LoopEndFrameIndex = RemapLoopIndexForDelete(_pixelStudio.LoopEndFrameIndex, deletedIndex, lastFrameIndex);
        NormalizeLoopRangeState();
    }

    private void ShiftLoopRangeForMovedFrame(int sourceIndex, int targetIndex)
    {
        if (!_pixelStudio.LoopRangeEnabled)
        {
            _pixelStudio.LoopStartFrameIndex = 0;
            _pixelStudio.LoopEndFrameIndex = Math.Max(_pixelStudio.Frames.Count - 1, 0);
            return;
        }

        _pixelStudio.LoopStartFrameIndex = RemapLoopIndexForMove(_pixelStudio.LoopStartFrameIndex, sourceIndex, targetIndex);
        _pixelStudio.LoopEndFrameIndex = RemapLoopIndexForMove(_pixelStudio.LoopEndFrameIndex, sourceIndex, targetIndex);
        NormalizeLoopRangeState();
    }

    private static int RemapLoopIndexForDelete(int index, int deletedIndex, int lastFrameIndex)
    {
        if (lastFrameIndex < 0)
        {
            return 0;
        }

        if (index < deletedIndex)
        {
            return Math.Clamp(index, 0, lastFrameIndex);
        }

        if (index > deletedIndex)
        {
            return Math.Clamp(index - 1, 0, lastFrameIndex);
        }

        return Math.Clamp(deletedIndex, 0, lastFrameIndex);
    }

    private static int RemapLoopIndexForMove(int index, int sourceIndex, int targetIndex)
    {
        if (index == sourceIndex)
        {
            return targetIndex;
        }

        if (sourceIndex < targetIndex && index > sourceIndex && index <= targetIndex)
        {
            return index - 1;
        }

        if (sourceIndex > targetIndex && index >= targetIndex && index < sourceIndex)
        {
            return index + 1;
        }

        return index;
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
        _pixelPlaybackDirection = 1;
    }

    private void TogglePixelToolsCollapse()
    {
        _pixelToolsCollapsed = !_pixelToolsCollapsed;
        if (_pixelToolsCollapsed)
        {
            _previousPixelToolsPanelWidth = Math.Max(_pixelToolsPanelWidth, 40);
            _pixelToolsPanelWidth = 34;
            RefreshPixelStudioView("Collapsed tools panel.", rebuildLayout: true);
            return;
        }

        _pixelToolsPanelWidth = Math.Max(_previousPixelToolsPanelWidth, 40);
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
        if (IsCurrentPaletteLocked())
        {
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.ModifyLockedPalette,
                "Palette Is Locked",
                $"{GetLockedPaletteSourceName()} is locked. Unlock the palette, add a new swatch, or cancel.",
                confirmAction: () =>
                {
                    if (!UnlockLockedPaletteSource())
                    {
                        RefreshPixelStudioView("Could not unlock the current palette.", rebuildLayout: true);
                        return;
                    }

                    AddPaletteSwatch();
                });
            return;
        }

        ApplyPixelStudioChange("Added a palette swatch.", () =>
        {
            if (_pixelStudio.Palette.Count >= 24)
            {
                return false;
            }

            ThemeColor current = GetActivePaletteColor();
            _pixelStudio.Palette.Add(current);
            _secondaryPaletteColor = current;
            _secondaryPaletteColorInitialized = true;
            _pixelStudio.ActivePaletteIndex = _pixelStudio.Palette.Count - 1;
            RememberRecentPaletteColor(current);
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
                ClampColorChannel(current.B, deltaB),
                current.A);

            if (!TrySetActivePaletteColor(updated))
            {
                return false;
            }

            RememberRecentPaletteColor(updated);
            return true;
        });
    }

    private void BeginPaletteColorAdjustment()
    {
        EndPixelStroke();
        StopPixelPlayback();
        _paletteColorAdjustSnapshot = CapturePixelStudioState();
        _paletteColorAdjustChanged = false;
        _paletteColorAdjustMode = PaletteColorAdjustMode.None;
    }

    private void UpdatePaletteColorFromField(UiRect fieldRect, float mouseX, float mouseY)
    {
        ThemeColor? updated = CreatePaletteColorFromField(fieldRect, mouseX, mouseY);
        if (updated is null || !TrySetActivePaletteColor(updated.Value))
        {
            return;
        }

        _paletteColorAdjustChanged = true;
        RefreshPixelStudioView(refreshPixelBuffers: true);
    }

    private bool TryBeginPaletteColorWheelAdjustment(UiRect wheelRect, UiRect fieldRect, float mouseX, float mouseY)
    {
        if (fieldRect.Contains(mouseX, mouseY))
        {
            BeginPaletteColorAdjustment();
            _paletteColorAdjustMode = PaletteColorAdjustMode.WheelField;
            UpdatePaletteColorFromWheelField(fieldRect, mouseX, mouseY);
            return true;
        }

        if (TryGetPaletteColorWheelHue(wheelRect, fieldRect, mouseX, mouseY, out float hue))
        {
            BeginPaletteColorAdjustment();
            _paletteColorAdjustMode = PaletteColorAdjustMode.WheelHue;
            UpdatePaletteColorFromWheelHue(hue);
            return true;
        }

        return false;
    }

    private void UpdatePaletteColorPicker(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        switch (_paletteColorAdjustMode)
        {
            case PaletteColorAdjustMode.Field when layout.PaletteColorFieldRect is not null:
                UpdatePaletteColorFromField(layout.PaletteColorFieldRect.Value, mouseX, mouseY);
                break;
            case PaletteColorAdjustMode.WheelField when layout.PaletteColorWheelFieldRect is not null:
                UpdatePaletteColorFromWheelField(layout.PaletteColorWheelFieldRect.Value, mouseX, mouseY);
                break;
            case PaletteColorAdjustMode.WheelHue when layout.PaletteColorWheelRect is not null && layout.PaletteColorWheelFieldRect is not null:
                if (TryGetPaletteColorWheelHue(layout.PaletteColorWheelRect.Value, layout.PaletteColorWheelFieldRect.Value, mouseX, mouseY, out float hue))
                {
                    UpdatePaletteColorFromWheelHue(hue);
                }
                break;
            case PaletteColorAdjustMode.Alpha when layout.PaletteAlphaSliderRect is not null:
                UpdatePaletteAlphaFromSlider(layout.PaletteAlphaSliderRect.Value, mouseX);
                break;
        }
    }

    private void UpdatePaletteColorFromWheelField(UiRect fieldRect, float mouseX, float mouseY)
    {
        ThemeColor? updated = CreatePaletteColorFromWheelField(fieldRect, mouseX, mouseY);
        if (updated is null || !TrySetActivePaletteColor(updated.Value))
        {
            return;
        }

        _paletteColorAdjustChanged = true;
        RefreshPixelStudioView(refreshPixelBuffers: true);
    }

    private void UpdatePaletteColorFromWheelHue(float hue)
    {
        ThemeColor? updated = CreatePaletteColorFromWheelHue(hue);
        if (updated is null || !TrySetActivePaletteColor(updated.Value))
        {
            return;
        }

        _paletteColorAdjustChanged = true;
        RefreshPixelStudioView(refreshPixelBuffers: true);
    }

    private void UpdatePaletteAlphaFromSlider(UiRect sliderRect, float mouseX)
    {
        if (_pixelStudio.Palette.Count == 0 || sliderRect.Width <= 0f)
        {
            return;
        }

        ThemeColor current = GetActivePaletteColor();
        float alpha = Math.Clamp((mouseX - sliderRect.X) / Math.Max(sliderRect.Width, 1f), 0f, 1f);
        ThemeColor updated = new(current.R, current.G, current.B, alpha);
        if (!TrySetActivePaletteColor(updated))
        {
            return;
        }

        _paletteColorAdjustChanged = true;
        RefreshPixelStudioView(refreshPixelBuffers: true);
    }

    private void CommitPaletteColorAdjustment()
    {
        if (_paletteColorAdjustChanged && _paletteColorAdjustSnapshot is not null)
        {
            RememberRecentPaletteColor(GetActivePaletteColor());
            _pixelUndoStack.Push(_paletteColorAdjustSnapshot);
            _pixelRedoStack.Clear();
            MarkPixelStudioRecoveryDirty();
            RefreshPixelStudioView($"Updated active palette color to {ToHex(_pixelStudio.Palette[Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1)])}.", refreshPixelBuffers: true);
        }

        _paletteColorAdjustSnapshot = null;
        _paletteColorAdjustChanged = false;
        _paletteColorAdjustMode = PaletteColorAdjustMode.None;
    }

    private ThemeColor? CreatePaletteColorFromField(UiRect fieldRect, float mouseX, float mouseY)
    {
        if (fieldRect.Width <= 0f || fieldRect.Height <= 0f || _pixelStudio.Palette.Count == 0)
        {
            return null;
        }

        float relativeX = Math.Clamp((mouseX - fieldRect.X) / fieldRect.Width, 0f, 1f);
        float relativeY = Math.Clamp((mouseY - fieldRect.Y) / fieldRect.Height, 0f, 1f);
        float hue = relativeX * 360f;
        float brightness = 1f - relativeY;
        ThemeColor current = _pixelStudio.Palette[Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1)];
        return FromHsv(hue, 1f, brightness, current.A);
    }

    private ThemeColor? CreatePaletteColorFromWheelField(UiRect fieldRect, float mouseX, float mouseY)
    {
        if (fieldRect.Width <= 0f || fieldRect.Height <= 0f || _pixelStudio.Palette.Count == 0)
        {
            return null;
        }

        float relativeX = Math.Clamp((mouseX - fieldRect.X) / fieldRect.Width, 0f, 1f);
        float relativeY = Math.Clamp((mouseY - fieldRect.Y) / fieldRect.Height, 0f, 1f);
        ThemeColor current = _pixelStudio.Palette[Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1)];
        (float hue, _, _) = ToHsv(current);
        float saturation = relativeX;
        float value = 1f - relativeY;
        return FromHsv(hue, saturation, value, current.A);
    }

    private ThemeColor? CreatePaletteColorFromWheelHue(float hue)
    {
        if (_pixelStudio.Palette.Count == 0)
        {
            return null;
        }

        ThemeColor current = _pixelStudio.Palette[Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1)];
        (_, float saturation, float value) = ToHsv(current);
        return FromHsv(hue, saturation, value, current.A);
    }

    private static bool TryGetPaletteColorWheelHue(UiRect wheelRect, UiRect fieldRect, float mouseX, float mouseY, out float hue)
    {
        float centerX = wheelRect.X + (wheelRect.Width * 0.5f);
        float centerY = wheelRect.Y + (wheelRect.Height * 0.5f);
        float deltaX = mouseX - centerX;
        float deltaY = mouseY - centerY;
        float distance = MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        float outerRadius = MathF.Min(wheelRect.Width, wheelRect.Height) * 0.5f;
        float fieldHalfWidth = fieldRect.Width * 0.5f;
        float fieldHalfHeight = fieldRect.Height * 0.5f;
        float innerRadius = MathF.Sqrt((fieldHalfWidth * fieldHalfWidth) + (fieldHalfHeight * fieldHalfHeight));
        if (distance < innerRadius || distance > outerRadius)
        {
            hue = 0f;
            return false;
        }

        hue = MathF.Atan2(deltaY, deltaX) * 180f / MathF.PI;
        if (hue < 0f)
        {
            hue += 360f;
        }

        return true;
    }

    private bool TryGetMirrorAxisHandle(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, out PixelStudioMirrorAxisKind axisKind)
    {
        axisKind = PixelStudioMirrorAxisKind.Vertical;
        if (!layout.CanvasClipRect.Contains(mouseX, mouseY) || _mirrorMode == PixelStudioMirrorMode.Off)
        {
            return false;
        }

        if (_mirrorMode is PixelStudioMirrorMode.Horizontal or PixelStudioMirrorMode.Both)
        {
            float guideX = PixelStudioMirrorAxisMath.GetGuidePosition(layout.CanvasViewportRect.X, layout.CanvasCellSize, GetMirrorAxisX());
            UiRect handleRect = GetMirrorAxisHandleRect(layout, PixelStudioMirrorAxisKind.Vertical, guideX);
            if (handleRect.Contains(mouseX, mouseY))
            {
                axisKind = PixelStudioMirrorAxisKind.Vertical;
                return true;
            }
        }

        if (_mirrorMode is PixelStudioMirrorMode.Vertical or PixelStudioMirrorMode.Both)
        {
            float guideY = PixelStudioMirrorAxisMath.GetGuidePosition(layout.CanvasViewportRect.Y, layout.CanvasCellSize, GetMirrorAxisY());
            UiRect handleRect = GetMirrorAxisHandleRect(layout, PixelStudioMirrorAxisKind.Horizontal, guideY);
            if (handleRect.Contains(mouseX, mouseY))
            {
                axisKind = PixelStudioMirrorAxisKind.Horizontal;
                return true;
            }
        }

        return false;
    }

    private void UpdateMirrorAxisFromMouse(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (_mirrorAxisDragKind == PixelStudioMirrorAxisKind.Vertical)
        {
            _mirrorAxisX = PixelStudioMirrorAxisMath.GetAxisFromMouse(mouseX, layout.CanvasViewportRect.X, layout.CanvasCellSize, _pixelStudio.CanvasWidth);
        }
        else
        {
            _mirrorAxisY = PixelStudioMirrorAxisMath.GetAxisFromMouse(mouseY, layout.CanvasViewportRect.Y, layout.CanvasCellSize, _pixelStudio.CanvasHeight);
        }

        RefreshPixelStudioInteraction();
    }

    private UiRect GetMirrorAxisHandleRect(PixelStudioLayoutSnapshot layout, PixelStudioMirrorAxisKind axisKind, float guidePosition)
    {
        UiRect visibleCanvasRect = PixelStudioMirrorAxisMath.GetVisibleCanvasRect(layout.CanvasViewportRect, layout.CanvasClipRect);
        UiRect handleBounds = visibleCanvasRect.Width > 0f && visibleCanvasRect.Height > 0f
            ? visibleCanvasRect
            : layout.CanvasClipRect;
        UiRect? obstructionRect = PixelStudioMirrorAxisMath.Intersects(handleBounds, layout.ToolSettingsPanelRect)
            ? layout.ToolSettingsPanelRect
            : null;
        return axisKind == PixelStudioMirrorAxisKind.Vertical
            ? PixelStudioMirrorAxisMath.GetVerticalHandleRect(handleBounds, obstructionRect, guidePosition)
            : PixelStudioMirrorAxisMath.GetHorizontalHandleRect(handleBounds, obstructionRect, guidePosition);
    }

    private bool TrySetActivePaletteColor(ThemeColor updated)
    {
        if (_pixelStudio.Palette.Count == 0)
        {
            return false;
        }

        int paletteIndex = Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1);
        if (IsCurrentPaletteLocked())
        {
            PixelStudioState snapshot = _paletteColorAdjustSnapshot is not null
                ? ClonePixelStudioState(_paletteColorAdjustSnapshot)
                : CapturePixelStudioState();
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.ModifyLockedPalette,
                "Palette Is Locked",
                $"{GetLockedPaletteSourceName()} is locked. Unlock the palette, change swatch {paletteIndex + 1}, or cancel.",
                confirmAction: () =>
                {
                    if (!UnlockLockedPaletteSource())
                    {
                        RefreshPixelStudioView("Could not unlock the current palette.", rebuildLayout: true);
                        return;
                    }

                    int unlockedIndex = Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1);
                    ThemeColor currentColor = _pixelStudio.Palette[unlockedIndex];
                    if (currentColor == updated)
                    {
                        RefreshPixelStudioView(rebuildLayout: true);
                        return;
                    }

                    _pixelStudio.Palette[unlockedIndex] = updated;
                    MarkCurrentPaletteAsUnsaved();
                    RememberRecentPaletteColor(updated);
                    _pixelUndoStack.Push(snapshot);
                    _pixelRedoStack.Clear();
                    MarkPixelStudioRecoveryDirty();
                    RefreshPixelStudioView($"Updated active palette color to {ToHex(updated)}.", refreshPixelBuffers: true);
                });
            return false;
        }

        ThemeColor current = _pixelStudio.Palette[paletteIndex];
        if (updated == current)
        {
            return false;
        }

        _pixelStudio.Palette[paletteIndex] = updated;
        MarkCurrentPaletteAsUnsaved();
        return true;
    }

    private bool TryBlockLockedPaletteSwatchEdit(int paletteIndex, string action)
    {
        if (!IsCurrentPaletteLocked())
        {
            return false;
        }

        if (string.Equals(action, "delete", StringComparison.Ordinal))
        {
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.DeleteLockedPaletteSwatch,
                "Palette Swatch Is Locked",
                $"The current palette comes from locked palette {GetLockedPaletteSourceName()}. Unlock the palette, delete swatch {paletteIndex + 1}, or cancel.",
                paletteSwatchIndex: paletteIndex);
            return true;
        }

        OpenPixelWarningDialog(
            PixelStudioWarningDialogKind.ModifyLockedPalette,
            "Palette Is Locked",
            $"{GetLockedPaletteSourceName()} is locked. Unlock the palette, {action} swatch {paletteIndex + 1}, or cancel.");
        return true;
    }

    private bool IsCurrentPaletteLocked()
    {
        return !string.IsNullOrWhiteSpace(_lockedPixelPaletteSourceId);
    }

    private string GetLockedPaletteSourceName()
    {
        if (string.IsNullOrWhiteSpace(_lockedPixelPaletteSourceId))
        {
            return "This palette";
        }

        return _savedPixelPalettes
            .FirstOrDefault(palette => string.Equals(palette.Id, _lockedPixelPaletteSourceId, StringComparison.Ordinal))
            ?.Name ?? "This palette";
    }

    private bool UnlockLockedPaletteSource()
    {
        if (string.IsNullOrWhiteSpace(_lockedPixelPaletteSourceId))
        {
            ClearLockedPaletteProtection();
            return true;
        }

        int paletteIndex = _savedPixelPalettes.FindIndex(palette =>
            string.Equals(palette.Id, _lockedPixelPaletteSourceId, StringComparison.Ordinal));
        if (paletteIndex < 0)
        {
            ClearLockedPaletteProtection();
            return false;
        }

        SavedPixelPalette existing = _savedPixelPalettes[paletteIndex];
        SavedPixelPalette unlocked = new()
        {
            Id = existing.Id,
            Name = existing.Name,
            IsLocked = false,
            Colors = existing.Colors
        };
        ReplaceSavedPalette(unlocked);
        ClearLockedPaletteProtection();
        return true;
    }

    private ThemeColor GetActivePaletteColor()
    {
        if (_pixelStudio.Palette.Count == 0)
        {
            return new ThemeColor(1f, 1f, 1f, 1f);
        }

        return _pixelStudio.Palette[Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1)];
    }

    private void ResetPaletteInteractionState()
    {
        ThemeColor active = GetActivePaletteColor();
        if (!_secondaryPaletteColorInitialized)
        {
            _secondaryPaletteColor = _pixelStudio.Palette.Count > 1
                ? _pixelStudio.Palette[Math.Clamp(_pixelStudio.ActivePaletteIndex == 0 ? 1 : 0, 0, _pixelStudio.Palette.Count - 1)]
                : active;
            _secondaryPaletteColorInitialized = true;
        }
        else if (_pixelStudio.Palette.Count == 0)
        {
            _secondaryPaletteColor = active;
        }

        if (_recentPaletteColors.Count == 0)
        {
            for (int paletteIndex = _pixelStudio.Palette.Count - 1; paletteIndex >= 0; paletteIndex--)
            {
                if (paletteIndex == _pixelStudio.ActivePaletteIndex)
                {
                    continue;
                }

                RememberRecentPaletteColor(_pixelStudio.Palette[paletteIndex]);
            }
        }

        if (_pixelStudio.Palette.Count > 0)
        {
            RememberRecentPaletteColor(active);
        }
    }

    private void RememberRecentPaletteColor(ThemeColor color)
    {
        _recentPaletteColors.RemoveAll(existing => ColorsClose(existing, color, includeAlpha: false));
        _recentPaletteColors.Insert(0, color);
        const int maxRecentColors = 8;
        if (_recentPaletteColors.Count > maxRecentColors)
        {
            _recentPaletteColors.RemoveRange(maxRecentColors, _recentPaletteColors.Count - maxRecentColors);
        }
    }

    private bool TrySelectActivePaletteIndex(int paletteIndex)
    {
        if (_pixelStudio.Palette.Count == 0)
        {
            return false;
        }

        int clampedIndex = Math.Clamp(paletteIndex, 0, _pixelStudio.Palette.Count - 1);
        if (clampedIndex == _pixelStudio.ActivePaletteIndex)
        {
            return false;
        }

        _secondaryPaletteColor = GetActivePaletteColor();
        _secondaryPaletteColorInitialized = true;
        _pixelStudio.ActivePaletteIndex = clampedIndex;
        RememberRecentPaletteColor(GetActivePaletteColor());
        return true;
    }

    private void SwapSecondaryPaletteColor()
    {
        ApplyPixelStudioChange("Activated the secondary color.", () =>
        {
            ThemeColor current = GetActivePaletteColor();
            ThemeColor secondary = _secondaryPaletteColor;
            if (ColorsClose(current, secondary))
            {
                return false;
            }

            _secondaryPaletteColor = current;
            _secondaryPaletteColorInitialized = true;
            return ActivatePaletteColor(secondary) != PaletteColorActivationResult.None;
        }, rebuildLayout: true);
    }

    private void ApplyRecentPaletteColor(int recentIndex)
    {
        if (recentIndex < 0 || recentIndex >= _recentPaletteColors.Count)
        {
            return;
        }

        ThemeColor recentColor = _recentPaletteColors[recentIndex];
        ThemeColor current = GetActivePaletteColor();
        if (ColorsClose(current, recentColor))
        {
            return;
        }

        _secondaryPaletteColor = current;
        _secondaryPaletteColorInitialized = true;
        PaletteColorActivationResult activationResult = ActivatePaletteColor(recentColor);
        switch (activationResult)
        {
            case PaletteColorActivationResult.SelectedExisting:
                RefreshPixelStudioView($"Selected recent color {ToHex(GetActivePaletteColor())}.");
                break;
            case PaletteColorActivationResult.AddedNew:
                RefreshPixelStudioView($"Added recent color {ToHex(recentColor)} as a new swatch.", rebuildLayout: true);
                break;
            case PaletteColorActivationResult.SelectedNearest:
                RefreshPixelStudioView(
                    $"Palette is full, so the closest swatch {ToHex(GetActivePaletteColor())} was selected.");
                break;
        }
    }

    private enum PaletteColorActivationResult
    {
        None,
        SelectedExisting,
        AddedNew,
        SelectedNearest
    }

    private PaletteColorActivationResult ActivatePaletteColor(ThemeColor targetColor)
    {
        if (_pixelStudio.Palette.Count == 0)
        {
            if (IsCurrentPaletteLocked())
            {
                OpenPixelWarningDialog(
                    PixelStudioWarningDialogKind.ModifyLockedPalette,
                    "Palette Is Locked",
                    $"{GetLockedPaletteSourceName()} is locked. Unlock the palette, add {ToHex(targetColor)} as a swatch, or cancel.",
                    confirmAction: () =>
                    {
                        if (!UnlockLockedPaletteSource())
                        {
                            RefreshPixelStudioView("Could not unlock the current palette.", rebuildLayout: true);
                            return;
                        }

                        if (ActivatePaletteColor(targetColor) != PaletteColorActivationResult.None)
                        {
                            RefreshPixelStudioView($"Activated color {ToHex(GetActivePaletteColor())}.", rebuildLayout: true);
                        }
                    });
                return PaletteColorActivationResult.None;
            }

            _pixelStudio.Palette.Add(targetColor);
            _pixelStudio.ActivePaletteIndex = 0;
            RememberRecentPaletteColor(targetColor);
            MarkCurrentPaletteAsUnsaved();
            MarkPixelStudioRecoveryDirty();
            return PaletteColorActivationResult.AddedNew;
        }

        if (TryFindPaletteIndex(targetColor, out int existingPaletteIndex))
        {
            _pixelStudio.ActivePaletteIndex = existingPaletteIndex;
            RememberRecentPaletteColor(_pixelStudio.Palette[existingPaletteIndex]);
            return PaletteColorActivationResult.SelectedExisting;
        }

        if (_pixelStudio.Palette.Count < 24)
        {
            if (IsCurrentPaletteLocked())
            {
                OpenPixelWarningDialog(
                    PixelStudioWarningDialogKind.ModifyLockedPalette,
                    "Palette Is Locked",
                    $"{GetLockedPaletteSourceName()} is locked. Unlock the palette, add {ToHex(targetColor)} as a swatch, or cancel.",
                    confirmAction: () =>
                    {
                        if (!UnlockLockedPaletteSource())
                        {
                            RefreshPixelStudioView("Could not unlock the current palette.", rebuildLayout: true);
                            return;
                        }

                        if (ActivatePaletteColor(targetColor) != PaletteColorActivationResult.None)
                        {
                            RefreshPixelStudioView($"Activated color {ToHex(GetActivePaletteColor())}.", rebuildLayout: true);
                        }
                    });
                return PaletteColorActivationResult.None;
            }

            _pixelStudio.Palette.Add(targetColor);
            _pixelStudio.ActivePaletteIndex = _pixelStudio.Palette.Count - 1;
            RememberRecentPaletteColor(targetColor);
            MarkCurrentPaletteAsUnsaved();
            MarkPixelStudioRecoveryDirty();
            return PaletteColorActivationResult.AddedNew;
        }

        int nearestPaletteIndex = FindNearestPaletteIndex(targetColor, _pixelStudio.Palette);
        _pixelStudio.ActivePaletteIndex = nearestPaletteIndex;
        RememberRecentPaletteColor(_pixelStudio.Palette[nearestPaletteIndex]);
        return PaletteColorActivationResult.SelectedNearest;
    }

    private static bool ColorsClose(ThemeColor left, ThemeColor right, bool includeAlpha = true)
    {
        const float epsilon = 0.0025f;
        return MathF.Abs(left.R - right.R) <= epsilon
            && MathF.Abs(left.G - right.G) <= epsilon
            && MathF.Abs(left.B - right.B) <= epsilon
            && (!includeAlpha || MathF.Abs(left.A - right.A) <= epsilon);
    }

    private void ApplyStoredWorkingPalette(IReadOnlyList<PaletteColorSetting> workingPalette, int activePaletteIndex)
    {
        ReplaceCurrentPaletteColors(workingPalette.Select(ToThemeColor).ToList(), remapArtwork: false);
        _pixelStudio.ActivePaletteIndex = _pixelStudio.Palette.Count == 0
            ? 0
            : Math.Clamp(activePaletteIndex, 0, _pixelStudio.Palette.Count - 1);
    }

    private void ApplyStoredPaletteInteractionState(
        IReadOnlyList<PaletteColorSetting> recentColors,
        PaletteColorSetting? secondaryColor)
    {
        if (secondaryColor is not null)
        {
            _secondaryPaletteColor = ToThemeColor(secondaryColor);
            _secondaryPaletteColorInitialized = true;
        }

        if (recentColors.Count == 0)
        {
            return;
        }

        _recentPaletteColors.Clear();
        foreach (PaletteColorSetting recentColor in recentColors)
        {
            RememberRecentPaletteColor(ToThemeColor(recentColor));
        }
    }

    private bool TryFindPaletteIndex(ThemeColor color, out int paletteIndex, bool includeAlpha = true)
    {
        for (int index = 0; index < _pixelStudio.Palette.Count; index++)
        {
            if (ColorsClose(_pixelStudio.Palette[index], color, includeAlpha))
            {
                paletteIndex = index;
                return true;
            }
        }

        paletteIndex = -1;
        return false;
    }

    private void UndoPixelStudio()
    {
        EndPixelStroke();
        if (_pixelUndoStack.Count == 0)
        {
            RefreshPixelStudioView("Nothing to undo.");
            return;
        }

        _pixelRedoStack.Push(CapturePixelStudioState());
        RestorePixelStudioState(_pixelUndoStack.Pop());
        RefreshPixelStudioView("Undid last pixel change.", rebuildLayout: true, refreshPixelBuffers: true);
    }

    private void RedoPixelStudio()
    {
        EndPixelStroke();
        if (_pixelRedoStack.Count == 0)
        {
            RefreshPixelStudioView("Nothing to redo.");
            return;
        }

        _pixelUndoStack.Push(CapturePixelStudioState());
        RestorePixelStudioState(_pixelRedoStack.Pop());
        RefreshPixelStudioView("Redid pixel change.", rebuildLayout: true, refreshPixelBuffers: true);
    }

    private void ApplyPixelStudioChange(string status, Func<bool> mutation, bool rebuildLayout = true, bool playWarningTone = false)
    {
        EndPixelStroke();
        StopPixelPlayback();
        PixelStudioState snapshot = CapturePixelStudioState();
        if (!mutation())
        {
            return;
        }

        EnsurePixelStudioIndices();
        _pixelUndoStack.Push(snapshot);
        _pixelRedoStack.Clear();
        MarkPixelStudioRecoveryDirty();
        RefreshPixelStudioView(status, rebuildLayout, refreshPixelBuffers: true, playWarningTone: playWarningTone);
    }

    private void RefreshPixelStudioView(string? overrideStatus = null, bool rebuildLayout = false, bool refreshPixelBuffers = false, bool playWarningTone = false)
    {
        EnsurePixelStudioIndices();
        _uiState.PixelStudio = BuildPixelStudioViewState(refreshPixelBuffers);
        if (overrideStatus is not null)
        {
            if (playWarningTone)
            {
                NotificationSoundPlayer.PlayWarning();
            }

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

    private void MarkPixelStudioRecoveryDirty()
    {
        _pixelStudioAutosavePending = true;
        _pixelStudioLastMutationAt = DateTimeOffset.UtcNow;
        _pixelStudioAutosaveIndicatorUntil = DateTimeOffset.MinValue;
    }

    private void ResetPixelStudioRecoveryTracking(bool useCurrentAsSavedBaseline, bool useCurrentAsAutosavedBaseline)
    {
        string currentJson = SerializeCurrentPixelStudioSnapshot();
        _pixelStudioLastSavedSnapshotJson = useCurrentAsSavedBaseline ? currentJson : null;
        _pixelStudioLastAutosavedSnapshotJson = useCurrentAsAutosavedBaseline ? currentJson : null;
        _pixelStudioAutosavePending = false;
        _pixelStudioLastMutationAt = DateTimeOffset.MinValue;
        _pixelStudioLastAutosaveAt = DateTimeOffset.UtcNow;
        _pixelStudioAutosaveIndicatorUntil = DateTimeOffset.MinValue;
        _pixelRecoveryBackupCount = PixelStudioRecoveryManager.GetBackupCount();
    }

    private int GetPixelStudioAutosaveCountdownSeconds()
    {
        if (!_pixelStudioAutosavePending || _pixelAutosaveIntervalSeconds <= 0 || _pixelStudioLastMutationAt <= DateTimeOffset.MinValue)
        {
            return 0;
        }

        TimeSpan autosaveDelay = TimeSpan.FromSeconds(Math.Max(PixelStudioAutosaveIdleDelay.TotalSeconds, _pixelAutosaveIntervalSeconds));
        DateTimeOffset nextAutosaveAt = _pixelStudioLastMutationAt + autosaveDelay;
        double remainingSeconds = (nextAutosaveAt - DateTimeOffset.UtcNow).TotalSeconds;
        return remainingSeconds <= 0d ? 0 : Math.Max((int)Math.Ceiling(remainingSeconds), 0);
    }

    private string BuildPixelStudioAutosaveBannerText()
    {
        if (_pixelAutosaveIntervalSeconds <= 0)
        {
            return string.Empty;
        }

        if (_pixelStudioAutosavePending)
        {
            int countdownSeconds = GetPixelStudioAutosaveCountdownSeconds();
            string timerText = countdownSeconds > 0
                ? $"Autosave in {countdownSeconds}s."
                : "Autosave pending.";
            return _pixelRecoveryBackupCount > 0
                ? $"{timerText} Recovery backups kept: {_pixelRecoveryBackupCount}."
                : timerText;
        }

        if (_pixelRecoveryOwnedByCurrentSession && _pixelStudioAutosaveIndicatorUntil > DateTimeOffset.UtcNow)
        {
            return _pixelRecoveryBackupCount > 0
                ? $"Recovery autosaved. Backups kept: {_pixelRecoveryBackupCount}."
                : "Recovery autosaved.";
        }

        return string.Empty;
    }

    private void UpdatePixelStudioAutosave()
    {
        if (!_pixelStudioAutosavePending || _pixelAutosaveIntervalSeconds <= 0)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan autosaveDelay = TimeSpan.FromSeconds(Math.Max(PixelStudioAutosaveIdleDelay.TotalSeconds, _pixelAutosaveIntervalSeconds));
        if (now - _pixelStudioLastMutationAt < autosaveDelay)
        {
            return;
        }

        TryFlushPixelStudioRecoveryNow();
        RefreshPixelStudioView();
    }

    private bool TryFlushPixelStudioRecoveryNow()
    {
        if (!_pixelStudioAutosavePending && !_pixelRecoveryOwnedByCurrentSession)
        {
            return false;
        }

        try
        {
            string currentJson = SerializeCurrentPixelStudioSnapshot();
            if (!string.IsNullOrWhiteSpace(_pixelStudioLastSavedSnapshotJson)
                && string.Equals(currentJson, _pixelStudioLastSavedSnapshotJson, StringComparison.Ordinal))
            {
                PixelStudioRecoveryManager.Clear();
                _pixelStudioLastAutosavedSnapshotJson = null;
                _pixelStudioAutosavePending = false;
                _pixelRecoveryOwnedByCurrentSession = false;
                _pixelStudioLastAutosaveAt = DateTimeOffset.UtcNow;
                _pixelStudioAutosaveIndicatorUntil = DateTimeOffset.MinValue;
                _pixelRecoveryBackupCount = PixelStudioRecoveryManager.GetBackupCount();
                return false;
            }

            if (!_pixelStudioAutosavePending
                && string.Equals(currentJson, _pixelStudioLastAutosavedSnapshotJson, StringComparison.Ordinal))
            {
                return true;
            }

            PixelStudioRecoveryManager.Save(new PixelStudioRecoverySnapshot
            {
                DocumentPath = _currentPixelDocumentPath,
                ProjectPath = _lastProjectPath,
                SavedAtUtc = DateTimeOffset.UtcNow,
                Document = CreateProjectDocumentSnapshot()
            });

            _pixelStudioLastAutosavedSnapshotJson = currentJson;
            _pixelStudioAutosavePending = false;
            _pixelRecoveryOwnedByCurrentSession = true;
            _pixelStudioLastAutosaveAt = DateTimeOffset.UtcNow;
            _pixelRecoveryBackupCount = PixelStudioRecoveryManager.GetBackupCount();
            if (_pixelAutosaveIntervalSeconds > 0)
            {
                _pixelStudioAutosaveIndicatorUntil = DateTimeOffset.UtcNow.AddSeconds(2.4);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void FinalizePixelStudioRecoveryOnCleanExit()
    {
        if (_pixelStudioAutosavePending || (_pixelRecoveryOwnedByCurrentSession && HasPixelStudioUnsavedChanges()))
        {
            TryFlushPixelStudioRecoveryNow();
            return;
        }

        if (_preserveDeferredRecoveryOnCleanExit && !_pixelRecoveryOwnedByCurrentSession)
        {
            return;
        }

        PixelStudioRecoveryManager.Clear();
        _pixelStudioLastAutosavedSnapshotJson = null;
        _pixelRecoveryOwnedByCurrentSession = false;
        _pixelRecoveryBackupCount = PixelStudioRecoveryManager.GetBackupCount();
    }

    private bool HasPixelStudioUnsavedChanges()
    {
        if (string.IsNullOrWhiteSpace(_pixelStudioLastSavedSnapshotJson))
        {
            return _pixelRecoveryOwnedByCurrentSession || _pixelStudioAutosavePending;
        }

        return !string.Equals(SerializeCurrentPixelStudioSnapshot(), _pixelStudioLastSavedSnapshotJson, StringComparison.Ordinal);
    }

    private string SerializeCurrentPixelStudioSnapshot()
    {
        return JsonSerializer.Serialize(CreateProjectDocumentSnapshot(), PixelStudioDocumentSerializerOptions);
    }

    private void RestorePixelStudioRecovery(PixelStudioRecoverySnapshot snapshot)
    {
        ReplacePixelStudioDocument(CreatePixelStudioState(snapshot.Document));
        _currentPixelDocumentPath = string.IsNullOrWhiteSpace(snapshot.DocumentPath)
            ? null
            : snapshot.DocumentPath;
        if (!string.IsNullOrWhiteSpace(snapshot.ProjectPath))
        {
            _lastProjectPath = snapshot.ProjectPath;
        }

        _pixelStudio.DocumentName = string.IsNullOrWhiteSpace(snapshot.Document.DocumentName)
            ? (!string.IsNullOrWhiteSpace(_currentPixelDocumentPath)
                ? Path.GetFileNameWithoutExtension(_currentPixelDocumentPath)
                : "Recovered Sprite")
            : snapshot.Document.DocumentName;
        _pixelUndoStack.Clear();
        _pixelRedoStack.Clear();
        _pixelRecoveryOwnedByCurrentSession = true;
        _pixelRecoveryBannerVisible = true;
        _pixelRecoveryBackupCount = PixelStudioRecoveryManager.GetBackupCount();
        ResetPixelStudioRecoveryTracking(useCurrentAsSavedBaseline: false, useCurrentAsAutosavedBaseline: true);
        RefreshPixelStudioView(BuildPixelStudioRecoveryStatusMessage(), rebuildLayout: true, refreshPixelBuffers: true);
    }

    private void SyncPixelStudioCameraUiState()
    {
        _uiState.PixelStudio.Zoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom);
        _uiState.PixelStudio.CanvasPanX = _pixelStudio.CanvasPanX;
        _uiState.PixelStudio.CanvasPanY = _pixelStudio.CanvasPanY;
    }

    private void RefreshPixelStudioCameraLayout(string? overrideStatus = null)
    {
        EnsurePixelStudioIndices();
        SyncPixelStudioCameraUiState();
        if (overrideStatus is not null)
        {
            _uiState.StatusText = overrideStatus;
            _shell.SetStatus(_uiState.StatusText);
        }

        if (_width > 0 && _height > 0)
        {
            _layoutSnapshot = EditorLayoutEngine.Create(_width, _height, _shell.Layout, _uiState);
            SyncCanvasCameraFromLayout(overrideStatus ?? "CameraRefresh");
            _renderer?.UpdateLayoutSnapshot(_layoutSnapshot);
        }

        _renderer?.UpdateUiState(_uiState);
    }

    private void RefreshPixelStudioCameraStatus(string status)
    {
        SyncPixelStudioCameraUiState();
        _uiState.StatusText = status;
        _shell.SetStatus(_uiState.StatusText);
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

    private void UpdateHoveredPixelTool(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        string hoveredTitle = string.Empty;
        string hoveredBody = string.Empty;
        if (_pixelDragMode == PixelStudioDragMode.None)
        {
            ResolveHoveredPixelHelp(layout, mouseX, mouseY, out hoveredTitle, out hoveredBody);
        }

        if (string.Equals(_hoveredPixelHelpTitle, hoveredTitle, StringComparison.Ordinal)
            && string.Equals(_hoveredPixelHelpBody, hoveredBody, StringComparison.Ordinal))
        {
            return;
        }

        _hoveredPixelHelpTitle = hoveredTitle;
        _hoveredPixelHelpBody = hoveredBody;
        _hoveredPixelHelpStartTick = string.IsNullOrWhiteSpace(hoveredTitle) ? 0L : Stopwatch.GetTimestamp();
        _hoveredPixelToolTooltipVisible = false;
        SyncPixelStudioHoverUiState();
    }

    private void SyncPixelStudioHoverUiState()
    {
        if (_uiState.PixelStudio is null)
        {
            return;
        }

        bool tooltipVisible = false;
        if (!string.IsNullOrWhiteSpace(_hoveredPixelHelpTitle) && _pixelDragMode == PixelStudioDragMode.None)
        {
            double elapsedMilliseconds = (Stopwatch.GetTimestamp() - _hoveredPixelHelpStartTick) * 1000.0 / Stopwatch.Frequency;
            tooltipVisible = elapsedMilliseconds >= 520.0;
        }

        _hoveredPixelToolTooltipVisible = tooltipVisible;
        _uiState.PixelStudio.HoverTooltipVisible = tooltipVisible && !string.IsNullOrWhiteSpace(_hoveredPixelHelpTitle);
        _uiState.PixelStudio.HoverTooltipTitle = _hoveredPixelHelpTitle;
        _uiState.PixelStudio.HoverTooltipBody = _hoveredPixelHelpBody;
        _uiState.PixelStudio.HoverTooltipX = _pixelMouseX + 14f;
        _uiState.PixelStudio.HoverTooltipY = _pixelMouseY + 18f;
    }

    private void ResolveHoveredPixelHelp(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, out string title, out string body)
    {
        title = string.Empty;
        body = string.Empty;

        ActionRect<PixelStudioToolKind>? hoveredToolButton = layout.ToolButtons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (hoveredToolButton is not null)
        {
            title = GetPixelToolDisplayLabel(hoveredToolButton.Action);
            body = GetPixelToolTooltipBody(hoveredToolButton.Action);
            return;
        }

        if (TryResolveHoveredPixelActionHelp(layout.DocumentButtons, mouseX, mouseY, out title, out body)
            || TryResolveHoveredPixelActionHelp(layout.CanvasButtons, mouseX, mouseY, out title, out body)
            || TryResolveHoveredPixelActionHelp(layout.SelectionButtons, mouseX, mouseY, out title, out body)
            || TryResolveHoveredPixelActionHelp(layout.ToolSettingsButtons, mouseX, mouseY, out title, out body)
            || TryResolveHoveredPixelActionHelp(layout.PaletteButtons, mouseX, mouseY, out title, out body)
            || TryResolveHoveredPixelActionHelp(layout.PaletteLibraryButtons, mouseX, mouseY, out title, out body)
            || TryResolveHoveredPixelActionHelp(layout.PalettePromptButtons, mouseX, mouseY, out title, out body)
            || TryResolveHoveredPixelActionHelp(layout.LayerButtons, mouseX, mouseY, out title, out body)
            || TryResolveHoveredPixelActionHelp(layout.TimelineButtons, mouseX, mouseY, out title, out body))
        {
            return;
        }

        if (layout.FrameDurationFieldRect is not null && layout.FrameDurationFieldRect.Value.Contains(mouseX, mouseY))
        {
            title = "Frame Duration";
            body = "Type the active frame timing in milliseconds. Enter applies it.";
            return;
        }

        if (layout.OnionOpacitySliderRect is not null && layout.OnionOpacitySliderRect.Value.Contains(mouseX, mouseY))
        {
            title = "Onion Opacity";
            body = "Adjust how strongly previous or next ghost frames show while animating.";
            return;
        }

        if (layout.LayerOpacitySliderRect is not null && layout.LayerOpacitySliderRect.Value.Contains(mouseX, mouseY))
        {
            title = "Layer Opacity";
            body = "Adjust the active layer opacity without changing its painted pixels.";
            return;
        }

        if (layout.PaletteAlphaSliderRect is not null && layout.PaletteAlphaSliderRect.Value.Contains(mouseX, mouseY))
        {
            title = "Color Alpha";
            body = "Set the opacity of the active color before painting.";
            return;
        }

        if (layout.PaletteColorWheelRect is not null && layout.PaletteColorWheelRect.Value.Contains(mouseX, mouseY))
        {
            title = "Color Wheel";
            body = "Pick hue from the ring, then refine saturation and value in the center field.";
            return;
        }

        if (layout.PaletteColorFieldRect is not null && layout.PaletteColorFieldRect.Value.Contains(mouseX, mouseY))
        {
            title = "Color Field";
            body = "Drag to choose the active drawing color from the current field.";
            return;
        }

        IndexedRect? paletteSwatch = layout.PaletteSwatches.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (paletteSwatch is not null)
        {
            title = $"Palette Color {paletteSwatch.Index + 1}";
            body = "Click to make this your active drawing swatch. Right-click to delete the swatch from the palette.";
            return;
        }

        IndexedRect? recentColor = layout.RecentColorSwatches.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (recentColor is not null)
        {
            title = "Recent Color";
            body = "Reuse a recently used color without replacing your current palette setup.";
            return;
        }

        IndexedRect? savedPaletteRow = layout.SavedPaletteRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (savedPaletteRow is not null)
        {
            title = savedPaletteRow.Index < 0 ? "Default Palette" : "Saved Palette";
            body = savedPaletteRow.Index >= 0
                && savedPaletteRow.Index < _uiState.PixelStudio.SavedPalettes.Count
                && _uiState.PixelStudio.SavedPalettes[savedPaletteRow.Index].IsLocked
                    ? "Click to switch to this locked palette. Palette edits are blocked until you unlock it. Right-click it for palette actions."
                    : "Click to switch to this palette. Right-click it for palette actions.";
            return;
        }

        PixelStudioLayerGroupHeaderRect? groupRow = layout.LayerGroupRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (groupRow is not null)
        {
            title = string.IsNullOrWhiteSpace(groupRow.GroupName) ? "Layer Group" : groupRow.GroupName;
            body = "Click to collapse or expand this group. Right-click for group actions.";
            return;
        }

        IndexedRect? layerRow = layout.LayerRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (layerRow is not null && layerRow.Index >= 0 && layerRow.Index < CurrentPixelFrame.Layers.Count)
        {
            PixelStudioLayerState layer = CurrentPixelFrame.Layers[layerRow.Index];
            title = layer.Name;
            body = "Click to select this layer. Drag to reorder, or drop onto a group to join it.";
            return;
        }

        IndexedRect? frameRow = layout.FrameRows.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (frameRow is not null && frameRow.Index >= 0 && frameRow.Index < _pixelStudio.Frames.Count)
        {
            PixelStudioFrameState frame = _pixelStudio.Frames[frameRow.Index];
            title = frame.Name;
            body = "Click to select this frame. Drag to reorder, or right-click for frame actions.";
            return;
        }
    }

    private bool TryResolveHoveredPixelActionHelp(IReadOnlyList<ActionRect<PixelStudioAction>> buttons, float mouseX, float mouseY, out string title, out string body)
    {
        ActionRect<PixelStudioAction>? hoveredButton = buttons.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (hoveredButton is null)
        {
            title = string.Empty;
            body = string.Empty;
            return false;
        }

        title = GetPixelStudioActionDisplayLabel(hoveredButton.Action);
        body = GetPixelStudioActionTooltipBody(hoveredButton.Action);
        return true;
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

        SyncPixelStudioCameraUiState();
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
            ThemeStudioDialogRect = _layoutSnapshot.ThemeStudioDialogRect,
            ThemeStudioNameFieldRect = _layoutSnapshot.ThemeStudioNameFieldRect,
            ThemeStudioWheelRect = _layoutSnapshot.ThemeStudioWheelRect,
            ThemeStudioWheelFieldRect = _layoutSnapshot.ThemeStudioWheelFieldRect,
            ThemeStudioPreviewRect = _layoutSnapshot.ThemeStudioPreviewRect,
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
            ThemeStudioButtons = _layoutSnapshot.ThemeStudioButtons,
            ThemeStudioRoleButtons = _layoutSnapshot.ThemeStudioRoleButtons,
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

    private void SyncSelectionTransformUiState()
    {
        if (_uiState.PixelStudio is null)
        {
            return;
        }

        _uiState.PixelStudio.HasSelection = _selectionActive;
        _uiState.PixelStudio.SelectionTransformModeActive = _selectionTransformModeActive;
        _uiState.PixelStudio.SelectionX = Math.Min(_selectionStartX, _selectionEndX);
        _uiState.PixelStudio.SelectionY = Math.Min(_selectionStartY, _selectionEndY);
        _uiState.PixelStudio.SelectionWidth = _selectionActive ? GetSelectionWidth() : 0;
        _uiState.PixelStudio.SelectionHeight = _selectionActive ? GetSelectionHeight() : 0;
        _uiState.PixelStudio.SelectionUsesMask = SelectionUsesMask();
        _uiState.PixelStudio.SelectionMaskIndices = SelectionUsesMask() ? _selectionMask : new HashSet<int>();
        _uiState.PixelStudio.SelectionTransformPreviewVisible = _selectionTransformPreviewVisible;
        _uiState.PixelStudio.SelectionTransformPreviewX = _selectionTransformPreviewLeft;
        _uiState.PixelStudio.SelectionTransformPreviewY = _selectionTransformPreviewTop;
        _uiState.PixelStudio.SelectionTransformPreviewWidth = _selectionTransformPreviewWidth;
        _uiState.PixelStudio.SelectionTransformPreviewHeight = _selectionTransformPreviewHeight;
        _uiState.PixelStudio.SelectionTransformPreviewRotationDegrees = _selectionTransformPreviewRotationDegrees;
        _uiState.PixelStudio.SelectionTransformPreviewScaleX = GetSelectionTransformPreviewScaleX();
        _uiState.PixelStudio.SelectionTransformPreviewScaleY = GetSelectionTransformPreviewScaleY();
        _uiState.PixelStudio.SelectionTransformPivotX = float.IsFinite(_selectionTransformPivotX)
            ? _selectionTransformPivotX
            : _uiState.PixelStudio.SelectionX + (_uiState.PixelStudio.SelectionWidth * 0.5f);
        _uiState.PixelStudio.SelectionTransformPivotY = float.IsFinite(_selectionTransformPivotY)
            ? _selectionTransformPivotY
            : _uiState.PixelStudio.SelectionY + (_uiState.PixelStudio.SelectionHeight * 0.5f);
        _uiState.PixelStudio.SelectionTransformAngleFieldActive = _selectionTransformAngleFieldActive;
        _uiState.PixelStudio.SelectionTransformAngleBuffer = _selectionTransformAngleBuffer;
        _uiState.PixelStudio.SelectionTransformScaleXFieldActive = _selectionTransformScaleXFieldActive;
        _uiState.PixelStudio.SelectionTransformScaleYFieldActive = _selectionTransformScaleYFieldActive;
        _uiState.PixelStudio.SelectionTransformScaleXBuffer = _selectionTransformScaleXBuffer;
        _uiState.PixelStudio.SelectionTransformScaleYBuffer = _selectionTransformScaleYBuffer;
    }

    private void RefreshSelectionTransformOverlay(PixelStudioLayoutSnapshot? layout = null)
    {
        EnsurePixelStudioIndices();
        if (_uiState.PixelStudio is null)
        {
            return;
        }

        SyncSelectionTransformUiState();
        PixelStudioLayoutSnapshot? currentLayout = layout ?? _layoutSnapshot?.PixelStudio;
        if (currentLayout is not null)
        {
            EditorLayoutEngine.BuildSelectionTransformOverlay(
                _uiState.PixelStudio,
                currentLayout.CanvasViewportRect,
                currentLayout.CanvasCellSize,
                out UiRect? previewRect,
                out UiRect? angleFieldRect,
                out UiRect? scaleXFieldRect,
                out UiRect? scaleYFieldRect,
                out List<PixelStudioSelectionHandleRect> handleRects,
                out List<ActionRect<PixelStudioResizeAnchor>> pivotPresetButtons);
            ReplacePixelStudioLayoutSnapshot(currentLayout.WithSelectionTransformOverlay(previewRect, angleFieldRect, scaleXFieldRect, scaleYFieldRect, handleRects, pivotPresetButtons));
        }

        _renderer?.UpdateUiState(_uiState);
    }

    private static float NormalizeTransformRotationDegrees(float degrees)
    {
        float normalized = degrees % 360f;
        if (normalized > 180f)
        {
            normalized -= 360f;
        }
        else if (normalized <= -180f)
        {
            normalized += 360f;
        }

        return normalized;
    }

    private float SnapSelectionTransformRotationDegrees(float degrees)
    {
        float normalized = NormalizeTransformRotationDegrees(degrees);
        if (!_shiftPressed)
        {
            return normalized;
        }

        int snap = Math.Max(_transformRotationSnapDegrees, 1);
        return NormalizeTransformRotationDegrees(MathF.Round(normalized / snap) * snap);
    }

    private static void RotateClockwise(float x, float y, float centerX, float centerY, float cos, float sin, out float rotatedX, out float rotatedY)
    {
        float dx = x - centerX;
        float dy = y - centerY;
        rotatedX = centerX + (dx * cos) - (dy * sin);
        rotatedY = centerY + (dx * sin) + (dy * cos);
    }

    private static void InverseRotateClockwise(float x, float y, float centerX, float centerY, float cos, float sin, out float rotatedX, out float rotatedY)
    {
        float dx = x - centerX;
        float dy = y - centerY;
        rotatedX = centerX + (dx * cos) + (dy * sin);
        rotatedY = centerY - (dx * sin) + (dy * cos);
    }

    private static void ComputeTransformedSelectionTargetBounds(
        int sourceLeft,
        int sourceTop,
        int sourceWidth,
        int sourceHeight,
        float pivotX,
        float pivotY,
        float scaleX,
        float scaleY,
        float angleDegrees,
        out int targetLeft,
        out int targetTop,
        out int targetWidth,
        out int targetHeight)
    {
        float radians = angleDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        float[] cornerXs =
        [
            sourceLeft,
            sourceLeft + sourceWidth,
            sourceLeft + sourceWidth,
            sourceLeft
        ];
        float[] cornerYs =
        [
            sourceTop,
            sourceTop,
            sourceTop + sourceHeight,
            sourceTop + sourceHeight
        ];

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        for (int index = 0; index < 4; index++)
        {
            float scaledX = pivotX + ((cornerXs[index] - pivotX) * scaleX);
            float scaledY = pivotY + ((cornerYs[index] - pivotY) * scaleY);
            RotateClockwise(scaledX, scaledY, pivotX, pivotY, cos, sin, out float rotatedX, out float rotatedY);
            minX = MathF.Min(minX, rotatedX);
            minY = MathF.Min(minY, rotatedY);
            maxX = MathF.Max(maxX, rotatedX);
            maxY = MathF.Max(maxY, rotatedY);
        }

        targetLeft = (int)MathF.Floor(minX);
        targetTop = (int)MathF.Floor(minY);
        targetWidth = Math.Max((int)MathF.Ceiling(maxX) - targetLeft, 1);
        targetHeight = Math.Max((int)MathF.Ceiling(maxY) - targetTop, 1);
    }

    private bool TryGetCanvasPoint(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, out float canvasX, out float canvasY)
    {
        canvasX = 0f;
        canvasY = 0f;
        if (layout.CanvasCellSize <= 0)
        {
            return false;
        }

        canvasX = (mouseX - layout.CanvasViewportRect.X) / layout.CanvasCellSize;
        canvasY = (mouseY - layout.CanvasViewportRect.Y) / layout.CanvasCellSize;
        return float.IsFinite(canvasX) && float.IsFinite(canvasY);
    }

    private void SetSelectionTransformPreviewState(int left, int top, int width, int height, float rotationDegrees, float scaleX, float scaleY)
    {
        _selectionTransformPreviewLeft = left;
        _selectionTransformPreviewTop = top;
        _selectionTransformPreviewWidth = width;
        _selectionTransformPreviewHeight = height;
        _selectionTransformPreviewRotationDegrees = rotationDegrees;
        _selectionTransformPreviewScaleX = scaleX;
        _selectionTransformPreviewScaleY = scaleY;
    }

    private void RefreshPixelStudioPixels(IReadOnlyCollection<int>? dirtyPixelIndices = null, bool refreshPreviewFrame = false)
    {
        EnsurePixelStudioIndices();
        int pixelCount = _pixelStudio.CanvasWidth * _pixelStudio.CanvasHeight;
        EnsurePixelStudioPixelBuffers();
        bool sharedLayerAffected = CurrentPixelFrame.Layers.Count > 0
            && _pixelStudio.ActiveLayerIndex >= 0
            && _pixelStudio.ActiveLayerIndex < CurrentPixelFrame.Layers.Count
            && CurrentPixelFrame.Layers[_pixelStudio.ActiveLayerIndex].IsSharedAcrossFrames;

        bool canPatchCurrent =
            dirtyPixelIndices is not null &&
            dirtyPixelIndices.Count > 0 &&
            _pixelCompositePixels.Count == pixelCount &&
            _pixelCompositeFrameIndex == _pixelStudio.ActiveFrameIndex;

        if (canPatchCurrent)
        {
            ApplyCompositePixelChanges(CurrentPixelFrame, _pixelCompositePixels, dirtyPixelIndices!);
        }
        else
        {
            _pixelCompositePixels = ComposeVisiblePixels(CurrentPixelFrame);
        }

        _pixelCompositeFrameIndex = _pixelStudio.ActiveFrameIndex;
        _pixelCompositeRevision++;
        RefreshOnionSkinBuffers(forceFullRefresh: sharedLayerAffected);

        if (_pixelStudio.PreviewFrameIndex == _pixelStudio.ActiveFrameIndex)
        {
            _pixelPreviewPixels = _pixelCompositePixels;
            _pixelPreviewFrameIndex = _pixelCompositeFrameIndex;
            _pixelPreviewRevision = _pixelCompositeRevision;
        }
        else if (sharedLayerAffected || refreshPreviewFrame || _pixelPreviewPixels.Count != pixelCount || _pixelPreviewFrameIndex != _pixelStudio.PreviewFrameIndex)
        {
            _pixelPreviewPixels = ComposeVisiblePixels(PreviewPixelFrame);
            _pixelPreviewFrameIndex = _pixelStudio.PreviewFrameIndex;
            _pixelPreviewRevision++;
        }

        _uiState.PixelStudio.CompositePixels = _pixelCompositePixels;
        _uiState.PixelStudio.OnionPreviousPixels = _pixelOnionPreviousPixels;
        _uiState.PixelStudio.OnionNextPixels = _pixelOnionNextPixels;
        _uiState.PixelStudio.PreviewPixels = _pixelPreviewPixels;
        _uiState.PixelStudio.CompositePixelsRevision = _pixelCompositeRevision;
        _uiState.PixelStudio.OnionPreviousPixelsRevision = _pixelOnionPreviousRevision;
        _uiState.PixelStudio.OnionNextPixelsRevision = _pixelOnionNextRevision;
        _uiState.PixelStudio.PreviewPixelsRevision = _pixelPreviewRevision;
        _renderer?.UpdateUiState(_uiState);
    }

    private void EnsurePixelStudioPixelBuffers(bool forceFullRefresh = false)
    {
        int pixelCount = _pixelStudio.CanvasWidth * _pixelStudio.CanvasHeight;
        bool compositeInvalid = forceFullRefresh
            || _pixelCompositePixels.Count != pixelCount
            || _pixelCompositeFrameIndex != _pixelStudio.ActiveFrameIndex;
        if (compositeInvalid)
        {
            _pixelCompositePixels = ComposeVisiblePixels(CurrentPixelFrame);
            _pixelCompositeFrameIndex = _pixelStudio.ActiveFrameIndex;
            _pixelCompositeRevision++;
        }

        RefreshOnionSkinBuffers(forceFullRefresh);

        if (_pixelStudio.PreviewFrameIndex == _pixelStudio.ActiveFrameIndex)
        {
            _pixelPreviewPixels = _pixelCompositePixels;
            _pixelPreviewFrameIndex = _pixelCompositeFrameIndex;
            _pixelPreviewRevision = _pixelCompositeRevision;
            return;
        }

        bool previewInvalid = forceFullRefresh
            || _pixelPreviewPixels.Count != pixelCount
            || _pixelPreviewFrameIndex != _pixelStudio.PreviewFrameIndex;
        if (previewInvalid)
        {
            _pixelPreviewPixels = ComposeVisiblePixels(PreviewPixelFrame);
            _pixelPreviewFrameIndex = _pixelStudio.PreviewFrameIndex;
            _pixelPreviewRevision++;
        }
    }

    private void RefreshOnionSkinBuffers(bool forceFullRefresh = false)
    {
        int pixelCount = Math.Max(_pixelStudio.CanvasWidth * _pixelStudio.CanvasHeight, 0);
        if (!_pixelStudio.ShowOnionSkin || _pixelStudio.Frames.Count <= 1)
        {
            bool previousChanged = _pixelOnionPreviousPixels.Count != pixelCount || _pixelOnionPreviousFrameIndex != -1;
            bool nextChanged = _pixelOnionNextPixels.Count != pixelCount || _pixelOnionNextFrameIndex != -1;
            _pixelOnionPreviousPixels = CreateTransparentPixelBuffer(pixelCount);
            _pixelOnionNextPixels = CreateTransparentPixelBuffer(pixelCount);
            _pixelOnionPreviousFrameIndex = -1;
            _pixelOnionNextFrameIndex = -1;
            if (previousChanged)
            {
                _pixelOnionPreviousRevision++;
            }

            if (nextChanged)
            {
                _pixelOnionNextRevision++;
            }

            return;
        }

        int previousFrameIndex = (_pixelStudio.ActiveFrameIndex - 1 + _pixelStudio.Frames.Count) % _pixelStudio.Frames.Count;
        int nextFrameIndex = (_pixelStudio.ActiveFrameIndex + 1) % _pixelStudio.Frames.Count;
        if (!_pixelStudio.ShowPreviousOnion)
        {
            if (forceFullRefresh || _pixelOnionPreviousPixels.Count != pixelCount || _pixelOnionPreviousFrameIndex != -1)
            {
                _pixelOnionPreviousPixels = CreateTransparentPixelBuffer(pixelCount);
                _pixelOnionPreviousFrameIndex = -1;
                _pixelOnionPreviousRevision++;
            }
        }
        else if (forceFullRefresh || _pixelOnionPreviousPixels.Count != pixelCount || _pixelOnionPreviousFrameIndex != previousFrameIndex)
        {
            _pixelOnionPreviousPixels = ComposeOnionSkinPixels(previousFrameIndex, Math.Clamp(_pixelStudio.OnionOpacity, 0f, 1f));
            _pixelOnionPreviousFrameIndex = previousFrameIndex;
            _pixelOnionPreviousRevision++;
        }

        if (!_pixelStudio.ShowNextOnion)
        {
            if (forceFullRefresh || _pixelOnionNextPixels.Count != pixelCount || _pixelOnionNextFrameIndex != -1)
            {
                _pixelOnionNextPixels = CreateTransparentPixelBuffer(pixelCount);
                _pixelOnionNextFrameIndex = -1;
                _pixelOnionNextRevision++;
            }
        }
        else if (forceFullRefresh || _pixelOnionNextPixels.Count != pixelCount || _pixelOnionNextFrameIndex != nextFrameIndex)
        {
            _pixelOnionNextPixels = ComposeOnionSkinPixels(nextFrameIndex, Math.Clamp(_pixelStudio.OnionOpacity, 0f, 1f));
            _pixelOnionNextFrameIndex = nextFrameIndex;
            _pixelOnionNextRevision++;
        }
    }

    private static List<ThemeColor?> CreateTransparentPixelBuffer(int pixelCount)
    {
        if (pixelCount <= 0)
        {
            return [];
        }

        return Enumerable.Repeat<ThemeColor?>(null, pixelCount).ToList();
    }

    private void ApplyCompositePixelChanges(PixelStudioFrameState frame, IList<ThemeColor?> composite, IEnumerable<int> dirtyPixelIndices)
    {
        foreach (int pixelIndex in dirtyPixelIndices)
        {
            if (pixelIndex < 0 || pixelIndex >= composite.Count)
            {
                continue;
            }

            composite[pixelIndex] = ComposeVisiblePixel(frame, pixelIndex);
        }
    }

    private ThemeColor? ComposeVisiblePixel(PixelStudioFrameState frame, int pixelIndex)
    {
        ThemeColor? resolvedColor = null;
        foreach (PixelStudioLayerState layer in frame.Layers)
        {
            if (!layer.IsVisible || pixelIndex < 0 || pixelIndex >= layer.Pixels.Length)
            {
                continue;
            }

            ThemeColor? layerColor = UnpackPixelColor(layer.Pixels[pixelIndex]);
            if (layerColor is not null)
            {
                resolvedColor = BlendLayerColorOver(resolvedColor, layerColor.Value, layer.Opacity);
            }
        }

        return resolvedColor;
    }

    private bool FloodFillCell(int cellIndex, int paletteIndex)
    {
        HashSet<int> seedIndices = [];
        int fillX = cellIndex % _pixelStudio.CanvasWidth;
        int fillY = cellIndex / _pixelStudio.CanvasWidth;
        AddMirroredCanvasTargets(seedIndices, fillX, fillY);
        return FloodFillSeedSet(seedIndices, paletteIndex);
    }

    private bool FloodFillSeedSet(IEnumerable<int> seedIndices, int paletteIndex)
    {
        int[] pixels = CurrentPixelLayer.Pixels;
        int[] sourcePixels = pixels.ToArray();

        bool changed = false;
        bool blockedByAlphaLock = false;
        foreach (int seedIndex in seedIndices.Distinct())
        {
            if (seedIndex < 0 || seedIndex >= sourcePixels.Length)
            {
                continue;
            }

            int target = sourcePixels[seedIndex];
            if (target == paletteIndex)
            {
                continue;
            }

            Queue<int> pending = new();
            HashSet<int> visited = [];
            pending.Enqueue(seedIndex);
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                if (!visited.Add(index) || sourcePixels[index] != target)
                {
                    continue;
                }

                int x = index % _pixelStudio.CanvasWidth;
                int y = index / _pixelStudio.CanvasWidth;
                if (!IsWithinSelection(x, y))
                {
                    continue;
                }

                if (CanWritePixelToLayer(CurrentPixelLayer, index, paletteIndex) && pixels[index] != paletteIndex)
                {
                    pixels[index] = paletteIndex;
                    changed = true;
                }
                else if (CurrentPixelLayer.IsAlphaLocked)
                {
                    blockedByAlphaLock = true;
                }

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
        }

        if (!changed && blockedByAlphaLock && !_pixelWarningDialogVisible)
        {
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.AlphaLockedLayerEdit,
                "Alpha Lock Blocks This Fill",
                $"Alpha lock on \"{CurrentPixelLayer.Name}\" only lets you recolor existing painted pixels. Disable alpha lock to fill empty space.",
                targetLayerIndex: _pixelStudio.ActiveLayerIndex);
        }

        return changed;
    }

    private bool FillSelectionPixels(int paletteIndex)
    {
        HashSet<int> targetIndices = [];
        foreach (int index in EnumerateSelectedIndices())
        {
            if (index < 0 || index >= CurrentPixelLayer.Pixels.Length)
            {
                continue;
            }

            int x = index % _pixelStudio.CanvasWidth;
            int y = index / _pixelStudio.CanvasWidth;
            AddMirroredBrushTargets(targetIndices, x, y);
        }

        bool changed = false;
        foreach (int index in targetIndices)
        {
            if (index < 0 || index >= CurrentPixelLayer.Pixels.Length)
            {
                continue;
            }

            if (CurrentPixelLayer.Pixels[index] == paletteIndex)
            {
                continue;
            }

            if (TryWritePixelToCurrentLayer(index, paletteIndex))
            {
                changed = true;
            }
        }

        return changed;
    }

    private ThemeColor? GetTopVisiblePixelColor(int cellIndex)
    {
        for (int index = 0; index < CurrentPixelFrame.Layers.Count; index++)
        {
            PixelStudioLayerState layer = CurrentPixelFrame.Layers[index];
            if (!layer.IsVisible)
            {
                continue;
            }

            ThemeColor? color = UnpackPixelColor(layer.Pixels[cellIndex]);
            if (color is not null)
            {
                return color;
            }
        }

        return null;
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

        return IsSelectionCellMarked(x, y);
    }

    private bool IsWithinCurrentSelection(int x, int y)
    {
        if (!_selectionActive)
        {
            return false;
        }

        return IsSelectionCellMarked(x, y);
    }

    private bool SelectionUsesMask() => _selectionMask.Count > 0;

    private bool IsSelectionCellMarked(int x, int y)
    {
        if (!_selectionActive || !IsWithinCanvas(x, y))
        {
            return false;
        }

        if (SelectionUsesMask())
        {
            return _selectionMask.Contains((y * _pixelStudio.CanvasWidth) + x);
        }

        int left = Math.Min(_selectionStartX, _selectionEndX);
        int right = Math.Max(_selectionStartX, _selectionEndX);
        int top = Math.Min(_selectionStartY, _selectionEndY);
        int bottom = Math.Max(_selectionStartY, _selectionEndY);
        return x >= left && x <= right && y >= top && y <= bottom;
    }

    private int GetSelectionLeft() => SelectionUsesMask() ? _selectionMaskLeft : Math.Min(_selectionStartX, _selectionEndX);

    private int GetSelectionTop() => SelectionUsesMask() ? _selectionMaskTop : Math.Min(_selectionStartY, _selectionEndY);

    private int GetSelectionWidth() => SelectionUsesMask() ? Math.Max((_selectionMaskRight - _selectionMaskLeft) + 1, 0) : Math.Abs(_selectionEndX - _selectionStartX) + 1;

    private int GetSelectionHeight() => SelectionUsesMask() ? Math.Max((_selectionMaskBottom - _selectionMaskTop) + 1, 0) : Math.Abs(_selectionEndY - _selectionStartY) + 1;

    private void SetSelectionRect(int left, int top, int width, int height)
    {
        _selectionMask.Clear();
        _selectionMaskLeft = left;
        _selectionMaskTop = top;
        _selectionMaskRight = left + Math.Max(width - 1, 0);
        _selectionMaskBottom = top + Math.Max(height - 1, 0);
        _selectionStartX = left;
        _selectionStartY = top;
        _selectionEndX = left + Math.Max(width - 1, 0);
        _selectionEndY = top + Math.Max(height - 1, 0);
        ResetSelectionTransformPivotToSelectionCenter();
        ResetSelectionTransformInputEditing();
    }

    private void ApplySelectionMask(IEnumerable<int> indices, bool committed)
    {
        _selectionMask.Clear();
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;

        foreach (int index in indices)
        {
            if (index < 0 || index >= _pixelStudio.CanvasWidth * _pixelStudio.CanvasHeight)
            {
                continue;
            }

            _selectionMask.Add(index);
            int x = index % _pixelStudio.CanvasWidth;
            int y = index / _pixelStudio.CanvasWidth;
            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }

        if (_selectionMask.Count == 0)
        {
            ClearSelection();
            return;
        }

        _selectionActive = true;
        _selectionCommitted = committed;
        _selectionDragActive = false;
        _selectionMaskLeft = left;
        _selectionMaskTop = top;
        _selectionMaskRight = right;
        _selectionMaskBottom = bottom;
        _selectionStartX = left;
        _selectionStartY = top;
        _selectionEndX = right;
        _selectionEndY = bottom;
        ResetSelectionTransformPivotToSelectionCenter();
        ResetSelectionTransformInputEditing();
    }

    private void ClearSelection()
    {
        bool restoredOriginalPixels = _selectionMoveActive;
        ResetSelectionMoveState(restoreOriginalPixels: _selectionMoveActive);
        _selectionTransformModeActive = false;
        ClearSelectionTransformPreview();
        if (restoredOriginalPixels)
        {
            InvalidatePixelStudioPixelBuffers();
        }

        _selectionActive = false;
        _selectionCommitted = false;
        _selectionDragActive = false;
        _selectionStartX = 0;
        _selectionStartY = 0;
        _selectionEndX = 0;
        _selectionEndY = 0;
        _selectionMask.Clear();
        _selectionMaskLeft = 0;
        _selectionMaskTop = 0;
        _selectionMaskRight = 0;
        _selectionMaskBottom = 0;
        _selectionTransformPivotX = float.NaN;
        _selectionTransformPivotY = float.NaN;
        ResetSelectionTransformInputEditing();
    }

    private bool HasSelectionClipboard()
    {
        return _selectionClipboardPixels is not null
            && _selectionClipboardPixels.Length > 0
            && _selectionClipboardWidth > 0
            && _selectionClipboardHeight > 0;
    }

    private bool TryGetSelectionBounds(out int left, out int top, out int width, out int height)
    {
        left = 0;
        top = 0;
        width = 0;
        height = 0;
        if (!_selectionActive)
        {
            return false;
        }

        left = GetSelectionLeft();
        top = GetSelectionTop();
        width = GetSelectionWidth();
        height = GetSelectionHeight();
        return width > 0 && height > 0;
    }

    private IEnumerable<int> EnumerateSelectedIndices()
    {
        if (!_selectionActive)
        {
            yield break;
        }

        if (SelectionUsesMask())
        {
            foreach (int index in _selectionMask)
            {
                yield return index;
            }

            yield break;
        }

        int left = GetSelectionLeft();
        int top = GetSelectionTop();
        int right = left + GetSelectionWidth() - 1;
        int bottom = top + GetSelectionHeight() - 1;
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                yield return (y * _pixelStudio.CanvasWidth) + x;
            }
        }
    }

    private int[] BuildSelectionClipboardBuffer(int left, int top, int width, int height)
    {
        int[] pixels = new int[width * height];
        Array.Fill(pixels, ClipboardEmptyPixel);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int canvasX = left + x;
                int canvasY = top + y;
                if (!IsSelectionCellMarked(canvasX, canvasY))
                {
                    continue;
                }

                pixels[(y * width) + x] = CurrentPixelLayer.Pixels[(canvasY * _pixelStudio.CanvasWidth) + canvasX];
            }
        }

        return pixels;
    }

    private IReadOnlyList<ThemeColor?> BuildSelectionTransformSourceColors()
    {
        if (!_selectionActive
            || !TryGetSelectionBounds(out int left, out int top, out int width, out int height)
            || width <= 0
            || height <= 0)
        {
            return [];
        }

        int[] sourcePixels = BuildSelectionClipboardBuffer(left, top, width, height);
        ThemeColor?[] sourceColors = new ThemeColor?[sourcePixels.Length];
        float layerOpacity = NormalizeLayerOpacity(CurrentPixelLayer.Opacity);
        for (int index = 0; index < sourcePixels.Length; index++)
        {
            ThemeColor? sourceColor = UnpackPixelColor(sourcePixels[index]);
            if (sourceColor is null)
            {
                continue;
            }

            sourceColors[index] = new ThemeColor(
                sourceColor.Value.R,
                sourceColor.Value.G,
                sourceColor.Value.B,
                Math.Clamp(sourceColor.Value.A * layerOpacity, 0f, 1f));
        }

        return sourceColors;
    }

    private bool CaptureSelectionClipboard(bool showStatus = true)
    {
        if (!TryGetSelectionBounds(out int left, out int top, out int width, out int height))
        {
            if (showStatus)
            {
                RefreshPixelStudioView("Create a selection before copying.");
            }

            return false;
        }

        int[] pixels = BuildSelectionClipboardBuffer(left, top, width, height);

        _selectionClipboardPixels = pixels;
        _selectionClipboardWidth = width;
        _selectionClipboardHeight = height;

        if (showStatus)
        {
            RefreshPixelStudioView($"Copied {width}x{height} selection.", rebuildLayout: true);
        }

        return true;
    }

    private void ResetSelectionMoveState(bool restoreOriginalPixels = false)
    {
        if (restoreOriginalPixels && _selectionMoveLayerSnapshot is not null && _selectionMoveLayerSnapshot.Length == CurrentPixelLayer.Pixels.Length)
        {
            Array.Copy(_selectionMoveLayerSnapshot, CurrentPixelLayer.Pixels, _selectionMoveLayerSnapshot.Length);
        }

        _selectionMoveActive = false;
        _selectionMovePointerCellX = 0;
        _selectionMovePointerCellY = 0;
        _selectionMoveOriginLeft = 0;
        _selectionMoveOriginTop = 0;
        _selectionMoveCurrentLeft = 0;
        _selectionMoveCurrentTop = 0;
        _selectionMoveWidth = 0;
        _selectionMoveHeight = 0;
        _selectionMovePixels = null;
        _selectionMoveLayerSnapshot = null;
        _selectionMoveSourceIndices = null;
        _selectionMoveUsesMask = false;
        _selectionMoveSnapshot = null;
        if (_pixelDragMode == PixelStudioDragMode.MoveSelection)
        {
            _pixelDragMode = PixelStudioDragMode.None;
        }
    }

    private void SetBrushSizeFromSlider(UiRect sliderRect, float mouseY)
    {
        float ratio = 1f - Math.Clamp((mouseY - sliderRect.Y) / Math.Max(sliderRect.Height, 1), 0f, 1f);
        SetCurrentBrushSize(1 + (int)MathF.Round(ratio * 15f));
    }

    private void SetCurrentBrushSize(int size)
    {
        int clamped = Math.Clamp(size, 1, 16);
        _pixelStudio.BrushSize = clamped;
        RememberBrushSizeForTool(_pixelStudio.ActiveTool, _brushMode, clamped);
    }

    private void ActivatePixelStudioTool(PixelStudioToolKind tool)
    {
        RememberBrushSizeForTool(_pixelStudio.ActiveTool, _brushMode, _pixelStudio.BrushSize);
        _pixelStudio.ActiveTool = tool;
        if (UsesRememberedBrushSize(tool))
        {
            _pixelStudio.BrushSize = GetRememberedBrushSize(tool, _brushMode, _pixelStudio.BrushSize);
        }
    }

    private static bool UsesRememberedBrushSize(PixelStudioToolKind tool)
        => tool is PixelStudioToolKind.Pencil or PixelStudioToolKind.Eraser or PixelStudioToolKind.Line;

    private int GetRememberedBrushSize(PixelStudioToolKind tool, PixelStudioBrushMode mode, int fallbackSize)
    {
        return tool switch
        {
            PixelStudioToolKind.Pencil => mode switch
            {
                PixelStudioBrushMode.Square => _pencilHardBrushSize,
                PixelStudioBrushMode.PixelPerfect => _pencilPixelPerfectBrushSize,
                _ => _pencilRoundBrushSize
            },
            PixelStudioToolKind.Eraser => mode switch
            {
                PixelStudioBrushMode.Square => _eraserHardBrushSize,
                PixelStudioBrushMode.PixelPerfect => _eraserPixelPerfectBrushSize,
                _ => _eraserRoundBrushSize
            },
            PixelStudioToolKind.Line => _lineBrushSize,
            _ => Math.Clamp(fallbackSize, 1, 16)
        };
    }

    private void RememberBrushSizeForTool(PixelStudioToolKind tool, PixelStudioBrushMode mode, int size)
    {
        if (!UsesRememberedBrushSize(tool))
        {
            return;
        }

        int clamped = Math.Clamp(size, 1, 16);
        switch (tool)
        {
            case PixelStudioToolKind.Pencil:
                switch (mode)
                {
                    case PixelStudioBrushMode.Square:
                        _pencilHardBrushSize = clamped;
                        break;
                    case PixelStudioBrushMode.PixelPerfect:
                        _pencilPixelPerfectBrushSize = clamped;
                        break;
                    default:
                        _pencilRoundBrushSize = clamped;
                        break;
                }
                break;
            case PixelStudioToolKind.Eraser:
                switch (mode)
                {
                    case PixelStudioBrushMode.Square:
                        _eraserHardBrushSize = clamped;
                        break;
                    case PixelStudioBrushMode.PixelPerfect:
                        _eraserPixelPerfectBrushSize = clamped;
                        break;
                    default:
                        _eraserRoundBrushSize = clamped;
                        break;
                }
                break;
            case PixelStudioToolKind.Line:
                _lineBrushSize = clamped;
                break;
        }
    }

    private void InitializeRememberedBrushSizes(int size)
    {
        int clamped = Math.Clamp(size, 1, 16);
        _pencilRoundBrushSize = clamped;
        _pencilHardBrushSize = clamped;
        _pencilPixelPerfectBrushSize = 1;
        _eraserRoundBrushSize = clamped;
        _eraserHardBrushSize = clamped;
        _eraserPixelPerfectBrushSize = 1;
        _lineBrushSize = clamped;
    }

    private void SetOnionOpacityFromSlider(UiRect sliderRect, float mouseX)
    {
        if (sliderRect.Width <= 0f)
        {
            return;
        }

        float opacity = Math.Clamp((mouseX - sliderRect.X) / Math.Max(sliderRect.Width, 1f), 0f, 1f);
        if (MathF.Abs(_pixelStudio.OnionOpacity - opacity) <= 0.002f)
        {
            return;
        }

        _pixelStudio.OnionOpacity = opacity;
        RefreshOnionSkinInteraction();
    }

    private void BeginLayerOpacityAdjustment()
    {
        _layerOpacityAdjustSnapshot = CapturePixelStudioState();
        _layerOpacityAdjustChanged = false;
    }

    private void SetLayerOpacityFromSlider(UiRect sliderRect, float mouseX)
    {
        if (sliderRect.Width <= 0f)
        {
            return;
        }

        int layerIndex = Math.Clamp(_pixelStudio.ActiveLayerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        float opacity = NormalizeLayerOpacity((mouseX - sliderRect.X) / Math.Max(sliderRect.Width, 1f));
        if (MathF.Abs(CurrentPixelFrame.Layers[layerIndex].Opacity - opacity) <= 0.002f)
        {
            return;
        }

        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            frame.Layers[layerIndex].Opacity = opacity;
        }

        _layerOpacityAdjustChanged = true;
        RefreshPixelStudioView(refreshPixelBuffers: true);
    }

    private void CommitLayerOpacityAdjustment()
    {
        if (_layerOpacityAdjustChanged && _layerOpacityAdjustSnapshot is not null)
        {
            _pixelUndoStack.Push(_layerOpacityAdjustSnapshot);
            _pixelRedoStack.Clear();
            MarkPixelStudioRecoveryDirty();
            RefreshPixelStudioView($"Layer opacity set to {MathF.Round(CurrentPixelLayer.Opacity * 100f)}%.", refreshPixelBuffers: true);
        }

        _layerOpacityAdjustSnapshot = null;
        _layerOpacityAdjustChanged = false;
    }

    private void RefreshOnionSkinInteraction()
    {
        EnsurePixelStudioIndices();
        RefreshOnionSkinBuffers(forceFullRefresh: true);
        _uiState.PixelStudio.OnionOpacity = _pixelStudio.OnionOpacity;
        _uiState.PixelStudio.ShowOnionSkin = _pixelStudio.ShowOnionSkin;
        _uiState.PixelStudio.ShowPreviousOnion = _pixelStudio.ShowPreviousOnion;
        _uiState.PixelStudio.ShowNextOnion = _pixelStudio.ShowNextOnion;
        _uiState.PixelStudio.OnionPreviousPixels = _pixelOnionPreviousPixels;
        _uiState.PixelStudio.OnionNextPixels = _pixelOnionNextPixels;
        _uiState.PixelStudio.OnionPreviousPixelsRevision = _pixelOnionPreviousRevision;
        _uiState.PixelStudio.OnionNextPixelsRevision = _pixelOnionNextRevision;
        _renderer?.UpdateUiState(_uiState);
    }

    private void BeginCanvasPan(float mouseX, float mouseY)
    {
        _pixelDragMode = PixelStudioDragMode.PanCanvas;
        _pixelPanDragStartMouseX = mouseX;
        _pixelPanDragStartMouseY = mouseY;
        _pixelPanDragStartX = _pixelStudio.CanvasPanX;
        _pixelPanDragStartY = _pixelStudio.CanvasPanY;
    }

    private void ResetSelectionTransformAngleEditing()
    {
        _selectionTransformAngleFieldActive = false;
        _selectionTransformAngleBuffer = string.Empty;
        ClearSelectedText(EditableTextTarget.TransformAngle);
    }

    private void ResetSelectionTransformScaleEditing()
    {
        _selectionTransformScaleXFieldActive = false;
        _selectionTransformScaleYFieldActive = false;
        _selectionTransformScaleXBuffer = string.Empty;
        _selectionTransformScaleYBuffer = string.Empty;
        ClearSelectedText(EditableTextTarget.TransformScaleX);
        ClearSelectedText(EditableTextTarget.TransformScaleY);
    }

    private void ResetSelectionTransformInputEditing()
    {
        ResetSelectionTransformAngleEditing();
        ResetSelectionTransformScaleEditing();
    }

    private bool IsSelectionTransformInputActive()
    {
        return _selectionTransformAngleFieldActive
            || _selectionTransformScaleXFieldActive
            || _selectionTransformScaleYFieldActive;
    }

    private float GetSelectionTransformPreviewScaleX()
    {
        return Math.Max(_selectionTransformPreviewScaleX, 0.01f);
    }

    private float GetSelectionTransformPreviewScaleY()
    {
        return Math.Max(_selectionTransformPreviewScaleY, 0.01f);
    }

    private float GetSelectionTransformScalePercent(bool horizontal)
    {
        float scale = horizontal ? GetSelectionTransformPreviewScaleX() : GetSelectionTransformPreviewScaleY();
        return scale * 100f;
    }

    private void ResetSelectionTransformPivotToSelectionCenter()
    {
        if (TryGetSelectionBounds(out int left, out int top, out int width, out int height))
        {
            _selectionTransformPivotX = left + (width * 0.5f);
            _selectionTransformPivotY = top + (height * 0.5f);
        }
        else
        {
            _selectionTransformPivotX = float.NaN;
            _selectionTransformPivotY = float.NaN;
        }
    }

    private static (float X, float Y) GetTransformAnchorPoint(int left, int top, int width, int height, PixelStudioResizeAnchor anchor)
    {
        (float anchorX, float anchorY) = anchor switch
        {
            PixelStudioResizeAnchor.TopLeft => (0f, 0f),
            PixelStudioResizeAnchor.Top => (0.5f, 0f),
            PixelStudioResizeAnchor.TopRight => (1f, 0f),
            PixelStudioResizeAnchor.Left => (0f, 0.5f),
            PixelStudioResizeAnchor.Center => (0.5f, 0.5f),
            PixelStudioResizeAnchor.Right => (1f, 0.5f),
            PixelStudioResizeAnchor.BottomLeft => (0f, 1f),
            PixelStudioResizeAnchor.Bottom => (0.5f, 1f),
            PixelStudioResizeAnchor.BottomRight => (1f, 1f),
            _ => (0.5f, 0.5f)
        };

        return (left + (width * anchorX), top + (height * anchorY));
    }

    private void EnsureSelectionTransformPivotInitialized(int left, int top, int width, int height)
    {
        if (float.IsFinite(_selectionTransformPivotX) && float.IsFinite(_selectionTransformPivotY))
        {
            return;
        }

        _selectionTransformPivotX = left + (width * 0.5f);
        _selectionTransformPivotY = top + (height * 0.5f);
    }

    private void EnsureSelectionTransformPreviewInitialized()
    {
        if (_selectionTransformSourceWidth > 0 && _selectionTransformSourceHeight > 0)
        {
            return;
        }

        if (!TryGetSelectionBounds(out int left, out int top, out int width, out int height))
        {
            return;
        }

        EnsureSelectionTransformPivotInitialized(left, top, width, height);
        _selectionTransformHandleKind = PixelStudioSelectionHandleKind.Rotate;
        _selectionTransformSourceLeft = left;
        _selectionTransformSourceTop = top;
        _selectionTransformSourceWidth = width;
        _selectionTransformSourceHeight = height;
        SetSelectionTransformPreviewState(left, top, width, height, 0f, 1f, 1f);
        _selectionTransformPreviewVisible = false;
    }

    private void UpdateSelectionTransformPreviewFromCurrentState(PixelStudioLayoutSnapshot? layout = null)
    {
        EnsureSelectionTransformPreviewInitialized();
        if (_selectionTransformSourceWidth <= 0 || _selectionTransformSourceHeight <= 0)
        {
            return;
        }

        float normalizedAngle = NormalizeTransformRotationDegrees(_selectionTransformPreviewRotationDegrees);
        float scaleX = GetSelectionTransformPreviewScaleX();
        float scaleY = GetSelectionTransformPreviewScaleY();
        ComputeTransformedSelectionTargetBounds(
            _selectionTransformSourceLeft,
            _selectionTransformSourceTop,
            _selectionTransformSourceWidth,
            _selectionTransformSourceHeight,
            _selectionTransformPivotX,
            _selectionTransformPivotY,
            scaleX,
            scaleY,
            normalizedAngle,
            out int transformedLeft,
            out int transformedTop,
            out int transformedWidth,
            out int transformedHeight);

        bool changed =
            transformedLeft != _selectionTransformSourceLeft
            || transformedTop != _selectionTransformSourceTop
            || transformedWidth != _selectionTransformSourceWidth
            || transformedHeight != _selectionTransformSourceHeight
            || MathF.Abs(normalizedAngle) > 0.01f
            || MathF.Abs(scaleX - 1f) > 0.0001f
            || MathF.Abs(scaleY - 1f) > 0.0001f;

        SetSelectionTransformPreviewState(transformedLeft, transformedTop, transformedWidth, transformedHeight, normalizedAngle, scaleX, scaleY);
        _selectionTransformPreviewVisible = changed;
        if (!_selectionTransformPreviewVisible)
        {
            SetSelectionTransformPreviewState(
                _selectionTransformSourceLeft,
                _selectionTransformSourceTop,
                _selectionTransformSourceWidth,
                _selectionTransformSourceHeight,
                normalizedAngle,
                scaleX,
                scaleY);
        }

        RefreshSelectionTransformOverlay(layout ?? _layoutSnapshot?.PixelStudio);
    }

    private void ApplySelectionTransformRotationPreview(float angleDegrees)
    {
        EnsureSelectionTransformPreviewInitialized();
        if (_selectionTransformSourceWidth <= 0 || _selectionTransformSourceHeight <= 0)
        {
            return;
        }

        _selectionTransformPreviewRotationDegrees = NormalizeTransformRotationDegrees(angleDegrees);
        UpdateSelectionTransformPreviewFromCurrentState(_layoutSnapshot?.PixelStudio);
    }

    private void ActivateSelectionTransformScaleField(bool horizontal)
    {
        if (!_selectionTransformModeActive || !_selectionActive || !_selectionCommitted)
        {
            RefreshPixelStudioView("Enable transform on a committed selection before entering scale.");
            return;
        }

        EnsureSelectionTransformPreviewInitialized();
        ResetSelectionTransformInputEditing();
        if (horizontal)
        {
            _selectionTransformScaleXFieldActive = true;
            _selectionTransformScaleXBuffer = GetSelectionTransformScalePercent(horizontal: true).ToString("0.#", CultureInfo.InvariantCulture);
            SelectAllText(EditableTextTarget.TransformScaleX);
        }
        else
        {
            _selectionTransformScaleYFieldActive = true;
            _selectionTransformScaleYBuffer = GetSelectionTransformScalePercent(horizontal: false).ToString("0.#", CultureInfo.InvariantCulture);
            SelectAllText(EditableTextTarget.TransformScaleY);
        }

        RefreshSelectionTransformOverlay(_layoutSnapshot?.PixelStudio);
        RefreshPixelStudioView(horizontal ? "Editing transform scale X." : "Editing transform scale Y.", rebuildLayout: true);
    }

    private void SetSelectionTransformScalePercent(bool horizontal, float percent)
    {
        float normalizedPercent = Math.Clamp(percent, 1f, 6400f);
        if (horizontal)
        {
            _selectionTransformPreviewScaleX = normalizedPercent / 100f;
            if (_selectionTransformScaleXFieldActive)
            {
                _selectionTransformScaleXBuffer = normalizedPercent.ToString("0.#", CultureInfo.InvariantCulture);
            }
        }
        else
        {
            _selectionTransformPreviewScaleY = normalizedPercent / 100f;
            if (_selectionTransformScaleYFieldActive)
            {
                _selectionTransformScaleYBuffer = normalizedPercent.ToString("0.#", CultureInfo.InvariantCulture);
            }
        }

        UpdateSelectionTransformPreviewFromCurrentState(_layoutSnapshot?.PixelStudio);
    }

    private void UpdateSelectionTransformScaleFromBuffer(bool horizontal)
    {
        bool active = horizontal ? _selectionTransformScaleXFieldActive : _selectionTransformScaleYFieldActive;
        if (!active)
        {
            return;
        }

        string buffer = horizontal ? _selectionTransformScaleXBuffer : _selectionTransformScaleYBuffer;
        if (string.IsNullOrWhiteSpace(buffer)
            || buffer == "."
            || buffer == "+")
        {
            SetSelectionTransformScalePercent(horizontal, 100f);
            RefreshPixelStudioView(horizontal ? "Editing transform scale X." : "Editing transform scale Y.", rebuildLayout: true);
            return;
        }

        if (!float.TryParse(buffer, NumberStyles.Float, CultureInfo.InvariantCulture, out float percent))
        {
            RefreshPixelStudioView(horizontal ? "Editing transform scale X." : "Editing transform scale Y.", rebuildLayout: true);
            return;
        }

        SetSelectionTransformScalePercent(horizontal, percent);
        RefreshPixelStudioView(
            horizontal
                ? $"Transform scale X set to {Math.Clamp(percent, 1f, 6400f):0.#}%."
                : $"Transform scale Y set to {Math.Clamp(percent, 1f, 6400f):0.#}%.",
            rebuildLayout: true);
    }

    private void ActivateSelectionTransformAngleField()
    {
        if (!_selectionTransformModeActive || !_selectionActive || !_selectionCommitted)
        {
            RefreshPixelStudioView("Enable transform on a committed selection before entering an angle.");
            return;
        }

        EnsureSelectionTransformPreviewInitialized();
        ResetSelectionTransformInputEditing();
        _selectionTransformAngleFieldActive = true;
        _selectionTransformAngleBuffer = NormalizeTransformRotationDegrees(
            _selectionTransformPreviewRotationDegrees)
            .ToString("0.#", CultureInfo.InvariantCulture);
        SelectAllText(EditableTextTarget.TransformAngle);
        RefreshSelectionTransformOverlay(_layoutSnapshot?.PixelStudio);
        RefreshPixelStudioView("Editing transform angle.", rebuildLayout: true);
    }

    private void UpdateSelectionTransformAngleFromBuffer()
    {
        if (!_selectionTransformAngleFieldActive)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectionTransformAngleBuffer)
            || _selectionTransformAngleBuffer == "-"
            || _selectionTransformAngleBuffer == "."
            || _selectionTransformAngleBuffer == "-.")
        {
            ApplySelectionTransformRotationPreview(0f);
            RefreshPixelStudioView("Editing transform angle.", rebuildLayout: true);
            return;
        }

        if (!float.TryParse(_selectionTransformAngleBuffer, NumberStyles.Float, CultureInfo.InvariantCulture, out float angleDegrees))
        {
            RefreshPixelStudioView("Editing transform angle.", rebuildLayout: true);
            return;
        }

        ApplySelectionTransformRotationPreview(angleDegrees);
        RefreshPixelStudioView($"Transform angle set to {NormalizeTransformRotationDegrees(angleDegrees):0.#} deg.", rebuildLayout: true);
    }

    private void SetSelectionTransformPivotPreset(PixelStudioResizeAnchor anchor)
    {
        if (!_selectionTransformModeActive || !_selectionActive || !_selectionCommitted)
        {
            RefreshPixelStudioView("Enable transform on a committed selection before changing the pivot.");
            return;
        }

        EnsureSelectionTransformPreviewInitialized();
        if (_selectionTransformSourceWidth <= 0 || _selectionTransformSourceHeight <= 0)
        {
            return;
        }

        (float pivotX, float pivotY) = GetTransformAnchorPoint(
            _selectionTransformSourceLeft,
            _selectionTransformSourceTop,
            _selectionTransformSourceWidth,
            _selectionTransformSourceHeight,
            anchor);
        _selectionTransformPivotX = pivotX;
        _selectionTransformPivotY = pivotY;
        UpdateSelectionTransformPreviewFromCurrentState(_layoutSnapshot?.PixelStudio);
        RefreshPixelStudioView($"Transform pivot set to {GetResizeAnchorLabel(anchor)}.", rebuildLayout: true);
    }

    private void BeginSelectionTransform(PixelStudioSelectionHandleKind handleKind)
    {
        if (!_selectionTransformModeActive)
        {
            return;
        }

        if (!TryGetSelectionBounds(out int left, out int top, out int width, out int height))
        {
            return;
        }

        if (handleKind is not PixelStudioSelectionHandleKind.Pivot
            && !CanTransformCurrentLayer(handleKind == PixelStudioSelectionHandleKind.Rotate ? "rotating the selection" : "scaling the selection"))
        {
            return;
        }

        EnsureSelectionTransformPivotInitialized(left, top, width, height);
        EnsureSelectionTransformPreviewInitialized();
        ResetSelectionTransformInputEditing();
        _selectionTransformHandleKind = handleKind;
        _selectionTransformPreviewVisible = handleKind is not PixelStudioSelectionHandleKind.Pivot;
        _pixelDragMode = PixelStudioDragMode.TransformSelection;
        RefreshSelectionTransformOverlay(_layoutSnapshot?.PixelStudio);
    }

    private void UpdateSelectionTransformPreview(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (_pixelDragMode != PixelStudioDragMode.TransformSelection)
        {
            return;
        }

        if (_selectionTransformHandleKind == PixelStudioSelectionHandleKind.Rotate)
        {
            if (!TryGetCanvasPoint(layout, mouseX, mouseY, out float mouseCanvasX, out float mouseCanvasY))
            {
                return;
            }

            float deltaX = mouseCanvasX - _selectionTransformPivotX;
            float deltaY = mouseCanvasY - _selectionTransformPivotY;
            if ((deltaX * deltaX) + (deltaY * deltaY) < 0.0001f)
            {
                return;
            }

            float nextRotationDegrees = SnapSelectionTransformRotationDegrees((MathF.Atan2(deltaY, deltaX) * 180f / MathF.PI) + 90f);
            if (MathF.Abs(_selectionTransformPreviewRotationDegrees - nextRotationDegrees) <= 0.01f)
            {
                return;
            }

            _selectionTransformPreviewRotationDegrees = nextRotationDegrees;
            if (_selectionTransformAngleFieldActive)
            {
                _selectionTransformAngleBuffer = nextRotationDegrees.ToString("0.#", CultureInfo.InvariantCulture);
            }
            UpdateSelectionTransformPreviewFromCurrentState(layout);
            return;
        }

        if (_selectionTransformHandleKind == PixelStudioSelectionHandleKind.Pivot)
        {
            if (!TryGetCanvasPoint(layout, mouseX, mouseY, out float pivotCanvasX, out float pivotCanvasY))
            {
                return;
            }

            _selectionTransformPivotX = Math.Clamp(pivotCanvasX, 0f, _pixelStudio.CanvasWidth);
            _selectionTransformPivotY = Math.Clamp(pivotCanvasY, 0f, _pixelStudio.CanvasHeight);
            UpdateSelectionTransformPreviewFromCurrentState(layout);
            return;
        }

        if (!TryGetCanvasPoint(layout, mouseX, mouseY, out float mouseCanvasPointX, out float mouseCanvasPointY))
        {
            return;
        }

        float radians = _selectionTransformPreviewRotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        InverseRotateClockwise(
            mouseCanvasPointX,
            mouseCanvasPointY,
            _selectionTransformPivotX,
            _selectionTransformPivotY,
            cos,
            sin,
            out float unrotatedMouseX,
            out float unrotatedMouseY);

        float localX = unrotatedMouseX - _selectionTransformPivotX;
        float localY = unrotatedMouseY - _selectionTransformPivotY;
        float sourceMinX = _selectionTransformSourceLeft - _selectionTransformPivotX;
        float sourceMaxX = (_selectionTransformSourceLeft + _selectionTransformSourceWidth) - _selectionTransformPivotX;
        float sourceMinY = _selectionTransformSourceTop - _selectionTransformPivotY;
        float sourceMaxY = (_selectionTransformSourceTop + _selectionTransformSourceHeight) - _selectionTransformPivotY;
        float nextScaleX = GetSelectionTransformPreviewScaleX();
        float nextScaleY = GetSelectionTransformPreviewScaleY();

        switch (_selectionTransformHandleKind)
        {
            case PixelStudioSelectionHandleKind.TopLeft:
                if (MathF.Abs(sourceMinX) > 0.0001f)
                {
                    nextScaleX = localX / sourceMinX;
                }
                if (MathF.Abs(sourceMinY) > 0.0001f)
                {
                    nextScaleY = localY / sourceMinY;
                }
                break;
            case PixelStudioSelectionHandleKind.Top:
                if (MathF.Abs(sourceMinY) > 0.0001f)
                {
                    nextScaleY = localY / sourceMinY;
                }
                break;
            case PixelStudioSelectionHandleKind.TopRight:
                if (MathF.Abs(sourceMaxX) > 0.0001f)
                {
                    nextScaleX = localX / sourceMaxX;
                }
                if (MathF.Abs(sourceMinY) > 0.0001f)
                {
                    nextScaleY = localY / sourceMinY;
                }
                break;
            case PixelStudioSelectionHandleKind.Right:
                if (MathF.Abs(sourceMaxX) > 0.0001f)
                {
                    nextScaleX = localX / sourceMaxX;
                }
                break;
            case PixelStudioSelectionHandleKind.BottomLeft:
                if (MathF.Abs(sourceMinX) > 0.0001f)
                {
                    nextScaleX = localX / sourceMinX;
                }
                if (MathF.Abs(sourceMaxY) > 0.0001f)
                {
                    nextScaleY = localY / sourceMaxY;
                }
                break;
            case PixelStudioSelectionHandleKind.Bottom:
                if (MathF.Abs(sourceMaxY) > 0.0001f)
                {
                    nextScaleY = localY / sourceMaxY;
                }
                break;
            case PixelStudioSelectionHandleKind.BottomRight:
                if (MathF.Abs(sourceMaxX) > 0.0001f)
                {
                    nextScaleX = localX / sourceMaxX;
                }
                if (MathF.Abs(sourceMaxY) > 0.0001f)
                {
                    nextScaleY = localY / sourceMaxY;
                }
                break;
            case PixelStudioSelectionHandleKind.Left:
                if (MathF.Abs(sourceMinX) > 0.0001f)
                {
                    nextScaleX = localX / sourceMinX;
                }
                break;
        }

        nextScaleX = Math.Clamp(nextScaleX, 0.02f, 64f);
        nextScaleY = Math.Clamp(nextScaleY, 0.02f, 64f);
        if (MathF.Abs(nextScaleX - _selectionTransformPreviewScaleX) <= 0.0001f
            && MathF.Abs(nextScaleY - _selectionTransformPreviewScaleY) <= 0.0001f)
        {
            return;
        }

        _selectionTransformPreviewScaleX = nextScaleX;
        _selectionTransformPreviewScaleY = nextScaleY;
        if (_selectionTransformScaleXFieldActive)
        {
            _selectionTransformScaleXBuffer = GetSelectionTransformScalePercent(horizontal: true).ToString("0.#", CultureInfo.InvariantCulture);
        }
        if (_selectionTransformScaleYFieldActive)
        {
            _selectionTransformScaleYBuffer = GetSelectionTransformScalePercent(horizontal: false).ToString("0.#", CultureInfo.InvariantCulture);
        }

        UpdateSelectionTransformPreviewFromCurrentState(layout);
    }

    private void CommitSelectionTransform()
    {
        if (!_selectionTransformPreviewVisible)
        {
            if (_selectionTransformHandleKind == PixelStudioSelectionHandleKind.Pivot)
            {
                _pixelDragMode = PixelStudioDragMode.None;
                RefreshSelectionTransformOverlay(_layoutSnapshot?.PixelStudio);
            }
            return;
        }

        int sourceLeft = _selectionTransformSourceLeft;
        int sourceTop = _selectionTransformSourceTop;
        int sourceWidth = _selectionTransformSourceWidth;
        int sourceHeight = _selectionTransformSourceHeight;
        int targetLeft = _selectionTransformPreviewLeft;
        int targetTop = _selectionTransformPreviewTop;
        int targetWidth = _selectionTransformPreviewWidth;
        int targetHeight = _selectionTransformPreviewHeight;
        float targetRotationDegrees = _selectionTransformPreviewRotationDegrees;
        float targetScaleX = GetSelectionTransformPreviewScaleX();
        float targetScaleY = GetSelectionTransformPreviewScaleY();
        PixelStudioSelectionHandleKind transformKind = _selectionTransformHandleKind;
        bool changed = targetLeft != sourceLeft
            || targetTop != sourceTop
            || targetWidth != sourceWidth
            || targetHeight != sourceHeight
            || MathF.Abs(targetRotationDegrees) > 0.01f
            || MathF.Abs(targetScaleX - 1f) > 0.0001f
            || MathF.Abs(targetScaleY - 1f) > 0.0001f;

        if (_pixelDragMode == PixelStudioDragMode.TransformSelection)
        {
            _pixelDragMode = PixelStudioDragMode.None;
        }

        ClearSelectionTransformPreview();
        if (!changed)
        {
            RefreshSelectionTransformOverlay(_layoutSnapshot?.PixelStudio);
            return;
        }

        float pivotX = _selectionTransformPivotX;
        float pivotY = _selectionTransformPivotY;
        ApplyPixelStudioChange(
            MathF.Abs(targetRotationDegrees) > 0.01f && (MathF.Abs(targetScaleX - 1f) > 0.0001f || MathF.Abs(targetScaleY - 1f) > 0.0001f)
                ? "Transformed selection."
                : MathF.Abs(targetRotationDegrees) > 0.01f
                    ? "Rotated selection."
                    : "Scaled selection.",
            () =>
        {
            float radians = targetRotationDegrees * (MathF.PI / 180f);
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            return ApplySelectionBufferTransform(
                sourceLeft,
                sourceTop,
                sourceWidth,
                sourceHeight,
                targetLeft,
                targetTop,
                targetWidth,
                targetHeight,
                (targetX, targetY, _, _) =>
                {
                    float targetCenterX = targetLeft + targetX + 0.5f;
                    float targetCenterY = targetTop + targetY + 0.5f;
                    InverseRotateClockwise(targetCenterX, targetCenterY, pivotX, pivotY, cos, sin, out float unrotatedX, out float unrotatedY);
                    float sourceCanvasX = pivotX + ((unrotatedX - pivotX) / targetScaleX);
                    float sourceCanvasY = pivotY + ((unrotatedY - pivotY) / targetScaleY);
                    if (sourceCanvasX < sourceLeft || sourceCanvasX >= sourceLeft + sourceWidth || sourceCanvasY < sourceTop || sourceCanvasY >= sourceTop + sourceHeight)
                    {
                        return (-1, -1);
                    }

                    int sourceX = Math.Clamp((int)MathF.Floor(sourceCanvasX - sourceLeft), 0, Math.Max(sourceWidth - 1, 0));
                    int sourceY = Math.Clamp((int)MathF.Floor(sourceCanvasY - sourceTop), 0, Math.Max(sourceHeight - 1, 0));
                    return (sourceX, sourceY);
                });
        }, rebuildLayout: true);
    }

    private void ClearSelectionTransformPreview()
    {
        _selectionTransformPreviewVisible = false;
        _selectionTransformHandleKind = PixelStudioSelectionHandleKind.Rotate;
        _selectionTransformSourceLeft = 0;
        _selectionTransformSourceTop = 0;
        _selectionTransformSourceWidth = 0;
        _selectionTransformSourceHeight = 0;
        _selectionTransformPreviewLeft = 0;
        _selectionTransformPreviewTop = 0;
        _selectionTransformPreviewWidth = 0;
        _selectionTransformPreviewHeight = 0;
        _selectionTransformPreviewRotationDegrees = 0f;
        _selectionTransformPreviewScaleX = 1f;
        _selectionTransformPreviewScaleY = 1f;
        ResetSelectionTransformInputEditing();
    }

    private void CancelSelectionTransformPreview()
    {
        if (_pixelDragMode == PixelStudioDragMode.TransformSelection)
        {
            _pixelDragMode = PixelStudioDragMode.None;
        }

        ClearSelectionTransformPreview();
        RefreshSelectionTransformOverlay(_layoutSnapshot?.PixelStudio);
    }

    private PixelStudioViewState BuildPixelStudioViewState(bool refreshPixelBuffers = false)
    {
        EnsurePixelStudioIndices();
        PruneCollapsedLayerGroups();
        if (_recentPaletteColors.Count == 0 && _pixelStudio.Palette.Count > 0)
        {
            ResetPaletteInteractionState();
        }
        EnsurePixelStudioPixelBuffers(refreshPixelBuffers);
        ThemeColor activeColor = GetActivePaletteColor();
        bool loopRangeActive = TryGetLoopRange(out int loopStartFrameIndex, out int loopEndFrameIndex);
        NormalizeOnionDisplayState();
        string autosaveBannerText = BuildPixelStudioAutosaveBannerText();

        return new PixelStudioViewState
        {
            DocumentName = _pixelStudio.DocumentName,
            CanvasWidth = _pixelStudio.CanvasWidth,
            CanvasHeight = _pixelStudio.CanvasHeight,
            Zoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom),
            BrushSize = _pixelStudio.BrushSize,
            BrushMode = _brushMode,
            CanvasPanX = _pixelStudio.CanvasPanX,
            CanvasPanY = _pixelStudio.CanvasPanY,
            ShowGrid = _pixelStudio.ShowGrid,
            MirrorMode = _mirrorMode,
            MirrorAxisX = GetMirrorAxisX(),
            MirrorAxisY = GetMirrorAxisY(),
            FramesPerSecond = _pixelStudio.FramesPerSecond,
            ShowOnionSkin = _pixelStudio.ShowOnionSkin,
            ShowPreviousOnion = _pixelStudio.ShowPreviousOnion,
            ShowNextOnion = _pixelStudio.ShowNextOnion,
            AllowDualOnion = _pixelStudio.AllowDualOnion,
            OnionOpacity = _pixelStudio.OnionOpacity,
            IsPlaying = _pixelStudio.IsPlaying,
            LoopRangeEnabled = loopRangeActive,
            LoopStartFrameIndex = loopStartFrameIndex,
            LoopEndFrameIndex = loopEndFrameIndex,
            PlaybackLoopMode = _pixelStudio.PlaybackLoopMode,
            CanUndo = _pixelUndoStack.Count > 0,
            CanRedo = _pixelRedoStack.Count > 0,
            ActiveTool = _pixelStudio.ActiveTool,
            RectangleRenderMode = _rectangleRenderMode,
            EllipseRenderMode = _ellipseRenderMode,
            ShapePreset = _shapePreset,
            ShapeRenderMode = _shapeRenderMode,
            ActivePaletteIndex = _pixelStudio.ActivePaletteIndex,
            ActiveColorHex = ToHex(activeColor),
            ActiveColor = activeColor,
            SecondaryColor = _secondaryPaletteColor,
            ActiveColorAlpha = activeColor.A,
            ActiveLayerOpacity = NormalizeLayerOpacity(CurrentPixelLayer.Opacity),
            ActiveLayerAlphaLocked = CurrentPixelLayer.IsAlphaLocked,
            LayerOpacityControlsVisible = _layerOpacityControlsVisible,
            ActivePaletteName = GetActivePaletteDisplayName(),
            WorkingPaletteActive = string.IsNullOrWhiteSpace(_activePixelPaletteId)
                && !string.Equals(_selectedPixelPaletteId, DefaultPaletteSelectionId, StringComparison.Ordinal),
            DefaultPaletteActive = string.IsNullOrWhiteSpace(_activePixelPaletteId)
                && string.Equals(_selectedPixelPaletteId, DefaultPaletteSelectionId, StringComparison.Ordinal),
            DefaultPaletteSelected = string.Equals(_selectedPixelPaletteId, DefaultPaletteSelectionId, StringComparison.Ordinal),
            ColorPickerMode = _pixelColorPickerMode,
            PaletteLibraryVisible = _paletteLibraryVisible,
            PalettePromptVisible = _palettePromptVisible,
            PaletteRenameActive = _paletteRenameActive,
            PaletteRenameSelected = _paletteRenameActive && IsTextSelected(EditableTextTarget.PaletteRename),
            LayerRenameActive = _layerRenameActive,
            LayerRenameSelected = _layerRenameActive && IsTextSelected(EditableTextTarget.LayerRename),
            FrameRenameActive = _frameRenameActive,
            FrameRenameSelected = _frameRenameActive && IsTextSelected(EditableTextTarget.FrameRename),
            FrameDurationFieldActive = _frameDurationFieldActive,
            FrameDurationFieldSelected = _frameDurationFieldActive && IsTextSelected(EditableTextTarget.FrameDuration),
            FrameDurationBuffer = _frameDurationBuffer,
            HoverTooltipVisible = _hoveredPixelToolTooltipVisible && !string.IsNullOrWhiteSpace(_hoveredPixelHelpTitle),
            HoverTooltipTitle = _hoveredPixelHelpTitle,
            HoverTooltipBody = _hoveredPixelHelpBody,
            HoverTooltipX = _pixelMouseX + 14f,
            HoverTooltipY = _pixelMouseY + 18f,
            HasSelection = _selectionActive,
            SelectionMode = _selectionMode,
            SelectionTransformModeActive = _selectionTransformModeActive,
            SelectionTransformAngleFieldActive = _selectionTransformAngleFieldActive,
            SelectionTransformAngleBuffer = _selectionTransformAngleBuffer,
            SelectionTransformScaleXFieldActive = _selectionTransformScaleXFieldActive,
            SelectionTransformScaleYFieldActive = _selectionTransformScaleYFieldActive,
            SelectionTransformScaleXBuffer = _selectionTransformScaleXBuffer,
            SelectionTransformScaleYBuffer = _selectionTransformScaleYBuffer,
            SelectionX = Math.Min(_selectionStartX, _selectionEndX),
            SelectionY = Math.Min(_selectionStartY, _selectionEndY),
            SelectionWidth = _selectionActive ? GetSelectionWidth() : 0,
            SelectionHeight = _selectionActive ? GetSelectionHeight() : 0,
            SelectionUsesMask = SelectionUsesMask(),
            SelectionMaskIndices = SelectionUsesMask() ? _selectionMask : new HashSet<int>(),
            SelectionTransformSourceColors = (_selectionTransformModeActive || _selectionTransformPreviewVisible) && _selectionActive
                ? BuildSelectionTransformSourceColors()
                : [],
            SelectionTransformPreviewVisible = _selectionTransformPreviewVisible,
            SelectionTransformPreviewX = _selectionTransformPreviewLeft,
            SelectionTransformPreviewY = _selectionTransformPreviewTop,
            SelectionTransformPreviewWidth = _selectionTransformPreviewWidth,
            SelectionTransformPreviewHeight = _selectionTransformPreviewHeight,
            SelectionTransformPreviewRotationDegrees = _selectionTransformPreviewRotationDegrees,
            SelectionTransformPreviewScaleX = GetSelectionTransformPreviewScaleX(),
            SelectionTransformPreviewScaleY = GetSelectionTransformPreviewScaleY(),
            SelectionTransformPivotX = float.IsFinite(_selectionTransformPivotX)
                ? _selectionTransformPivotX
                : Math.Min(_selectionStartX, _selectionEndX) + ((_selectionActive ? GetSelectionWidth() : 0) * 0.5f),
            SelectionTransformPivotY = float.IsFinite(_selectionTransformPivotY)
                ? _selectionTransformPivotY
                : Math.Min(_selectionStartY, _selectionEndY) + ((_selectionActive ? GetSelectionHeight() : 0) * 0.5f),
            HasClipboardSelection = HasSelectionClipboard(),
            ClipboardWidth = _selectionClipboardWidth,
            ClipboardHeight = _selectionClipboardHeight,
            CanvasResizeDialogVisible = _canvasResizeDialogVisible,
            CanvasResizeWidthBuffer = _canvasResizeWidthBuffer,
            CanvasResizeHeightBuffer = _canvasResizeHeightBuffer,
            CanvasResizeWidthFieldActive = _canvasResizeActiveField == CanvasResizeInputField.Width,
            CanvasResizeWidthSelected = _canvasResizeActiveField == CanvasResizeInputField.Width && IsTextSelected(EditableTextTarget.CanvasResizeWidth),
            CanvasResizeHeightFieldActive = _canvasResizeActiveField == CanvasResizeInputField.Height,
            CanvasResizeHeightSelected = _canvasResizeActiveField == CanvasResizeInputField.Height && IsTextSelected(EditableTextTarget.CanvasResizeHeight),
            CanvasResizeWouldCrop = _canvasResizeWouldCrop,
            CanvasResizeWarningText = BuildCanvasResizeWarningText(),
            CanvasResizeAnchor = _canvasResizeAnchor,
            WarningDialogVisible = _pixelWarningDialogVisible,
            WarningDialogTitle = _pixelWarningDialogTitle,
            WarningDialogMessage = _pixelWarningDialogMessage,
            WarningDialogConfirmLabel = GetPixelWarningDialogConfirmLabel(),
            WarningDialogAlternateVisible = HasPixelWarningDialogAlternateAction(),
            WarningDialogAlternateLabel = GetPixelWarningDialogAlternateLabel(),
            WarningDialogTertiaryVisible = HasPixelWarningDialogTertiaryAction(),
            WarningDialogTertiaryLabel = GetPixelWarningDialogTertiaryLabel(),
            WarningDialogCancelLabel = GetPixelWarningDialogCancelLabel(),
            WarningToastVisible = false,
            WarningToastText = string.Empty,
            PromptForPaletteGenerationAfterImport = _promptForPaletteGenerationAfterImport,
            HasUnsavedChanges = HasPixelStudioUnsavedChanges(),
            AutosaveEnabled = _pixelAutosaveIntervalSeconds > 0,
            AutosavePending = _pixelAutosaveIntervalSeconds > 0 && _pixelStudioAutosavePending,
            AutosaveAnimationEndsAtUnixMilliseconds = _pixelAutosaveIntervalSeconds > 0 && _pixelStudioAutosaveIndicatorUntil > DateTimeOffset.MinValue
                ? _pixelStudioAutosaveIndicatorUntil.ToUnixTimeMilliseconds()
                : 0L,
            AutosaveCountdownSeconds = GetPixelStudioAutosaveCountdownSeconds(),
            RecoveryBackupCount = _pixelRecoveryBackupCount,
            AutosaveBannerVisible = !string.IsNullOrWhiteSpace(autosaveBannerText),
            AutosaveBannerText = autosaveBannerText,
            RecoveryBannerVisible = _pixelRecoveryBannerVisible,
            RecoveryBannerText = BuildPixelStudioRecoveryStatusMessage(),
            ToolsPanelPreferredWidth = _pixelToolsPanelWidth,
            SidebarPreferredWidth = _pixelSidebarWidth,
            ToolsPanelCollapsed = _pixelToolsCollapsed,
            SidebarCollapsed = _pixelSidebarCollapsed,
            TimelineVisible = _pixelTimelineVisible,
            ToolSettingsPanelOffsetX = _pixelToolSettingsPanelOffsetX,
            ToolSettingsPanelOffsetY = _pixelToolSettingsPanelOffsetY,
            NavigatorVisible = _pixelNavigatorVisible,
            NavigatorPanelOffsetX = _pixelNavigatorPanelOffsetX,
            NavigatorPanelOffsetY = _pixelNavigatorPanelOffsetY,
            NavigatorPanelWidth = _pixelNavigatorPanelWidth,
            NavigatorPanelHeight = _pixelNavigatorPanelHeight,
            AnimationPreviewVisible = _pixelAnimationPreviewVisible,
            AnimationPreviewPanelOffsetX = _pixelAnimationPreviewPanelOffsetX,
            AnimationPreviewPanelOffsetY = _pixelAnimationPreviewPanelOffsetY,
            AnimationPreviewPanelWidth = _pixelAnimationPreviewPanelWidth,
            AnimationPreviewPanelHeight = _pixelAnimationPreviewPanelHeight,
            PaletteSwatchScrollRow = _paletteSwatchScrollRow,
            SavedPaletteScrollRow = _savedPaletteScrollRow,
            LayerScrollRow = _layerScrollRow,
            FrameScrollRow = _frameScrollRow,
            CollapsedLayerGroupIds = new HashSet<string>(_collapsedLayerGroupIds, StringComparer.Ordinal),
            LayerReorderActive = _pixelDragMode == PixelStudioDragMode.ReorderLayer && _layerDragMoved,
            LayerReorderSourceIndex = _layerDragSourceIndex,
            LayerReorderInsertIndex = _layerDragInsertIndex,
            LayerReorderJoinGroupId = _layerDragJoinGroupId,
            LayerReorderJoinAnchorLayerIndex = _layerDragJoinAnchorLayerIndex,
            FrameReorderActive = _pixelDragMode == PixelStudioDragMode.ReorderFrame && _frameDragMoved,
            FrameReorderSourceIndex = _frameDragSourceIndex,
            FrameReorderInsertIndex = _frameDragInsertIndex,
            PaletteReorderActive = _pixelDragMode == PixelStudioDragMode.ReorderPaletteSwatch && _paletteSwatchDragMoved,
            PaletteReorderSourceIndex = _paletteSwatchDragSourceIndex,
            PaletteReorderTargetIndex = _paletteSwatchDragTargetIndex,
            PaletteRenameBuffer = _paletteRenameBuffer,
            LayerRenameBuffer = _layerRenameBuffer,
            LayerRenameTargetsGroup = _layerRenameTargetsGroup,
            FrameRenameBuffer = _frameRenameBuffer,
            ContextMenuVisible = _pixelContextMenuVisible,
            ContextMenuX = _pixelContextMenuX,
            ContextMenuY = _pixelContextMenuY,
            ContextMenuItems = BuildPixelContextMenuItems(),
            Palette = _pixelStudio.Palette.ToList(),
            RecentColors = _recentPaletteColors.ToList(),
            CompositePixels = _pixelCompositePixels,
            OnionPreviousPixels = _pixelOnionPreviousPixels,
            OnionNextPixels = _pixelOnionNextPixels,
            PreviewPixels = _pixelPreviewPixels,
            CompositePixelsRevision = _pixelCompositeRevision,
            OnionPreviousPixelsRevision = _pixelOnionPreviousRevision,
            OnionNextPixelsRevision = _pixelOnionNextRevision,
            PreviewPixelsRevision = _pixelPreviewRevision,
            SavedPalettes = BuildSavedPaletteViews(),
            Layers = CurrentPixelFrame.Layers
                .Select((layer, index) => new PixelStudioLayerView
                {
                    Name = layer.Name,
                    GroupId = layer.GroupId,
                    GroupName = layer.GroupName,
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked,
                    IsAlphaLocked = layer.IsAlphaLocked,
                    IsSharedAcrossFrames = layer.IsSharedAcrossFrames,
                    Opacity = NormalizeLayerOpacity(layer.Opacity),
                    IsActive = index == _pixelStudio.ActiveLayerIndex
                })
                .ToList(),
            Frames = _pixelStudio.Frames
                .Select((frame, index) => new PixelStudioFrameView
                {
                    Name = frame.Name,
                    DurationMilliseconds = Math.Clamp(frame.DurationMilliseconds, 40, 1000),
                    IsActive = index == _pixelStudio.ActiveFrameIndex,
                    IsPreviewing = index == _pixelStudio.PreviewFrameIndex,
                    IsSelected = index == _pixelStudio.ActiveFrameIndex,
                    IsInLoopRange = loopRangeActive && index >= loopStartFrameIndex && index <= loopEndFrameIndex,
                    IsLoopStart = loopRangeActive && index == loopStartFrameIndex,
                    IsLoopEnd = loopRangeActive && index == loopEndFrameIndex
                })
                .ToList()
        };
    }

    private void OpenCanvasResizeDialog(int width, int height, string? status = null)
    {
        _canvasResizeDialogVisible = true;
        _canvasResizeWidthBuffer = Math.Clamp(width, 1, MaxPixelCanvasDimension).ToString();
        _canvasResizeHeightBuffer = Math.Clamp(height, 1, MaxPixelCanvasDimension).ToString();
        _canvasResizeActiveField = CanvasResizeInputField.Width;
        SelectAllText(EditableTextTarget.CanvasResizeWidth);
        UpdateCanvasResizePreviewState();
        RefreshPixelStudioView(status ?? "Opened canvas resize.", rebuildLayout: true, playWarningTone: _canvasResizeWouldCrop);
    }

    private void OpenPixelWarningDialog(
        PixelStudioWarningDialogKind kind,
        string title,
        string message,
        int resizeWidth = 0,
        int resizeHeight = 0,
        int targetLayerIndex = -1,
        int paletteSwatchIndex = -1,
        int paletteSwapTargetIndex = -1,
        int paletteTargetIndex = int.MinValue,
        Action? confirmAction = null)
    {
        EndPixelStroke();
        StopPixelPlayback();
        ClosePixelContextMenu();
        _pixelWarningDialogVisible = true;
        _pixelWarningDialogKind = kind;
        _pixelWarningDialogTitle = title;
        _pixelWarningDialogMessage = message;
        _pixelWarningTargetLayerIndex = targetLayerIndex;
        _pixelWarningResizeWidth = resizeWidth;
        _pixelWarningResizeHeight = resizeHeight;
        _pixelWarningPaletteSwatchIndex = paletteSwatchIndex;
        _pixelWarningPaletteSwapTargetIndex = paletteSwapTargetIndex;
        _pixelWarningPaletteTargetIndex = paletteTargetIndex;
        _pixelWarningConfirmAction = confirmAction;
        ClearSelectedText(EditableTextTarget.CanvasResizeWidth);
        ClearSelectedText(EditableTextTarget.CanvasResizeHeight);
        NotificationSoundPlayer.PlayWarning();
        RefreshPixelStudioView(title, rebuildLayout: true);
    }

    private void ClosePixelWarningDialog(string? status = null)
    {
        _pixelWarningDialogVisible = false;
        _pixelWarningDialogKind = PixelStudioWarningDialogKind.None;
        _pixelWarningDialogTitle = string.Empty;
        _pixelWarningDialogMessage = string.Empty;
        _pixelWarningTargetLayerIndex = -1;
        _pixelWarningResizeWidth = 0;
        _pixelWarningResizeHeight = 0;
        _pixelWarningPaletteSwatchIndex = -1;
        _pixelWarningPaletteSwapTargetIndex = -1;
        _pixelWarningPaletteTargetIndex = int.MinValue;
        _pixelWarningConfirmAction = null;
        RefreshPixelStudioView(status, rebuildLayout: true);
    }

    private void ConfirmPixelWarningDialog()
    {
        PixelStudioWarningDialogKind kind = _pixelWarningDialogKind;
        int targetLayerIndex = _pixelWarningTargetLayerIndex;
        int resizeWidth = _pixelWarningResizeWidth;
        int resizeHeight = _pixelWarningResizeHeight;
        int paletteSwatchIndex = _pixelWarningPaletteSwatchIndex;
        int paletteSwapTargetIndex = _pixelWarningPaletteSwapTargetIndex;
        int paletteTargetIndex = _pixelWarningPaletteTargetIndex;
        Action? confirmAction = _pixelWarningConfirmAction;
        _pixelWarningDialogVisible = false;
        _pixelWarningDialogKind = PixelStudioWarningDialogKind.None;
        _pixelWarningDialogTitle = string.Empty;
        _pixelWarningDialogMessage = string.Empty;
        _pixelWarningTargetLayerIndex = -1;
        _pixelWarningResizeWidth = 0;
        _pixelWarningResizeHeight = 0;
        _pixelWarningPaletteSwatchIndex = -1;
        _pixelWarningPaletteSwapTargetIndex = -1;
        _pixelWarningPaletteTargetIndex = int.MinValue;
        _pixelWarningConfirmAction = null;

        if (confirmAction is not null)
        {
            confirmAction();
            return;
        }

        switch (kind)
        {
            case PixelStudioWarningDialogKind.ResizeCanvas:
                ApplyRequestedPixelCanvasResize(resizeWidth, resizeHeight);
                break;
            case PixelStudioWarningDialogKind.ScaleSelectionUp:
                ScaleSelectionPixels(scaleUp: true, requireWarning: false);
                break;
            case PixelStudioWarningDialogKind.ScaleSelectionDown:
                ScaleSelectionPixels(scaleUp: false, requireWarning: false);
                break;
            case PixelStudioWarningDialogKind.HiddenLayerEdit:
                RevealLayerForEditing(targetLayerIndex >= 0 ? targetLayerIndex : _pixelStudio.ActiveLayerIndex);
                break;
            case PixelStudioWarningDialogKind.LockedLayerEdit:
                UnlockLayerForEditing(targetLayerIndex >= 0 ? targetLayerIndex : _pixelStudio.ActiveLayerIndex);
                break;
            case PixelStudioWarningDialogKind.AlphaLockedLayerEdit:
                DisableLayerAlphaLockForEditing(targetLayerIndex >= 0 ? targetLayerIndex : _pixelStudio.ActiveLayerIndex);
                break;
            case PixelStudioWarningDialogKind.DeleteLockedSavedPalette:
                if (paletteTargetIndex >= 0 && paletteTargetIndex < _savedPixelPalettes.Count)
                {
                    SavedPixelPalette lockedPalette = _savedPixelPalettes[paletteTargetIndex];
                    SavedPixelPalette unlockedPalette = new()
                    {
                        Id = lockedPalette.Id,
                        Name = lockedPalette.Name,
                        IsLocked = false,
                        Colors = lockedPalette.Colors
                    };
                    ReplaceSavedPalette(unlockedPalette);
                    _selectedPixelPaletteId = unlockedPalette.Id;
                    DeleteSelectedSavedPalette();
                }
                else
                {
                    RefreshPixelStudioView(rebuildLayout: true);
                }
                break;
            case PixelStudioWarningDialogKind.DeleteLockedPaletteSwatch:
                if (paletteSwatchIndex >= 0)
                {
                    UnlockLockedPaletteSource();
                    DeletePaletteSwatch(paletteSwatchIndex);
                }
                else
                {
                    RefreshPixelStudioView(rebuildLayout: true);
                }
                break;
            case PixelStudioWarningDialogKind.DeleteUsedPaletteSwatch:
                if (paletteSwatchIndex >= 0)
                {
                    if (paletteSwapTargetIndex >= 0)
                    {
                        DeletePaletteSwatchWithUnusedSwap(paletteSwatchIndex, paletteSwapTargetIndex);
                    }
                    else
                    {
                        DeletePaletteSwatchWithNearestRemap(paletteSwatchIndex);
                    }
                }
                else
                {
                    RefreshPixelStudioView(rebuildLayout: true);
                }
                break;
            case PixelStudioWarningDialogKind.ApplyPaletteWithArtworkColors:
                if (paletteTargetIndex != int.MinValue)
                {
                    SaveCurrentPaletteAndApplyPaletteTarget(paletteTargetIndex);
                }
                else
                {
                    RefreshPixelStudioView(rebuildLayout: true);
                }
                break;
            default:
                RefreshPixelStudioView(rebuildLayout: true);
                break;
        }
    }

    private void ExecuteAlternatePixelWarningDialog()
    {
        PixelStudioWarningDialogKind kind = _pixelWarningDialogKind;
        int paletteSwatchIndex = _pixelWarningPaletteSwatchIndex;
        int paletteTargetIndex = _pixelWarningPaletteTargetIndex;
        _pixelWarningDialogVisible = false;
        _pixelWarningDialogKind = PixelStudioWarningDialogKind.None;
        _pixelWarningDialogTitle = string.Empty;
        _pixelWarningDialogMessage = string.Empty;
        _pixelWarningTargetLayerIndex = -1;
        _pixelWarningResizeWidth = 0;
        _pixelWarningResizeHeight = 0;
        _pixelWarningPaletteSwatchIndex = -1;
        _pixelWarningPaletteSwapTargetIndex = -1;
        _pixelWarningPaletteTargetIndex = int.MinValue;
        _pixelWarningConfirmAction = null;

        switch (kind)
        {
            case PixelStudioWarningDialogKind.DeleteUsedPaletteSwatch:
                if (paletteSwatchIndex >= 0)
                {
                    DeletePaletteSwatchWithNearestRemap(paletteSwatchIndex);
                }
                else
                {
                    RefreshPixelStudioView(rebuildLayout: true);
                }
                break;
            case PixelStudioWarningDialogKind.ApplyPaletteWithArtworkColors:
                if (paletteTargetIndex != int.MinValue)
                {
                    ApplyPaletteSelection(paletteTargetIndex);
                }
                else
                {
                    RefreshPixelStudioView(rebuildLayout: true);
                }
                break;
            default:
                RefreshPixelStudioView(rebuildLayout: true);
                break;
        }
    }

    private void CloseCanvasResizeDialog(string? status = null)
    {
        _canvasResizeDialogVisible = false;
        _canvasResizeActiveField = CanvasResizeInputField.None;
        _canvasResizeWouldCrop = false;
        _canvasResizeCroppedPixelCount = 0;
        ClearSelectedText(EditableTextTarget.CanvasResizeWidth);
        ClearSelectedText(EditableTextTarget.CanvasResizeHeight);
        RefreshPixelStudioView(status, rebuildLayout: true);
    }

    private void AppendCanvasResizeCharacter(char character)
    {
        ref string buffer = ref GetCanvasResizeActiveBuffer();
        if (buffer.Length >= 4)
        {
            return;
        }

        if (buffer == "0")
        {
            buffer = character.ToString();
        }
        else
        {
            buffer += character;
        }

        UpdateCanvasResizePreviewState();
        RefreshPixelStudioView("Editing canvas size.", rebuildLayout: true);
    }

    private void RemoveCanvasResizeTextCharacter()
    {
        ref string buffer = ref GetCanvasResizeActiveBuffer();
        if (buffer.Length > 0)
        {
            buffer = buffer[..^1];
        }

        UpdateCanvasResizePreviewState();
        RefreshPixelStudioView("Editing canvas size.", rebuildLayout: true);
    }

    private ref string GetCanvasResizeActiveBuffer()
    {
        if (_canvasResizeActiveField == CanvasResizeInputField.Height)
        {
            return ref _canvasResizeHeightBuffer;
        }

        return ref _canvasResizeWidthBuffer;
    }

    private void UpdateCanvasResizePreviewState()
    {
        if (!TryParseCanvasResizeBuffers(out int width, out int height))
        {
            _canvasResizeWouldCrop = false;
            _canvasResizeCroppedPixelCount = 0;
            return;
        }

        _canvasResizeCroppedPixelCount = CountCroppedPixelsForResize(width, height, _canvasResizeAnchor);
        _canvasResizeWouldCrop = _canvasResizeCroppedPixelCount > 0;
    }

    private string BuildCanvasResizeWarningText()
    {
        if (!_canvasResizeDialogVisible)
        {
            return string.Empty;
        }

        if (!TryParseCanvasResizeBuffers(out int width, out int height))
        {
            return $"Use values from 1 to {MaxPixelCanvasDimension}.";
        }

        if (_canvasResizeWouldCrop)
        {
            string noun = _canvasResizeCroppedPixelCount == 1 ? "pixel" : "pixels";
            return $"{_canvasResizeCroppedPixelCount} painted {noun} will be removed.";
        }

        return $"Resize to {width}x{height} with {GetResizeAnchorLabel(_canvasResizeAnchor)} anchor.";
    }

    private bool TryParseCanvasResizeBuffers(out int width, out int height)
    {
        bool widthValid = int.TryParse(_canvasResizeWidthBuffer, out width);
        bool heightValid = int.TryParse(_canvasResizeHeightBuffer, out height);
        if (!widthValid || !heightValid)
        {
            width = 0;
            height = 0;
            return false;
        }

        return width >= 1 && width <= MaxPixelCanvasDimension && height >= 1 && height <= MaxPixelCanvasDimension;
    }

    private void ApplyCanvasResizeFromDialog()
    {
        if (!TryParseCanvasResizeBuffers(out int width, out int height))
        {
            RefreshPixelStudioView($"Canvas size must stay between 1 and {MaxPixelCanvasDimension}.", rebuildLayout: true, playWarningTone: true);
            return;
        }

        ClosePixelContextMenu();
        CloseCanvasResizeDialog();
        ResizePixelCanvas(width, height, _canvasResizeAnchor);
    }

    private void SetCanvasResizeAnchor(PixelStudioResizeAnchor anchor)
    {
        _canvasResizeAnchor = anchor;
        UpdateCanvasResizePreviewState();
        RefreshPixelStudioView($"Canvas anchor set to {GetResizeAnchorLabel(anchor)}.", rebuildLayout: true);
    }

    private static string GetResizeAnchorLabel(PixelStudioResizeAnchor anchor)
    {
        return anchor switch
        {
            PixelStudioResizeAnchor.TopLeft => "Top Left",
            PixelStudioResizeAnchor.Top => "Top",
            PixelStudioResizeAnchor.TopRight => "Top Right",
            PixelStudioResizeAnchor.Left => "Left",
            PixelStudioResizeAnchor.Center => "Center",
            PixelStudioResizeAnchor.Right => "Right",
            PixelStudioResizeAnchor.BottomLeft => "Bottom Left",
            PixelStudioResizeAnchor.Bottom => "Bottom",
            PixelStudioResizeAnchor.BottomRight => "Bottom Right",
            _ => "Top Left"
        };
    }

    private bool HandlePixelStudioTextKeyDown(Key key)
    {
        if (_pixelWarningDialogVisible)
        {
            switch (key)
            {
                case Key.Enter:
                    ConfirmPixelWarningDialog();
                    return true;
                case Key.Escape:
                    ClosePixelWarningDialog("Warning dismissed.");
                    return true;
                default:
                    return true;
            }
        }

        if (_canvasResizeDialogVisible)
        {
            switch (key)
            {
                case Key.Backspace:
                    HandleTextBackspace(
                        _canvasResizeActiveField == CanvasResizeInputField.Height
                            ? EditableTextTarget.CanvasResizeHeight
                            : EditableTextTarget.CanvasResizeWidth);
                    return true;
                case Key.Enter:
                    ApplyCanvasResizeFromDialog();
                    return true;
                case Key.Escape:
                    CloseCanvasResizeDialog("Canvas resize cancelled.");
                    return true;
                case Key.Tab:
                    _canvasResizeActiveField = _canvasResizeActiveField == CanvasResizeInputField.Width
                        ? CanvasResizeInputField.Height
                        : CanvasResizeInputField.Width;
                    SelectAllText(
                        _canvasResizeActiveField == CanvasResizeInputField.Height
                            ? EditableTextTarget.CanvasResizeHeight
                            : EditableTextTarget.CanvasResizeWidth);
                    RefreshPixelStudioView("Editing canvas size.", rebuildLayout: true);
                    return true;
                default:
                    return true;
            }
        }

        if (IsSelectionTransformInputActive())
        {
            EditableTextTarget activeTarget = _selectionTransformAngleFieldActive
                ? EditableTextTarget.TransformAngle
                : _selectionTransformScaleXFieldActive
                    ? EditableTextTarget.TransformScaleX
                    : EditableTextTarget.TransformScaleY;
            switch (key)
            {
                case Key.Backspace:
                    HandleTextBackspace(activeTarget);
                    return true;
                case Key.Tab:
                    if (_selectionTransformAngleFieldActive)
                    {
                        ActivateSelectionTransformScaleField(horizontal: true);
                    }
                    else if (_selectionTransformScaleXFieldActive)
                    {
                        ActivateSelectionTransformScaleField(horizontal: false);
                    }
                    else
                    {
                        ActivateSelectionTransformAngleField();
                    }

                    return true;
                case Key.Enter:
                    ResetSelectionTransformInputEditing();
                    if (_selectionTransformPreviewVisible)
                    {
                        CommitSelectionTransform();
                        RefreshPixelStudioView("Transform applied.", rebuildLayout: true);
                    }
                    else
                    {
                        RefreshPixelStudioView("Transform angle set.", rebuildLayout: true);
                    }

                    return true;
                case Key.Escape:
                    ResetSelectionTransformInputEditing();
                    if (_selectionTransformPreviewVisible)
                    {
                        CancelSelectionTransformPreview();
                        RefreshPixelStudioView("Transform cancelled.", rebuildLayout: true);
                    }
                    else
                    {
                        RefreshPixelStudioView("Transform entry cancelled.", rebuildLayout: true);
                    }

                    return true;
                default:
                    return true;
            }
        }

        if (_frameDurationFieldActive)
        {
            switch (key)
            {
                case Key.Backspace:
                    HandleTextBackspace(EditableTextTarget.FrameDuration);
                    return true;
                case Key.Enter:
                    CommitFrameDurationEdit();
                    return true;
                case Key.Escape:
                    CancelFrameDurationEdit();
                    return true;
                case Key.Tab:
                    CommitFrameDurationEdit();
                    return true;
                default:
                    return true;
            }
        }

        if (!_paletteRenameActive && !_layerRenameActive && !_frameRenameActive)
        {
            if (_selectionTransformPreviewVisible)
            {
                switch (key)
                {
                    case Key.Enter:
                        CommitSelectionTransform();
                        RefreshPixelStudioView("Transform applied.", rebuildLayout: true);
                        return true;
                    case Key.Escape:
                        CancelSelectionTransformPreview();
                        RefreshPixelStudioView("Transform cancelled.", rebuildLayout: true);
                        return true;
                }
            }

            if (key == Key.Delete && _selectionActive && !CurrentPixelLayer.IsLocked && !CurrentPixelLayer.IsAlphaLocked)
            {
                DeleteSelectionPixels();
                return true;
            }

            if (key == Key.Escape && _selectionActive)
            {
                ClearSelection();
                RefreshPixelStudioView("Selection cleared.", rebuildLayout: true);
                return true;
            }

            return false;
        }

        switch (key)
        {
            case Key.Backspace:
                if (_paletteRenameActive)
                {
                    HandleTextBackspace(EditableTextTarget.PaletteRename);
                }
                else if (_layerRenameActive)
                {
                    HandleTextBackspace(EditableTextTarget.LayerRename);
                }
                else if (_frameRenameActive)
                {
                    HandleTextBackspace(EditableTextTarget.FrameRename);
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
        if (_pixelWarningDialogVisible)
        {
            return true;
        }

        if (_canvasResizeDialogVisible)
        {
            if (char.IsControl(character))
            {
                return false;
            }

            if (!char.IsDigit(character))
            {
                return true;
            }

            EditableTextTarget resizeTarget = _canvasResizeActiveField == CanvasResizeInputField.Height
                ? EditableTextTarget.CanvasResizeHeight
                : EditableTextTarget.CanvasResizeWidth;
            if (ConsumeSelectedText(resizeTarget))
            {
                ref string resizeBuffer = ref GetCanvasResizeActiveBuffer();
                resizeBuffer = string.Empty;
            }

            AppendCanvasResizeCharacter(character);
            return true;
        }

        if (!IsSelectionTransformInputActive()
            && _selectionTransformModeActive
            && _selectionActive
            && _selectionCommitted
            && !char.IsControl(character)
            && (char.IsDigit(character) || character is '-' or '+' or '.'))
        {
            ActivateSelectionTransformAngleField();
        }

        if (_selectionTransformAngleFieldActive)
        {
            if (char.IsControl(character))
            {
                return false;
            }

            if ((character is not ('-' or '.' or '+')) && !char.IsDigit(character))
            {
                return true;
            }

            if (ConsumeSelectedText(EditableTextTarget.TransformAngle))
            {
                _selectionTransformAngleBuffer = string.Empty;
            }

            if (character == '+' && _selectionTransformAngleBuffer.Length > 0)
            {
                return true;
            }

            if (character == '-' && _selectionTransformAngleBuffer.Length > 0)
            {
                return true;
            }

            if (character == '.' && _selectionTransformAngleBuffer.Contains('.'))
            {
                return true;
            }

            if (_selectionTransformAngleBuffer.Length >= 7)
            {
                return true;
            }

            _selectionTransformAngleBuffer += character;
            UpdateSelectionTransformAngleFromBuffer();
            return true;
        }

        if (_selectionTransformScaleXFieldActive || _selectionTransformScaleYFieldActive)
        {
            if (char.IsControl(character))
            {
                return false;
            }

            if ((character is not ('+' or '.')) && !char.IsDigit(character))
            {
                return true;
            }

            bool horizontal = _selectionTransformScaleXFieldActive;
            EditableTextTarget target = horizontal ? EditableTextTarget.TransformScaleX : EditableTextTarget.TransformScaleY;
            string scaleBuffer = horizontal ? _selectionTransformScaleXBuffer : _selectionTransformScaleYBuffer;
            if (ConsumeSelectedText(target))
            {
                scaleBuffer = string.Empty;
            }

            if (character == '+' && scaleBuffer.Length > 0)
            {
                return true;
            }

            if (character == '.' && scaleBuffer.Contains('.'))
            {
                return true;
            }

            if (scaleBuffer.Length >= 7)
            {
                return true;
            }

            scaleBuffer += character;
            if (horizontal)
            {
                _selectionTransformScaleXBuffer = scaleBuffer;
            }
            else
            {
                _selectionTransformScaleYBuffer = scaleBuffer;
            }
            UpdateSelectionTransformScaleFromBuffer(horizontal);
            return true;
        }

        if (_frameDurationFieldActive)
        {
            if (char.IsControl(character))
            {
                return false;
            }

            if (!char.IsDigit(character))
            {
                return true;
            }

            if (ConsumeSelectedText(EditableTextTarget.FrameDuration))
            {
                _frameDurationBuffer = string.Empty;
            }

            if (_frameDurationBuffer.Length >= 4)
            {
                return true;
            }

            _frameDurationBuffer += character;
            RefreshPixelStudioView("Editing frame duration.");
            return true;
        }

        if ((!_paletteRenameActive && !_layerRenameActive && !_frameRenameActive) || char.IsControl(character))
        {
            return false;
        }

        if (_paletteRenameActive)
        {
            if (ConsumeSelectedText(EditableTextTarget.PaletteRename))
            {
                _paletteRenameBuffer = string.Empty;
            }

            _paletteRenameBuffer += character;
            RefreshPixelStudioView("Editing palette name.");
        }
        else
        if (_frameRenameActive)
        {
            if (ConsumeSelectedText(EditableTextTarget.FrameRename))
            {
                _frameRenameBuffer = string.Empty;
            }

            _frameRenameBuffer += character;
            RefreshPixelStudioView("Editing frame name.");
        }
        else
        {
            if (ConsumeSelectedText(EditableTextTarget.LayerRename))
            {
                _layerRenameBuffer = string.Empty;
            }

            _layerRenameBuffer += character;
            RefreshPixelStudioView(_layerRenameTargetsGroup ? "Editing group name." : "Editing layer name.");
        }
        return true;
    }

    private IReadOnlyList<PixelStudioContextMenuItemView> BuildPixelContextMenuItems()
    {
        if (!_pixelContextMenuVisible)
        {
            return [];
        }

        if (_contextToolMenuTool == PixelStudioToolKind.Select)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetSelectionModeBox, Label = "Box Select" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetSelectionModeAutoGlobal, Label = "Automatic - Global" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetSelectionModeAutoLocal, Label = "Automatic - Local" }
            ];
        }

        if (_contextOnionControlsActive)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetOnionModePreviousOnly, Label = "Previous Only" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetOnionModeNextOnly, Label = "Next Only" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetOnionModeBoth, Label = "Show Both" }
            ];
        }

        if (_contextToolMenuTool is PixelStudioToolKind.Pencil or PixelStudioToolKind.Eraser)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetBrushModeRound, Label = "Round Brush" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetBrushModeSquare, Label = "Hard Edge" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetBrushModePixelPerfect, Label = "Pixel Perfect" }
            ];
        }

        if (_contextToolMenuTool == PixelStudioToolKind.Rectangle)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetRectangleModeOutline, Label = "Rectangle Outline" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetRectangleModeFilled, Label = "Rectangle Fill" }
            ];
        }

        if (_contextToolMenuTool == PixelStudioToolKind.Ellipse)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetEllipseModeOutline, Label = "Ellipse Outline" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetEllipseModeFilled, Label = "Ellipse Fill" }
            ];
        }

        if (_contextToolMenuTool == PixelStudioToolKind.Shape)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeStarOutline, Label = "Star Outline" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeStarFilled, Label = "Star Fill" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeHeartOutline, Label = "Heart Outline" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeHeartFilled, Label = "Heart Fill" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeTeardropOutline, Label = "Teardrop Outline" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeTeardropFilled, Label = "Teardrop Fill" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeTriangleOutline, Label = "Triangle Outline" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeTriangleFilled, Label = "Triangle Fill" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeDiamondOutline, Label = "Diamond Outline" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.SetShapeModeDiamondFilled, Label = "Diamond Fill" }
            ];
        }

        if (_contextSelectionActive)
        {
            List<PixelStudioContextMenuItemView> items =
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.CopySelection, Label = "Copy Selection" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.CutSelection, Label = "Cut Selection", IsDestructive = true },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.FlipSelectionHorizontal, Label = "Flip Horizontal" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.FlipSelectionVertical, Label = "Flip Vertical" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RotateSelectionCounterClockwise, Label = "Rotate Counterclockwise" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RotateSelectionClockwise, Label = "Rotate Clockwise" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.ScaleSelectionDown, Label = "Scale Down /2" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.ScaleSelectionUp, Label = "Scale Up 2x" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DisableSelection, Label = "Disable Selection", IsDestructive = true }
            ];

            if (HasSelectionClipboard())
            {
                items.Insert(2, new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.PasteSelection, Label = "Paste Selection" });
            }

            return items;
        }

        if (_contextClipboardActive)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.PasteSelection, Label = "Paste Selection" }
            ];
        }

        if (_contextPaletteSwatchIndex is not null)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DeletePaletteSwatch, Label = "Delete Swatch", IsDestructive = true }
            ];
        }

        if (_contextPaletteIndex is not null)
        {
            if (_contextPaletteIndex.Value < 0)
            {
                return
                [
                    new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.ApplyPaletteToArtwork, Label = "Apply Default Palette to Artwork" }
                ];
            }

            SavedPixelPalette palette = _savedPixelPalettes[Math.Clamp(_contextPaletteIndex.Value, 0, _savedPixelPalettes.Count - 1)];
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RenamePalette, Label = "Rename Palette" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.ApplyPaletteToArtwork, Label = "Apply Palette to Artwork" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DuplicatePalette, Label = "Duplicate Palette" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.TogglePaletteLock, Label = palette.IsLocked ? "Unlock Palette" : "Lock Palette" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.ExportPalette, Label = "Export Palette" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DeletePalette, Label = "Delete Palette", IsDestructive = true }
            ];
        }

        if (_contextLayerIndex is null && !string.IsNullOrWhiteSpace(_contextLayerGroupId))
        {
            string groupId = _contextLayerGroupId;
            string groupName = GetLayerGroupDisplayName(groupId) ?? "Layer Group";
            bool collapsed = _collapsedLayerGroupIds.Contains(groupId);
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RenameLayerGroup, Label = "Rename Group" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.ToggleLayerGroupCollapsed, Label = collapsed ? "Expand Group" : "Collapse Group" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.UngroupLayerGroup, Label = $"Ungroup {groupName}", IsDestructive = true }
            ];
        }

        if (_contextLayerIndex is not null)
        {
            int layerIndex = Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1);
            PixelStudioLayerState layer = CurrentPixelFrame.Layers[layerIndex];
            List<PixelStudioContextMenuItemView> items =
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RenameLayer, Label = "Rename Layer" }
            ];

            if (string.IsNullOrWhiteSpace(layer.GroupId))
            {
                items.Add(new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.CreateLayerGroup, Label = "Create New Group" });
                if (TryGetAdjacentLayerGroupId(layerIndex, -1, out _))
                {
                    items.Add(new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.JoinLayerGroupAbove, Label = "Join Group Above" });
                }

                if (TryGetAdjacentLayerGroupId(layerIndex, 1, out _))
                {
                    items.Add(new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.JoinLayerGroupBelow, Label = "Join Group Below" });
                }
            }
            else
            {
                items.Add(new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RenameLayerGroup, Label = "Rename Group" });
                items.Add(new PixelStudioContextMenuItemView
                {
                    Action = PixelStudioContextMenuAction.ToggleLayerGroupCollapsed,
                    Label = _collapsedLayerGroupIds.Contains(layer.GroupId) ? "Expand Group" : "Collapse Group"
                });
                items.Add(new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RemoveLayerFromGroup, Label = "Remove From Group" });
                items.Add(new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.UngroupLayerGroup, Label = "Ungroup Entire Group", IsDestructive = true });
            }

            items.AddRange(
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DuplicateLayer, Label = "Duplicate Layer" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.MergeLayerDown, Label = "Merge Layer Down" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.MoveLayerUp, Label = "Move Layer Up" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.MoveLayerDown, Label = "Move Layer Down" },
                new PixelStudioContextMenuItemView
                {
                    Action = PixelStudioContextMenuAction.ToggleLayerSharedAcrossFrames,
                    Label = layer.IsSharedAcrossFrames ? "Make Layer Frame-Local" : "Share Across All Frames"
                },
                new PixelStudioContextMenuItemView
                {
                    Action = PixelStudioContextMenuAction.ToggleLayerAlphaLock,
                    Label = layer.IsAlphaLocked ? "Disable Alpha Lock" : "Enable Alpha Lock"
                },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.ToggleLayerLock, Label = layer.IsLocked ? "Unlock Layer" : "Lock Layer" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DeleteLayer, Label = "Delete Layer", IsDestructive = true }
            ]);

            return items;
        }

        if (_contextFrameIndex is not null)
        {
            return
            [
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.RenameFrame, Label = "Rename Frame" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.CopyFrame, Label = "Copy Frame" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.PasteFrame, Label = "Paste Frame" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DuplicateFrame, Label = "Duplicate Frame" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.MoveFrameLeft, Label = "Move Frame Left" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.MoveFrameRight, Label = "Move Frame Right" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.DecreaseFrameDuration, Label = "Shorter Frame" },
                new PixelStudioContextMenuItemView { Action = PixelStudioContextMenuAction.IncreaseFrameDuration, Label = "Longer Frame" },
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
                IsLocked = palette.IsLocked,
                IsActive = string.Equals(palette.Id, _activePixelPaletteId, StringComparison.Ordinal),
                IsSelected = string.Equals(palette.Id, _selectedPixelPaletteId, StringComparison.Ordinal),
                PreviewColors = palette.Colors.Take(4).Select(ToThemeColor).ToList()
            })
            .ToList();
    }

    private void OpenPaletteContextMenu(int paletteIndex, float x, float y)
    {
        _contextPaletteIndex = paletteIndex;
        _contextPaletteSwatchIndex = null;
        _contextLayerIndex = null;
        _contextLayerGroupId = null;
        _contextFrameIndex = null;
        _contextSelectionActive = false;
        _contextOnionControlsActive = false;
        _contextToolMenuTool = null;
        _contextClipboardActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        _layerRenameActive = false;
        string paletteName = paletteIndex < 0
            ? "Default Palette"
            : _savedPixelPalettes[Math.Clamp(paletteIndex, 0, _savedPixelPalettes.Count - 1)].Name;
        RefreshPixelStudioView($"Opened palette menu for {paletteName}.", rebuildLayout: true);
    }

    private void OpenPaletteSwatchContextMenu(int swatchIndex, float x, float y)
    {
        _contextPaletteSwatchIndex = Math.Clamp(swatchIndex, 0, Math.Max(_pixelStudio.Palette.Count - 1, 0));
        _contextPaletteIndex = null;
        _contextLayerIndex = null;
        _contextLayerGroupId = null;
        _contextFrameIndex = null;
        _contextSelectionActive = false;
        _contextOnionControlsActive = false;
        _contextToolMenuTool = null;
        _contextClipboardActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        RefreshPixelStudioView($"Opened swatch menu for palette color {swatchIndex + 1}.", rebuildLayout: true);
    }

    private void OpenLayerContextMenu(int layerIndex, float x, float y)
    {
        _contextLayerIndex = layerIndex;
        _contextLayerGroupId = CurrentPixelFrame.Layers[Math.Clamp(layerIndex, 0, CurrentPixelFrame.Layers.Count - 1)].GroupId;
        _contextPaletteIndex = null;
        _contextPaletteSwatchIndex = null;
        _contextFrameIndex = null;
        _contextSelectionActive = false;
        _contextOnionControlsActive = false;
        _contextToolMenuTool = null;
        _contextClipboardActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        _paletteRenameActive = false;
        RefreshPixelStudioView($"Opened layer menu for {CurrentPixelFrame.Layers[layerIndex].Name}.", rebuildLayout: true);
    }

    private void OpenLayerGroupContextMenu(string groupId, float x, float y)
    {
        _contextLayerGroupId = groupId;
        _contextLayerIndex = null;
        _contextPaletteIndex = null;
        _contextPaletteSwatchIndex = null;
        _contextFrameIndex = null;
        _contextSelectionActive = false;
        _contextOnionControlsActive = false;
        _contextToolMenuTool = null;
        _contextClipboardActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        _paletteRenameActive = false;
        RefreshPixelStudioView($"Opened group menu for {GetLayerGroupDisplayName(groupId) ?? "Layer Group"}.", rebuildLayout: true);
    }

    private void OpenFrameContextMenu(int frameIndex, float x, float y)
    {
        _contextFrameIndex = frameIndex;
        _contextLayerIndex = null;
        _contextLayerGroupId = null;
        _contextPaletteIndex = null;
        _contextPaletteSwatchIndex = null;
        _contextSelectionActive = false;
        _contextOnionControlsActive = false;
        _contextToolMenuTool = null;
        _contextClipboardActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        RefreshPixelStudioView($"Opened frame menu for {_pixelStudio.Frames[frameIndex].Name}.", rebuildLayout: true);
    }

    private void OpenSelectionContextMenu(float x, float y)
    {
        _contextFrameIndex = null;
        _contextLayerIndex = null;
        _contextLayerGroupId = null;
        _contextPaletteIndex = null;
        _contextPaletteSwatchIndex = null;
        _contextSelectionActive = true;
        _contextOnionControlsActive = false;
        _contextToolMenuTool = null;
        _contextClipboardActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        RefreshPixelStudioView("Opened selection menu.", rebuildLayout: true);
    }

    private void OpenClipboardContextMenu(float x, float y)
    {
        _contextFrameIndex = null;
        _contextLayerIndex = null;
        _contextLayerGroupId = null;
        _contextPaletteIndex = null;
        _contextPaletteSwatchIndex = null;
        _contextSelectionActive = false;
        _contextOnionControlsActive = false;
        _contextToolMenuTool = null;
        _contextClipboardActive = true;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        RefreshPixelStudioView("Opened clipboard menu.", rebuildLayout: true);
    }

    private void OpenToolContextMenu(PixelStudioToolKind tool, float x, float y)
    {
        _contextFrameIndex = null;
        _contextLayerIndex = null;
        _contextLayerGroupId = null;
        _contextPaletteIndex = null;
        _contextPaletteSwatchIndex = null;
        _contextSelectionActive = false;
        _contextOnionControlsActive = false;
        _contextToolMenuTool = tool;
        _contextClipboardActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        ActivatePixelStudioTool(tool);
        RefreshPixelStudioView($"Opened {GetPixelStudioToolStatusLabel(tool)} mode menu.", rebuildLayout: true);
    }

    private void OpenOnionContextMenu(float x, float y)
    {
        _contextFrameIndex = null;
        _contextLayerIndex = null;
        _contextLayerGroupId = null;
        _contextPaletteIndex = null;
        _contextPaletteSwatchIndex = null;
        _contextSelectionActive = false;
        _contextOnionControlsActive = true;
        _contextToolMenuTool = null;
        _contextClipboardActive = false;
        _pixelContextMenuVisible = true;
        _pixelContextMenuX = x;
        _pixelContextMenuY = y;
        RefreshPixelStudioView("Opened onion display options.", rebuildLayout: true);
    }

    private void ClosePixelContextMenu()
    {
        _pixelContextMenuVisible = false;
        _contextPaletteIndex = null;
        _contextPaletteSwatchIndex = null;
        _contextLayerIndex = null;
        _contextLayerGroupId = null;
        _contextFrameIndex = null;
        _contextSelectionActive = false;
        _contextOnionControlsActive = false;
        _contextToolMenuTool = null;
        _contextClipboardActive = false;
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
            case PixelStudioContextMenuAction.CopySelection:
                ClosePixelContextMenu();
                CopySelectionPixels();
                break;
            case PixelStudioContextMenuAction.CutSelection:
                ClosePixelContextMenu();
                CutSelectionPixels();
                break;
            case PixelStudioContextMenuAction.PasteSelection:
                ClosePixelContextMenu();
                PasteSelectionPixels();
                break;
            case PixelStudioContextMenuAction.FlipSelectionHorizontal:
                ClosePixelContextMenu();
                FlipSelectionPixels(horizontal: true);
                break;
            case PixelStudioContextMenuAction.FlipSelectionVertical:
                ClosePixelContextMenu();
                FlipSelectionPixels(horizontal: false);
                break;
            case PixelStudioContextMenuAction.RotateSelectionClockwise:
                ClosePixelContextMenu();
                RotateSelectionPixels(clockwise: true);
                break;
            case PixelStudioContextMenuAction.RotateSelectionCounterClockwise:
                ClosePixelContextMenu();
                RotateSelectionPixels(clockwise: false);
                break;
            case PixelStudioContextMenuAction.ScaleSelectionUp:
                ClosePixelContextMenu();
                ScaleSelectionPixels(scaleUp: true);
                break;
            case PixelStudioContextMenuAction.ScaleSelectionDown:
                ClosePixelContextMenu();
                ScaleSelectionPixels(scaleUp: false);
                break;
            case PixelStudioContextMenuAction.SetSelectionModeBox:
                SetSelectionMode(PixelStudioSelectionMode.Box);
                break;
            case PixelStudioContextMenuAction.SetSelectionModeAutoGlobal:
                SetSelectionMode(PixelStudioSelectionMode.AutoGlobal);
                break;
            case PixelStudioContextMenuAction.SetSelectionModeAutoLocal:
                SetSelectionMode(PixelStudioSelectionMode.AutoLocal);
                break;
            case PixelStudioContextMenuAction.SetBrushModeRound:
                SetBrushMode(PixelStudioBrushMode.Round, _contextToolMenuTool is PixelStudioToolKind.Eraser ? PixelStudioToolKind.Eraser : PixelStudioToolKind.Pencil);
                break;
            case PixelStudioContextMenuAction.SetBrushModeSquare:
                SetBrushMode(PixelStudioBrushMode.Square, _contextToolMenuTool is PixelStudioToolKind.Eraser ? PixelStudioToolKind.Eraser : PixelStudioToolKind.Pencil);
                break;
            case PixelStudioContextMenuAction.SetBrushModePixelPerfect:
                SetBrushMode(PixelStudioBrushMode.PixelPerfect, _contextToolMenuTool is PixelStudioToolKind.Eraser ? PixelStudioToolKind.Eraser : PixelStudioToolKind.Pencil);
                break;
            case PixelStudioContextMenuAction.SetOnionModePreviousOnly:
                ClosePixelContextMenu();
                _pixelStudio.ShowOnionSkin = true;
                SetOnionDisplayMode(showPrevious: true, showNext: false, allowDual: false, "Onion skin set to previous frame only.");
                break;
            case PixelStudioContextMenuAction.SetOnionModeNextOnly:
                ClosePixelContextMenu();
                _pixelStudio.ShowOnionSkin = true;
                SetOnionDisplayMode(showPrevious: false, showNext: true, allowDual: false, "Onion skin set to next frame only.");
                break;
            case PixelStudioContextMenuAction.SetOnionModeBoth:
                ClosePixelContextMenu();
                _pixelStudio.ShowOnionSkin = true;
                SetOnionDisplayMode(showPrevious: true, showNext: true, allowDual: true, "Onion skin set to show both previous and next frames.");
                break;
            case PixelStudioContextMenuAction.SetRectangleModeOutline:
                SetRectangleRenderMode(PixelStudioShapeRenderMode.Outline);
                break;
            case PixelStudioContextMenuAction.SetRectangleModeFilled:
                SetRectangleRenderMode(PixelStudioShapeRenderMode.Filled);
                break;
            case PixelStudioContextMenuAction.SetEllipseModeOutline:
                SetEllipseRenderMode(PixelStudioShapeRenderMode.Outline);
                break;
            case PixelStudioContextMenuAction.SetEllipseModeFilled:
                SetEllipseRenderMode(PixelStudioShapeRenderMode.Filled);
                break;
            case PixelStudioContextMenuAction.SetShapeModeStarOutline:
                SetShapeToolMode(PixelStudioShapePreset.Star, PixelStudioShapeRenderMode.Outline);
                break;
            case PixelStudioContextMenuAction.SetShapeModeStarFilled:
                SetShapeToolMode(PixelStudioShapePreset.Star, PixelStudioShapeRenderMode.Filled);
                break;
            case PixelStudioContextMenuAction.SetShapeModeHeartOutline:
                SetShapeToolMode(PixelStudioShapePreset.Heart, PixelStudioShapeRenderMode.Outline);
                break;
            case PixelStudioContextMenuAction.SetShapeModeHeartFilled:
                SetShapeToolMode(PixelStudioShapePreset.Heart, PixelStudioShapeRenderMode.Filled);
                break;
            case PixelStudioContextMenuAction.SetShapeModeTeardropOutline:
                SetShapeToolMode(PixelStudioShapePreset.Teardrop, PixelStudioShapeRenderMode.Outline);
                break;
            case PixelStudioContextMenuAction.SetShapeModeTeardropFilled:
                SetShapeToolMode(PixelStudioShapePreset.Teardrop, PixelStudioShapeRenderMode.Filled);
                break;
            case PixelStudioContextMenuAction.SetShapeModeTriangleOutline:
                SetShapeToolMode(PixelStudioShapePreset.Triangle, PixelStudioShapeRenderMode.Outline);
                break;
            case PixelStudioContextMenuAction.SetShapeModeTriangleFilled:
                SetShapeToolMode(PixelStudioShapePreset.Triangle, PixelStudioShapeRenderMode.Filled);
                break;
            case PixelStudioContextMenuAction.SetShapeModeDiamondOutline:
                SetShapeToolMode(PixelStudioShapePreset.Diamond, PixelStudioShapeRenderMode.Outline);
                break;
            case PixelStudioContextMenuAction.SetShapeModeDiamondFilled:
                SetShapeToolMode(PixelStudioShapePreset.Diamond, PixelStudioShapeRenderMode.Filled);
                break;
            case PixelStudioContextMenuAction.RenamePalette:
                if (_contextPaletteIndex is not null && _contextPaletteIndex.Value >= 0)
                {
                    _selectedPixelPaletteId = _savedPixelPalettes[Math.Clamp(_contextPaletteIndex.Value, 0, _savedPixelPalettes.Count - 1)].Id;
                    ClosePixelContextMenu();
                    StartPaletteRename();
                }
                break;
            case PixelStudioContextMenuAction.DuplicatePalette:
                if (_contextPaletteIndex is not null && _contextPaletteIndex.Value >= 0)
                {
                    _selectedPixelPaletteId = _savedPixelPalettes[Math.Clamp(_contextPaletteIndex.Value, 0, _savedPixelPalettes.Count - 1)].Id;
                    ClosePixelContextMenu();
                    DuplicateSelectedPalette();
                }
                break;
            case PixelStudioContextMenuAction.ApplyPaletteToArtwork:
                if (_contextPaletteIndex is not null)
                {
                    int paletteIndex = _contextPaletteIndex.Value < 0
                        ? -1
                        : Math.Clamp(_contextPaletteIndex.Value, 0, _savedPixelPalettes.Count - 1);
                    _selectedPixelPaletteId = paletteIndex < 0
                        ? DefaultPaletteSelectionId
                        : _savedPixelPalettes[paletteIndex].Id;
                    ClosePixelContextMenu();
                    ApplyPaletteToArtwork(paletteIndex);
                }
                break;
            case PixelStudioContextMenuAction.ExportPalette:
                if (_contextPaletteIndex is not null && _contextPaletteIndex.Value >= 0)
                {
                    _selectedPixelPaletteId = _savedPixelPalettes[Math.Clamp(_contextPaletteIndex.Value, 0, _savedPixelPalettes.Count - 1)].Id;
                    ClosePixelContextMenu();
                    ExportSelectedPalette();
                }
                break;
            case PixelStudioContextMenuAction.TogglePaletteLock:
                if (_contextPaletteIndex is not null && _contextPaletteIndex.Value >= 0)
                {
                    _selectedPixelPaletteId = _savedPixelPalettes[Math.Clamp(_contextPaletteIndex.Value, 0, _savedPixelPalettes.Count - 1)].Id;
                    ClosePixelContextMenu();
                    ToggleSelectedPaletteLock();
                }
                break;
            case PixelStudioContextMenuAction.DeletePalette:
                if (_contextPaletteIndex is not null && _contextPaletteIndex.Value >= 0)
                {
                    _selectedPixelPaletteId = _savedPixelPalettes[Math.Clamp(_contextPaletteIndex.Value, 0, _savedPixelPalettes.Count - 1)].Id;
                    ClosePixelContextMenu();
                    DeleteSelectedSavedPalette();
                }
                break;
            case PixelStudioContextMenuAction.DeletePaletteSwatch:
                if (_contextPaletteSwatchIndex is not null)
                {
                    int swatchIndex = Math.Clamp(_contextPaletteSwatchIndex.Value, 0, Math.Max(_pixelStudio.Palette.Count - 1, 0));
                    ClosePixelContextMenu();
                    DeletePaletteSwatch(swatchIndex);
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
            case PixelStudioContextMenuAction.CreateLayerGroup:
                if (_contextLayerIndex is not null)
                {
                    CreateLayerGroup(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1));
                }
                break;
            case PixelStudioContextMenuAction.JoinLayerGroupAbove:
                if (_contextLayerIndex is not null)
                {
                    JoinAdjacentLayerGroup(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1), -1);
                }
                break;
            case PixelStudioContextMenuAction.JoinLayerGroupBelow:
                if (_contextLayerIndex is not null)
                {
                    JoinAdjacentLayerGroup(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1), 1);
                }
                break;
            case PixelStudioContextMenuAction.RemoveLayerFromGroup:
                if (_contextLayerIndex is not null)
                {
                    RemoveLayerFromGroup(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1));
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
                    int layerIndex = Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1);
                    _pixelStudio.ActiveLayerIndex = layerIndex;
                    ClosePixelContextMenu();
                    DuplicatePixelLayer(layerIndex);
                }
                break;
            case PixelStudioContextMenuAction.MergeLayerDown:
                if (_contextLayerIndex is not null)
                {
                    int layerIndex = Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1);
                    _pixelStudio.ActiveLayerIndex = layerIndex;
                    ClosePixelContextMenu();
                    MergePixelLayerDown(layerIndex);
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
            case PixelStudioContextMenuAction.RenameLayerGroup:
                if (!string.IsNullOrWhiteSpace(_contextLayerGroupId))
                {
                    StartLayerGroupRename(_contextLayerGroupId);
                }
                break;
            case PixelStudioContextMenuAction.ToggleLayerGroupCollapsed:
                if (!string.IsNullOrWhiteSpace(_contextLayerGroupId))
                {
                    ToggleLayerGroupCollapsed(_contextLayerGroupId);
                }
                break;
            case PixelStudioContextMenuAction.UngroupLayerGroup:
                if (!string.IsNullOrWhiteSpace(_contextLayerGroupId))
                {
                    UngroupLayerGroup(_contextLayerGroupId);
                }
                break;
            case PixelStudioContextMenuAction.ToggleLayerSharedAcrossFrames:
                if (_contextLayerIndex is not null)
                {
                    TogglePixelLayerSharedAcrossFrames(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1));
                }
                break;
            case PixelStudioContextMenuAction.ToggleLayerAlphaLock:
                if (_contextLayerIndex is not null)
                {
                    TogglePixelLayerAlphaLock(Math.Clamp(_contextLayerIndex.Value, 0, CurrentPixelFrame.Layers.Count - 1));
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
            case PixelStudioContextMenuAction.CopyFrame:
                if (_contextFrameIndex is not null)
                {
                    int frameIndex = Math.Clamp(_contextFrameIndex.Value, 0, _pixelStudio.Frames.Count - 1);
                    _pixelStudio.ActiveFrameIndex = frameIndex;
                    CopyPixelFrame(frameIndex);
                }
                break;
            case PixelStudioContextMenuAction.PasteFrame:
                if (_contextFrameIndex is not null)
                {
                    int frameIndex = Math.Clamp(_contextFrameIndex.Value, 0, _pixelStudio.Frames.Count - 1);
                    _pixelStudio.ActiveFrameIndex = frameIndex;
                    PastePixelFrame(frameIndex);
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
            case PixelStudioContextMenuAction.DecreaseFrameDuration:
                if (_contextFrameIndex is not null)
                {
                    int frameIndex = Math.Clamp(_contextFrameIndex.Value, 0, _pixelStudio.Frames.Count - 1);
                    _pixelStudio.ActiveFrameIndex = frameIndex;
                    AdjustActiveFrameDuration(-20);
                }
                break;
            case PixelStudioContextMenuAction.IncreaseFrameDuration:
                if (_contextFrameIndex is not null)
                {
                    int frameIndex = Math.Clamp(_contextFrameIndex.Value, 0, _pixelStudio.Frames.Count - 1);
                    _pixelStudio.ActiveFrameIndex = frameIndex;
                    AdjustActiveFrameDuration(20);
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

    private void DeletePaletteSwatch(int paletteIndex)
    {
        if (_pixelStudio.Palette.Count == 0)
        {
            RefreshPixelStudioView("There are no swatches to delete.");
            return;
        }

        int clampedIndex = Math.Clamp(paletteIndex, 0, _pixelStudio.Palette.Count - 1);
        if (TryBlockLockedPaletteSwatchEdit(clampedIndex, "delete"))
        {
            return;
        }

        if (IsPaletteSwatchUsedInArtwork(clampedIndex))
        {
            int? unusedReplacementIndex = FindUnusedPaletteReplacementIndex(clampedIndex);
            string warningMessage = unusedReplacementIndex is not null
                ? $"Swatch {clampedIndex + 1} is already used in the artwork. Swap those pixels to free swatch {unusedReplacementIndex.Value + 1}, delete the swatch, or cancel."
                : $"Swatch {clampedIndex + 1} is already used in the artwork. There is no free replacement swatch right now, so you can only delete it or cancel.";
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.DeleteUsedPaletteSwatch,
                "Palette Swatch Is In Use",
                warningMessage,
                paletteSwatchIndex: clampedIndex,
                paletteSwapTargetIndex: unusedReplacementIndex ?? -1);
            return;
        }

        DeletePaletteSwatchWithNearestRemap(clampedIndex);
    }

    private void DeletePaletteSwatchWithNearestRemap(int paletteIndex)
    {
        ApplyPixelStudioChange($"Deleted palette swatch {paletteIndex + 1}.", () =>
        {
            if (_pixelStudio.Palette.Count == 0)
            {
                return false;
            }

            int clampedIndex = Math.Clamp(paletteIndex, 0, _pixelStudio.Palette.Count - 1);
            ThemeColor removedColor = _pixelStudio.Palette[clampedIndex];
            List<ThemeColor> updatedPalette = _pixelStudio.Palette
                .Where((_, index) => index != clampedIndex)
                .ToList();
            int nextActiveIndex = updatedPalette.Count == 0
                ? 0
                : _pixelStudio.ActivePaletteIndex == clampedIndex
                    ? Math.Clamp(clampedIndex, 0, updatedPalette.Count - 1)
                    : Math.Clamp(
                        _pixelStudio.ActivePaletteIndex > clampedIndex
                            ? _pixelStudio.ActivePaletteIndex - 1
                            : _pixelStudio.ActivePaletteIndex,
                        0,
                        updatedPalette.Count - 1);

            ReplaceCurrentPaletteColors(updatedPalette, remapArtwork: false);
            _pixelStudio.ActivePaletteIndex = nextActiveIndex;
            FinalizePaletteSwatchDeletion(clampedIndex, removedColor);
            return true;
        }, rebuildLayout: true);
    }

    private void DeletePaletteSwatchWithUnusedSwap(int paletteIndex, int replacementIndex)
    {
        ApplyPixelStudioChange($"Swapped palette swatch {paletteIndex + 1} to swatch {replacementIndex + 1} and deleted it.", () =>
        {
            if (_pixelStudio.Palette.Count == 0)
            {
                return false;
            }

            int clampedIndex = Math.Clamp(paletteIndex, 0, _pixelStudio.Palette.Count - 1);
            int clampedReplacementIndex = Math.Clamp(replacementIndex, 0, _pixelStudio.Palette.Count - 1);
            if (clampedReplacementIndex == clampedIndex)
            {
                return false;
            }

            ThemeColor removedColor = _pixelStudio.Palette[clampedIndex];
            ThemeColor replacementColor = _pixelStudio.Palette[clampedReplacementIndex];
            RecolorArtworkColor(removedColor, replacementColor);
            _pixelStudio.Palette.RemoveAt(clampedIndex);
            if (_pixelStudio.ActivePaletteIndex == clampedIndex)
            {
                _pixelStudio.ActivePaletteIndex = _pixelStudio.Palette.Count == 0
                    ? 0
                    : Math.Clamp(
                        clampedReplacementIndex > clampedIndex
                            ? clampedReplacementIndex - 1
                            : clampedReplacementIndex,
                        0,
                        _pixelStudio.Palette.Count - 1);
            }
            else if (_pixelStudio.ActivePaletteIndex > clampedIndex)
            {
                _pixelStudio.ActivePaletteIndex = Math.Max(_pixelStudio.ActivePaletteIndex - 1, 0);
            }

            FinalizePaletteSwatchDeletion(clampedIndex, removedColor);
            return true;
        }, rebuildLayout: true);
    }

    private void FinalizePaletteSwatchDeletion(int removedIndex, ThemeColor removedColor)
    {
        _pixelStudio.ActivePaletteIndex = _pixelStudio.Palette.Count == 0
            ? 0
            : Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1);
        if (_secondaryPaletteColorInitialized)
        {
            if (ColorsClose(_secondaryPaletteColor, removedColor))
            {
                _secondaryPaletteColor = GetActivePaletteColor();
            }
        }

        _secondaryPaletteColorInitialized = true;
        RememberRecentPaletteColor(GetActivePaletteColor());
        MarkCurrentPaletteAsUnsaved();
    }

    private bool IsPaletteSwatchUsedInArtwork(int paletteIndex)
    {
        if (paletteIndex < 0 || paletteIndex >= _pixelStudio.Palette.Count)
        {
            return false;
        }

        int targetPixel = PackPixelColor(_pixelStudio.Palette[paletteIndex]);
        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            foreach (PixelStudioLayerState layer in frame.Layers)
            {
                for (int pixelIndex = 0; pixelIndex < layer.Pixels.Length; pixelIndex++)
                {
                    if (layer.Pixels[pixelIndex] == targetPixel)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private int? FindUnusedPaletteReplacementIndex(int removedIndex)
    {
        HashSet<int> usedIndices = GetUsedPaletteColorIndices();
        if (removedIndex < 0 || removedIndex >= _pixelStudio.Palette.Count)
        {
            return null;
        }

        ThemeColor removedColor = _pixelStudio.Palette[removedIndex];
        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        for (int index = 0; index < _pixelStudio.Palette.Count; index++)
        {
            if (index == removedIndex || usedIndices.Contains(index))
            {
                continue;
            }

            float distance = GetPaletteColorDistance(removedColor, _pixelStudio.Palette[index]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex >= 0 ? bestIndex : null;
    }

    private HashSet<int> GetUsedPaletteColorIndices()
    {
        HashSet<int> usedIndices = [];
        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            foreach (PixelStudioLayerState layer in frame.Layers)
            {
                foreach (int pixelValue in layer.Pixels)
                {
                    if (IsTransparentPixel(pixelValue))
                    {
                        continue;
                    }

                    if (TryFindPaletteIndex(UnpackPixelColor(pixelValue)!.Value, out int paletteIndex))
                    {
                        usedIndices.Add(paletteIndex);
                    }
                }
            }
        }

        return usedIndices;
    }

    private void RecolorArtworkColor(ThemeColor sourceColor, ThemeColor replacementColor)
    {
        int sourcePixel = PackPixelColor(sourceColor);
        int replacementPixel = PackPixelColor(replacementColor);

        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            foreach (PixelStudioLayerState layer in frame.Layers)
            {
                for (int pixelIndex = 0; pixelIndex < layer.Pixels.Length; pixelIndex++)
                {
                    if (layer.Pixels[pixelIndex] == sourcePixel)
                    {
                        layer.Pixels[pixelIndex] = replacementPixel;
                    }
                }
            }
        }
    }

    private static float GetPaletteColorDistance(ThemeColor left, ThemeColor right)
    {
        float dr = left.R - right.R;
        float dg = left.G - right.G;
        float db = left.B - right.B;
        float da = left.A - right.A;
        return (dr * dr) + (dg * dg) + (db * db) + (da * da * 0.2f);
    }

    private void SetSelectionMode(PixelStudioSelectionMode mode)
    {
        _selectionMode = mode;
        ActivatePixelStudioTool(PixelStudioToolKind.Select);
        ClosePixelContextMenu();
        string modeLabel = GetSelectionModeLabel(mode);
        RefreshPixelStudioView($"Selection mode set to {modeLabel}.", rebuildLayout: true);
    }

    private void SetRectangleRenderMode(PixelStudioShapeRenderMode mode)
    {
        _rectangleRenderMode = mode;
        ActivatePixelStudioTool(PixelStudioToolKind.Rectangle);
        ClosePixelContextMenu();
        RefreshPixelStudioView($"Rectangle tool set to {GetShapeRenderModeLabel(mode)}.", rebuildLayout: true);
    }

    private void SetBrushMode(PixelStudioBrushMode mode, PixelStudioToolKind tool)
    {
        RememberBrushSizeForTool(_pixelStudio.ActiveTool, _brushMode, _pixelStudio.BrushSize);
        _pixelStudio.ActiveTool = tool;
        _brushMode = mode;
        _pixelStudio.BrushSize = GetRememberedBrushSize(tool, mode, _pixelStudio.BrushSize);
        ClosePixelContextMenu();
        RefreshPixelStudioView($"{GetBrushToolLabel(tool)} set to {GetBrushModeLabel(mode)}.", rebuildLayout: true);
    }

    private void SetEllipseRenderMode(PixelStudioShapeRenderMode mode)
    {
        _ellipseRenderMode = mode;
        ActivatePixelStudioTool(PixelStudioToolKind.Ellipse);
        ClosePixelContextMenu();
        RefreshPixelStudioView($"Ellipse tool set to {GetShapeRenderModeLabel(mode)}.", rebuildLayout: true);
    }

    private void SetShapeToolMode(PixelStudioShapePreset preset, PixelStudioShapeRenderMode mode)
    {
        _shapePreset = preset;
        _shapeRenderMode = mode;
        ActivatePixelStudioTool(PixelStudioToolKind.Shape);
        ClosePixelContextMenu();
        RefreshPixelStudioView($"Shape tool set to {GetShapePresetLabel(preset)} {GetShapeRenderModeLabel(mode)}.", rebuildLayout: true);
    }

    private string GetPixelStudioToolStatusLabel(PixelStudioToolKind tool)
    {
        return tool switch
        {
            PixelStudioToolKind.Select => _selectionMode switch
            {
                PixelStudioSelectionMode.AutoGlobal => "Select AG",
                PixelStudioSelectionMode.AutoLocal => "Select AL",
                _ => "Select"
            },
            PixelStudioToolKind.Hand => "Hand",
            PixelStudioToolKind.Pencil => GetBrushToolLabel(PixelStudioToolKind.Pencil),
            PixelStudioToolKind.Eraser => GetBrushToolLabel(PixelStudioToolKind.Eraser),
            PixelStudioToolKind.Line => "Line",
            PixelStudioToolKind.Rectangle => GetRectangleToolLabel(),
            PixelStudioToolKind.Ellipse => GetEllipseToolLabel(),
            PixelStudioToolKind.Shape => GetShapeToolLabel(),
            PixelStudioToolKind.Fill => "Fill",
            PixelStudioToolKind.Picker => "Picker",
            _ => tool.ToString()
        };
    }

    private string GetPixelToolDisplayLabel(PixelStudioToolKind tool)
    {
        return tool switch
        {
            PixelStudioToolKind.Select => _selectionMode switch
            {
                PixelStudioSelectionMode.AutoGlobal => "Select AG",
                PixelStudioSelectionMode.AutoLocal => "Select AL",
                _ => "Select"
            },
            PixelStudioToolKind.Hand => "Hand",
            PixelStudioToolKind.Pencil => GetBrushToolLabel(PixelStudioToolKind.Pencil),
            PixelStudioToolKind.Eraser => GetBrushToolLabel(PixelStudioToolKind.Eraser),
            PixelStudioToolKind.Line => "Line",
            PixelStudioToolKind.Rectangle => GetRectangleToolLabel(),
            PixelStudioToolKind.Ellipse => GetEllipseToolLabel(),
            PixelStudioToolKind.Shape => GetShapeToolLabel(),
            PixelStudioToolKind.Fill => "Fill",
            PixelStudioToolKind.Picker => "Picker",
            _ => tool.ToString()
        };
    }

    private string GetPixelToolTooltipBody(PixelStudioToolKind tool)
    {
        return tool switch
        {
            PixelStudioToolKind.Select => _selectionMode switch
            {
                PixelStudioSelectionMode.AutoGlobal => "Pick one color across the full layer.",
                PixelStudioSelectionMode.AutoLocal => "Pick touching pixels of the same color.",
                _ => "Drag a rectangular selection. Right-click to change mode."
            },
            PixelStudioToolKind.Hand => "Pan the canvas or drag an active selection.",
            PixelStudioToolKind.Pencil => GetBrushToolTooltipBody(erase: false),
            PixelStudioToolKind.Eraser => GetBrushToolTooltipBody(erase: true),
            PixelStudioToolKind.Line => "Drag to preview and place a straight line.",
            PixelStudioToolKind.Rectangle => _rectangleRenderMode == PixelStudioShapeRenderMode.Filled
                ? "Drag to preview and place a filled box. Hold Shift for a square. Hold Alt for center-draw. Right-click to change mode."
                : "Drag to preview and place a box outline. Hold Shift for a square. Hold Alt for center-draw. Right-click to change mode.",
            PixelStudioToolKind.Ellipse => _ellipseRenderMode == PixelStudioShapeRenderMode.Filled
                ? "Drag to preview and place a filled ellipse. Hold Shift for a circle. Hold Alt for center-draw. Right-click to change mode."
                : "Drag to preview and place an ellipse outline. Hold Shift for a circle. Hold Alt for center-draw. Right-click to change mode.",
            PixelStudioToolKind.Shape => $"Drag to preview and place a {_shapeRenderMode switch { PixelStudioShapeRenderMode.Filled => "filled", _ => "outlined" }} {GetShapePresetLabel(_shapePreset).ToLowerInvariant()}. Hold Shift for even proportions, Alt for center-draw, and right-click to switch shapes.",
            PixelStudioToolKind.Fill => "Fill a region or the active selection.",
            PixelStudioToolKind.Picker => "Sample a color from the active layer.",
            _ => "Tool"
        };
    }

    private string GetPixelStudioActionDisplayLabel(PixelStudioAction action)
    {
        return action switch
        {
            PixelStudioAction.CycleMirrorMode => _mirrorMode switch
            {
                PixelStudioMirrorMode.Horizontal => "Mirror H",
                PixelStudioMirrorMode.Vertical => "Mirror V",
                PixelStudioMirrorMode.Both => "Mirror HV",
                _ => "Mirror"
            },
            _ => GetPixelStudioActionLabel(action)
        };
    }

    private string GetPixelStudioActionLabel(PixelStudioAction action)
    {
        return action switch
        {
            PixelStudioAction.NewBlankDocument => "New Sprite",
            PixelStudioAction.SaveProjectDocument => "Save",
            PixelStudioAction.LoadProjectDocument => "Open",
            PixelStudioAction.LoadDemoDocument => "Demo",
            PixelStudioAction.ImportImage => "Import",
            PixelStudioAction.ExportSpriteStrip => "Strip",
            PixelStudioAction.ExportPngSequence => "PNGs",
            PixelStudioAction.ExportGif => "GIF",
            PixelStudioAction.ToggleNavigatorPanel => "Nav",
            PixelStudioAction.ToggleAnimationPreviewPanel => "Preview",
            PixelStudioAction.ToggleOnionSkin => "Onion",
            PixelStudioAction.ToggleOnionPrevious => "Prev",
            PixelStudioAction.ToggleOnionNext => "Next",
            PixelStudioAction.ClearSelection => "Deselect",
            PixelStudioAction.ToggleSelectionTransformMode => "Transform",
            PixelStudioAction.ActivateTransformAngleField => "Angle",
            PixelStudioAction.CopySelection => "Copy",
            PixelStudioAction.CutSelection => "Cut",
            PixelStudioAction.PasteSelection => "Paste",
            PixelStudioAction.FlipSelectionHorizontal => "Flip H",
            PixelStudioAction.FlipSelectionVertical => "Flip V",
            PixelStudioAction.RotateSelectionClockwise => "Rot+",
            PixelStudioAction.RotateSelectionCounterClockwise => "Rot-",
            PixelStudioAction.ScaleSelectionUp => "2x",
            PixelStudioAction.ScaleSelectionDown => "/2",
            PixelStudioAction.ConfirmWarningDialog => "Continue",
            PixelStudioAction.AlternateWarningDialog => "Delete",
            PixelStudioAction.TertiaryWarningDialog => "Save & Swap",
            PixelStudioAction.CancelWarningDialog => "Cancel",
            PixelStudioAction.OpenCanvasResizeDialog => "Custom",
            PixelStudioAction.ResizeCanvas16 => "16px",
            PixelStudioAction.ResizeCanvas32 => "32px",
            PixelStudioAction.ResizeCanvas64 => "64px",
            PixelStudioAction.ResizeCanvas128 => "128px",
            PixelStudioAction.ResizeCanvas256 => "256px",
            PixelStudioAction.ResizeCanvas512 => "512px",
            PixelStudioAction.ActivateCanvasResizeWidthField => "Width",
            PixelStudioAction.ActivateCanvasResizeHeightField => "Height",
            PixelStudioAction.SetCanvasResizeAnchorTopLeft => "TL",
            PixelStudioAction.SetCanvasResizeAnchorTop => "T",
            PixelStudioAction.SetCanvasResizeAnchorTopRight => "TR",
            PixelStudioAction.SetCanvasResizeAnchorLeft => "L",
            PixelStudioAction.SetCanvasResizeAnchorCenter => "C",
            PixelStudioAction.SetCanvasResizeAnchorRight => "R",
            PixelStudioAction.SetCanvasResizeAnchorBottomLeft => "BL",
            PixelStudioAction.SetCanvasResizeAnchorBottom => "B",
            PixelStudioAction.SetCanvasResizeAnchorBottomRight => "BR",
            PixelStudioAction.ApplyCanvasResize => "Apply",
            PixelStudioAction.CancelCanvasResize => "Cancel",
            PixelStudioAction.ZoomOut => "-",
            PixelStudioAction.ZoomIn => "+",
            PixelStudioAction.ToggleGrid => "Grid",
            PixelStudioAction.CycleMirrorMode => "Mirror",
            PixelStudioAction.FitCanvas => "Fit",
            PixelStudioAction.ResetView => "Reset",
            PixelStudioAction.ExportPng => "Export",
            PixelStudioAction.DecreaseBrushSize => "Brush -",
            PixelStudioAction.IncreaseBrushSize => "Brush +",
            PixelStudioAction.DockToolSettingsLeft => "Dock Left",
            PixelStudioAction.DockToolSettingsRight => "Dock Right",
            PixelStudioAction.TogglePaletteLibrary => "Library",
            PixelStudioAction.ToggleTimelinePanel => "Frames",
            PixelStudioAction.NewBlankPalette => "Blank",
            PixelStudioAction.AddPaletteSwatch => "Add",
            PixelStudioAction.GeneratePaletteRamp => "Ramp",
            PixelStudioAction.SaveCurrentPalette => "Save",
            PixelStudioAction.UpdateSelectedPalette => "Update",
            PixelStudioAction.DuplicateSelectedPalette => "Dup",
            PixelStudioAction.ImportPalette => "Import",
            PixelStudioAction.ExportPalette => "Export",
            PixelStudioAction.GeneratePaletteFromImage => "Image",
            PixelStudioAction.RenameSelectedPalette => "Rename",
            PixelStudioAction.DeleteSelectedPalette => "Delete",
            PixelStudioAction.PalettePromptGenerate => "Yes",
            PixelStudioAction.PalettePromptDismiss => "No",
            PixelStudioAction.PalettePromptDismissForever => "Don't Ask",
            PixelStudioAction.SwapSecondaryColor => "Swap",
            PixelStudioAction.DecreaseRed => "R-",
            PixelStudioAction.IncreaseRed => "R+",
            PixelStudioAction.DecreaseGreen => "G-",
            PixelStudioAction.IncreaseGreen => "G+",
            PixelStudioAction.DecreaseBlue => "B-",
            PixelStudioAction.IncreaseBlue => "B+",
            PixelStudioAction.AddLayer => "Layer +",
            PixelStudioAction.ToggleLayerOpacityControls => "Opacity",
            PixelStudioAction.ToggleLayerAlphaLock => "Alpha",
            PixelStudioAction.DeleteLayer => "Layer -",
            PixelStudioAction.AddFrame => "Frame +",
            PixelStudioAction.DuplicateFrame => "Dup",
            PixelStudioAction.CopyFrame => "Copy",
            PixelStudioAction.PasteFrame => "Paste",
            PixelStudioAction.DeleteFrame => "Frame -",
            PixelStudioAction.TogglePlayback => "Play",
            PixelStudioAction.DecreaseFrameRate => "FPS -",
            PixelStudioAction.IncreaseFrameRate => "FPS +",
            PixelStudioAction.SetLoopStart => "Loop In",
            PixelStudioAction.SetLoopEnd => "Loop Out",
            PixelStudioAction.CyclePlaybackLoopMode => _pixelStudio.PlaybackLoopMode == PixelStudioPlaybackLoopMode.PingPong ? "Bounce" : "Fwd",
            PixelStudioAction.DecreaseFrameDuration => "Dur -",
            PixelStudioAction.IncreaseFrameDuration => "Dur +",
            _ => action.ToString()
        };
    }

    private string GetPixelStudioActionTooltipBody(PixelStudioAction action)
    {
        return action switch
        {
            PixelStudioAction.NewBlankDocument => "Start a new blank sprite document.",
            PixelStudioAction.SaveProjectDocument => "Save the current Kearu document to disk.",
            PixelStudioAction.LoadProjectDocument => "Open a saved Kearu document.",
            PixelStudioAction.LoadDemoDocument => "Load the bundled demo artwork.",
            PixelStudioAction.ImportImage => "Import an image into the active layer.",
            PixelStudioAction.ExportPng => "Export the active frame as a PNG.",
            PixelStudioAction.ExportSpriteStrip => "Export all frames as one horizontal sprite strip.",
            PixelStudioAction.ExportPngSequence => "Export every frame as separate PNG files.",
            PixelStudioAction.ExportGif => "Export the animation as a GIF using current frame timing.",
            PixelStudioAction.ToggleGrid => "Show or hide the pixel grid overlay.",
            PixelStudioAction.CycleMirrorMode => "Cycle mirror drawing between off, horizontal, vertical, and both axes.",
            PixelStudioAction.FitCanvas => "Fit the full canvas into the current view.",
            PixelStudioAction.ResetView => "Reset zoom and pan to the default canvas view.",
            PixelStudioAction.TogglePaletteLibrary => "Show or hide the saved palette library.",
            PixelStudioAction.NewBlankPalette => "Create a new blank saved palette and make it the working palette.",
            PixelStudioAction.AddPaletteSwatch => "Add the current color as a new palette swatch.",
            PixelStudioAction.GeneratePaletteRamp => "Generate intermediate swatches between the active and secondary colors.",
            PixelStudioAction.GeneratePaletteFromImage => "Build a palette from the most recently imported image.",
            PixelStudioAction.SwapSecondaryColor => "Make the secondary color active. If it is not in the palette, Kuma adds it as a new swatch and keeps your previous active color as the secondary color.",
            PixelStudioAction.DecreaseRed => "Lower the red channel of the active color.",
            PixelStudioAction.IncreaseRed => "Raise the red channel of the active color.",
            PixelStudioAction.DecreaseGreen => "Lower the green channel of the active color.",
            PixelStudioAction.IncreaseGreen => "Raise the green channel of the active color.",
            PixelStudioAction.DecreaseBlue => "Lower the blue channel of the active color.",
            PixelStudioAction.IncreaseBlue => "Raise the blue channel of the active color.",
            PixelStudioAction.SaveCurrentPalette => "Save the current working palette to the library.",
            PixelStudioAction.UpdateSelectedPalette => "Overwrite the selected saved palette with the current working swatches.",
            PixelStudioAction.DuplicateSelectedPalette => "Duplicate the selected saved palette.",
            PixelStudioAction.ImportPalette => "Import a palette file into the library.",
            PixelStudioAction.ExportPalette => "Export the selected palette to a file.",
            PixelStudioAction.RenameSelectedPalette => "Rename the selected saved palette.",
            PixelStudioAction.DeleteSelectedPalette => "Delete the selected saved palette from the library.",
            PixelStudioAction.AddLayer => "Create a new layer above the active one.",
            PixelStudioAction.ToggleLayerOpacityControls => "Show or hide the active layer opacity slider.",
            PixelStudioAction.ToggleLayerAlphaLock => "Only allow recoloring existing painted pixels on the active layer.",
            PixelStudioAction.DeleteLayer => "Delete the active layer.",
            PixelStudioAction.TogglePlayback => "Preview the animation using the current frame timing.",
            PixelStudioAction.ToggleOnionSkin => "Show neighboring frames as ghosted onion-skin guides. Right-click for previous, next, or both.",
            PixelStudioAction.ToggleOnionPrevious => "Show the previous frame in the onion skin. Right-click to choose previous, next, or both.",
            PixelStudioAction.ToggleOnionNext => "Show the next frame in the onion skin. Right-click to choose previous, next, or both.",
            PixelStudioAction.DecreaseFrameRate => "Lower the global animation FPS and update frame timing.",
            PixelStudioAction.IncreaseFrameRate => "Raise the global animation FPS and update frame timing.",
            PixelStudioAction.SetLoopStart => "Use the active frame as the point where loop playback begins. Click it again on the same frame to clear that start boundary.",
            PixelStudioAction.SetLoopEnd => "Use the active frame as the point where loop playback jumps back. Click it again on the same frame to clear that end boundary.",
            PixelStudioAction.CyclePlaybackLoopMode => "Switch between Forward looping and Bounce (ping-pong) playback for the current frame range.",
            PixelStudioAction.AddFrame => "Add a new frame after the current one.",
            PixelStudioAction.DuplicateFrame => "Duplicate the active frame.",
            PixelStudioAction.CopyFrame => "Copy the active frame to the frame clipboard.",
            PixelStudioAction.PasteFrame => "Paste the copied frame after the active one.",
            PixelStudioAction.DeleteFrame => "Delete the active frame.",
            PixelStudioAction.ClearSelection => "Remove the current selection without deleting pixels.",
            PixelStudioAction.ToggleSelectionTransformMode => "Enter transform mode for the active selection.",
            PixelStudioAction.CopySelection => "Copy the current selection.",
            PixelStudioAction.CutSelection => "Cut the current selection to the clipboard.",
            PixelStudioAction.PasteSelection => "Paste the clipboard selection onto the active layer.",
            PixelStudioAction.FlipSelectionHorizontal => "Flip the active selection horizontally.",
            PixelStudioAction.FlipSelectionVertical => "Flip the active selection vertically.",
            PixelStudioAction.RotateSelectionClockwise => "Rotate the active selection 90 degrees clockwise.",
            PixelStudioAction.RotateSelectionCounterClockwise => "Rotate the active selection 90 degrees counterclockwise.",
            PixelStudioAction.ScaleSelectionUp => "Scale the active selection up by 2x.",
            PixelStudioAction.ScaleSelectionDown => "Scale the active selection down by half.",
            _ => "Pixel Studio control."
        };
    }

    private static string GetSelectionModeLabel(PixelStudioSelectionMode mode)
    {
        return mode switch
        {
            PixelStudioSelectionMode.AutoGlobal => "Automatic - Global",
            PixelStudioSelectionMode.AutoLocal => "Automatic - Local",
            _ => "Box Select"
        };
    }

    private string GetRectangleToolLabel()
        => _rectangleRenderMode == PixelStudioShapeRenderMode.Filled ? "Rectangle Fill" : "Rectangle";

    private string GetEllipseToolLabel()
        => _ellipseRenderMode == PixelStudioShapeRenderMode.Filled ? "Ellipse Fill" : "Ellipse";

    private string GetBrushToolLabel(PixelStudioToolKind tool)
    {
        string prefix = tool == PixelStudioToolKind.Eraser ? "Eraser" : "Pencil";
        return _brushMode switch
        {
            PixelStudioBrushMode.Square => $"{prefix} Hard",
            PixelStudioBrushMode.PixelPerfect => $"{prefix} PP",
            _ => prefix
        };
    }

    private static string GetBrushModeLabel(PixelStudioBrushMode mode)
    {
        return mode switch
        {
            PixelStudioBrushMode.Square => "Hard Edge",
            PixelStudioBrushMode.PixelPerfect => "Pixel Perfect",
            _ => "Round Brush"
        };
    }

    private string GetBrushToolTooltipBody(bool erase)
    {
        string verb = erase ? "Erase" : "Paint";
        return _brushMode switch
        {
            PixelStudioBrushMode.Square => $"{verb} with a hard edge square brush. Right-click to change brush mode.",
            PixelStudioBrushMode.PixelPerfect => $"{verb} with a pixel-perfect 1px brush that cleans corner doubles. Best at 1px. Right-click to change brush mode.",
            _ => $"{verb} with a round brush using the current color and brush size. Right-click to change brush mode."
        };
    }

    private string GetShapeToolLabel()
        => _shapeRenderMode == PixelStudioShapeRenderMode.Filled
            ? $"{GetShapePresetLabel(_shapePreset)} Fill"
            : GetShapePresetLabel(_shapePreset);

    private static string GetShapePresetLabel(PixelStudioShapePreset preset)
    {
        return preset switch
        {
            PixelStudioShapePreset.Heart => "Heart",
            PixelStudioShapePreset.Teardrop => "Teardrop",
            PixelStudioShapePreset.Triangle => "Triangle",
            PixelStudioShapePreset.Diamond => "Diamond",
            _ => "Star"
        };
    }

    private static string GetShapeRenderModeLabel(PixelStudioShapeRenderMode mode)
        => mode == PixelStudioShapeRenderMode.Filled ? "fill mode" : "outline mode";

    private void SelectAndApplySavedPalette(int index)
    {
        SelectPaletteLibraryRow(index);
        ApplyPaletteSelection(index);
    }

    private void ApplyPaletteSelection(int index)
    {
        if (!TryGetPaletteTarget(index, out string paletteName, out _, out _))
        {
            return;
        }

        string status = index < 0
            ? "Applied the default palette."
            : $"Applied palette {paletteName}.";
        if (!ApplyPaletteTarget(index))
        {
            RefreshPixelStudioView(rebuildLayout: true);
            return;
        }

        RefreshPixelStudioView(status, rebuildLayout: true);
    }

    private void ApplyPaletteToArtwork(int index)
    {
        if (!TryGetPaletteTarget(index, out string paletteName, out List<ThemeColor> targetPaletteColors, out SavedPixelPalette? savedPalette))
        {
            RefreshPixelStudioView(rebuildLayout: true);
            return;
        }

        if (targetPaletteColors.Count == 0)
        {
            RefreshPixelStudioView($"{paletteName} is empty, so there are no palette colors to apply to the artwork.");
            return;
        }

        ApplyPixelStudioChange($"Applied {paletteName} to the artwork.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                foreach (PixelStudioLayerState layer in frame.Layers)
                {
                    for (int pixelIndex = 0; pixelIndex < layer.Pixels.Length; pixelIndex++)
                    {
                        ThemeColor? sourceColor = UnpackPixelColor(layer.Pixels[pixelIndex]);
                        if (sourceColor is null)
                        {
                            continue;
                        }

                        int nearestPaletteIndex = FindNearestPaletteIndex(sourceColor.Value, targetPaletteColors);
                        layer.Pixels[pixelIndex] = PackPixelColor(targetPaletteColors[nearestPaletteIndex]);
                    }
                }
            }

            if (savedPalette is not null)
            {
                ApplySavedPalette(savedPalette);
            }
            else
            {
                ApplyDefaultPalette();
            }

            return true;
        }, rebuildLayout: true);
    }

    private bool TryGetPaletteTarget(int index, out string paletteName, out List<ThemeColor> paletteColors, out SavedPixelPalette? savedPalette)
    {
        if (index < 0)
        {
            paletteName = "Default Palette";
            paletteColors = CreateDefaultPalette();
            savedPalette = null;
            return true;
        }

        if (index >= _savedPixelPalettes.Count)
        {
            paletteName = string.Empty;
            paletteColors = [];
            savedPalette = null;
            return false;
        }

        savedPalette = _savedPixelPalettes[index];
        paletteName = savedPalette.Name;
        paletteColors = savedPalette.Colors.Select(ToThemeColor).ToList();
        return true;
    }

    private bool TryOpenPaletteArtworkColorWarning(int index)
    {
        if (!TryGetPaletteTarget(index, out string paletteName, out List<ThemeColor> targetPaletteColors, out _))
        {
            return false;
        }

        List<ThemeColor> missingArtworkColors = GetMissingArtworkColorsForPalette(targetPaletteColors);
        if (missingArtworkColors.Count == 0)
        {
            return false;
        }

        string exampleText = missingArtworkColors.Count switch
        {
            1 => ToHex(missingArtworkColors[0]),
            2 => $"{ToHex(missingArtworkColors[0])} and {ToHex(missingArtworkColors[1])}",
            _ => $"{ToHex(missingArtworkColors[0])}, {ToHex(missingArtworkColors[1])}, and more"
        };
        string colorCountLabel = missingArtworkColors.Count == 1 ? "color" : "colors";
        string warningMessage =
            $"{paletteName} is missing {missingArtworkColors.Count} used artwork {colorCountLabel}, including {exampleText}. " +
            $"Save & Swap, saves your current palette to the library and then applies {paletteName}. " +
            $"Apply Palette, remaps the artwork into {paletteName}. " +
            "Cancel, keeps the current palette unchanged.";
        OpenPixelWarningDialog(
            PixelStudioWarningDialogKind.ApplyPaletteWithArtworkColors,
            "Palette Change Would Recolor Artwork",
            warningMessage,
            paletteTargetIndex: index);
        return true;
    }

    private List<ThemeColor> GetMissingArtworkColorsForPalette(IReadOnlyList<ThemeColor> targetPaletteColors)
    {
        List<ThemeColor> missingColors = [];
        foreach (ThemeColor usedColor in GetUsedArtworkColors())
        {
            bool existsInTargetPalette = targetPaletteColors.Any(color => ColorsClose(color, usedColor));
            bool alreadyTracked = missingColors.Any(color => ColorsClose(color, usedColor));
            if (!existsInTargetPalette && !alreadyTracked)
            {
                missingColors.Add(usedColor);
            }
        }

        return missingColors;
    }

    private IReadOnlyList<ThemeColor> GetUsedArtworkColors()
    {
        List<ThemeColor> colors = [];
        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            foreach (PixelStudioLayerState layer in frame.Layers)
            {
                foreach (int pixelValue in layer.Pixels)
                {
                    ThemeColor? color = UnpackPixelColor(pixelValue);
                    if (color is null)
                    {
                        continue;
                    }

                    if (!colors.Any(existing => ColorsClose(existing, color.Value)))
                    {
                        colors.Add(color.Value);
                    }
                }
            }
        }

        return colors;
    }

    private bool ApplyPaletteTarget(int index)
    {
        if (!TryGetPaletteTarget(index, out _, out _, out SavedPixelPalette? savedPalette))
        {
            return false;
        }

        if (savedPalette is not null)
        {
            ApplySavedPalette(savedPalette);
            return true;
        }

        ApplyDefaultPalette();
        return true;
    }

    private string SaveCurrentPaletteSnapshot(string? preferredName = null)
    {
        string baseName = SanitizePaletteName(preferredName ?? BuildNextPaletteName());
        string name = baseName;
        int suffix = 2;
        while (_savedPixelPalettes.Any(palette => string.Equals(palette.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            name = $"{baseName} {suffix}";
            suffix++;
        }

        SavedPixelPalette palette = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            IsLocked = false,
            Colors = _pixelStudio.Palette.Select(ToPaletteColorSetting).ToList()
        };

        _savedPixelPalettes.Add(palette);
        return palette.Name;
    }

    private void SaveCurrentPaletteAndApplyPaletteTarget(int index)
    {
        if (!TryGetPaletteTarget(index, out string paletteName, out _, out _))
        {
            RefreshPixelStudioView(rebuildLayout: true);
            return;
        }

        string snapshotName = SaveCurrentPaletteSnapshot($"{GetActivePaletteDisplayName()} Backup");
        ApplyPaletteSelection(index);
        RefreshPixelStudioView($"Saved palette {snapshotName}, then applied {paletteName}.", rebuildLayout: true);
    }

    private void SelectPaletteLibraryRow(int index)
    {
        if (index < 0)
        {
            _selectedPixelPaletteId = DefaultPaletteSelectionId;
            _paletteRenameBuffer = "Default Palette";
            return;
        }

        if (index >= _savedPixelPalettes.Count)
        {
            return;
        }

        SavedPixelPalette palette = _savedPixelPalettes[index];
        _selectedPixelPaletteId = palette.Id;
        _paletteRenameBuffer = palette.Name;
    }

    private void SaveCurrentPalette()
    {
        string name = SaveCurrentPaletteSnapshot();
        SavedPixelPalette palette = _savedPixelPalettes[^1];
        _activePixelPaletteId = palette.Id;
        _selectedPixelPaletteId = palette.Id;
        ClearLockedPaletteProtection();
        ClosePixelContextMenu();
        _paletteRenameBuffer = palette.Name;
        RefreshPixelStudioView($"Saved palette {name}.", rebuildLayout: true);
    }

    private void CreateBlankPalette()
    {
        string name = SanitizePaletteName(BuildNextPaletteName());
        SavedPixelPalette palette = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            IsLocked = false,
            Colors = []
        };

        ReplaceSavedPalette(palette);
        ReplaceCurrentPaletteColors([], remapArtwork: false);
        _activePixelPaletteId = palette.Id;
        _selectedPixelPaletteId = palette.Id;
        _paletteRenameActive = false;
        _paletteRenameBuffer = palette.Name;
        ClearLockedPaletteProtection();
        ClosePixelContextMenu();
        MarkPixelStudioRecoveryDirty();
        RefreshPixelStudioView($"Created blank palette {palette.Name}.", rebuildLayout: true);
    }

    private void UpdateSelectedPalette()
    {
        if (string.Equals(_selectedPixelPaletteId, DefaultPaletteSelectionId, StringComparison.Ordinal))
        {
            RefreshPixelStudioView("Default Palette is fixed. Save a new palette instead.");
            return;
        }

        SavedPixelPalette? selected = GetSelectedSavedPalette();
        if (selected is null)
        {
            RefreshPixelStudioView("Select a saved palette to update.");
            return;
        }

        if (selected.IsLocked)
        {
            SavedPixelPalette lockedPalette = selected;
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.ModifyLockedPalette,
                "Palette Is Locked",
                $"{lockedPalette.Name} is locked. Unlock the palette, update it with the current swatches, or cancel.",
                confirmAction: () =>
                {
                    ReplaceSavedPalette(new SavedPixelPalette
                    {
                        Id = lockedPalette.Id,
                        Name = lockedPalette.Name,
                        IsLocked = false,
                        Colors = lockedPalette.Colors
                            .Select(color => new PaletteColorSetting
                            {
                                R = color.R,
                                G = color.G,
                                B = color.B,
                                A = color.A
                            })
                            .ToList()
                    });

                    if (string.Equals(_lockedPixelPaletteSourceId, lockedPalette.Id, StringComparison.Ordinal))
                    {
                        ClearLockedPaletteProtection();
                    }

                    _selectedPixelPaletteId = lockedPalette.Id;
                    UpdateSelectedPalette();
                });
            return;
        }

        SavedPixelPalette updated = new()
        {
            Id = selected.Id,
            Name = selected.Name,
            IsLocked = false,
            Colors = _pixelStudio.Palette.Select(ToPaletteColorSetting).ToList()
        };

        ReplaceSavedPalette(updated);
        _activePixelPaletteId = updated.Id;
        _selectedPixelPaletteId = updated.Id;
        _paletteRenameActive = false;
        _paletteRenameBuffer = updated.Name;
        ClearLockedPaletteProtection();
        ClosePixelContextMenu();
        RefreshPixelStudioView($"Updated palette {updated.Name}.", rebuildLayout: true);
    }

    private void GeneratePaletteRamp()
    {
        if (_pixelStudio.Palette.Count == 0)
        {
            RefreshPixelStudioView("There is no active palette to build a ramp from.");
            return;
        }

        if (IsCurrentPaletteLocked())
        {
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.ModifyLockedPalette,
                "Palette Is Locked",
                $"{GetLockedPaletteSourceName()} is locked. Unlock the palette, generate a ramp, or cancel.",
                confirmAction: () =>
                {
                    if (!UnlockLockedPaletteSource())
                    {
                        RefreshPixelStudioView("Could not unlock the current palette.", rebuildLayout: true);
                        return;
                    }

                    GeneratePaletteRamp();
                });
            return;
        }

        ThemeColor active = GetActivePaletteColor();
        ThemeColor secondary = _secondaryPaletteColorInitialized ? _secondaryPaletteColor : active;
        if (ColorsClose(active, secondary))
        {
            RefreshPixelStudioView("Choose a different secondary color to generate a ramp.");
            return;
        }

        const int intermediateSteps = 3;
        ApplyPixelStudioChange(
            $"Generated a color ramp from {ToHex(active)} to {ToHex(secondary)}.",
            () =>
            {
                int availableSlots = Math.Max(24 - _pixelStudio.Palette.Count, 0);
                if (availableSlots <= 0)
                {
                    RefreshPixelStudioView("Palette is full, so there is no room to add a ramp.");
                    return false;
                }

                List<ThemeColor> rampColors = [];
                for (int step = 1; step <= intermediateSteps; step++)
                {
                    float t = step / (float)(intermediateSteps + 1);
                    ThemeColor rampColor = new(
                        active.R + ((secondary.R - active.R) * t),
                        active.G + ((secondary.G - active.G) * t),
                        active.B + ((secondary.B - active.B) * t),
                        active.A + ((secondary.A - active.A) * t));

                    if (TryFindPaletteIndex(rampColor, out _) || rampColors.Any(existing => ColorsClose(existing, rampColor)))
                    {
                        continue;
                    }

                    rampColors.Add(rampColor);
                }

                if (rampColors.Count == 0)
                {
                    RefreshPixelStudioView("That ramp is already represented in the current palette.");
                    return false;
                }

                int insertIndex = Math.Clamp(_pixelStudio.ActivePaletteIndex + 1, 0, _pixelStudio.Palette.Count);
                int insertedCount = 0;
                foreach (ThemeColor rampColor in rampColors)
                {
                    if (insertedCount >= availableSlots)
                    {
                        break;
                    }

                    _pixelStudio.Palette.Insert(insertIndex + insertedCount, rampColor);
                    insertedCount++;
                }

                if (insertedCount <= 0)
                {
                    RefreshPixelStudioView("Palette is full, so there is no room to add a ramp.");
                    return false;
                }

                MarkCurrentPaletteAsUnsaved();
                return true;
            },
            rebuildLayout: true);
    }

    private void DuplicateSelectedPalette()
    {
        SavedPixelPalette? selected = GetSelectedSavedPalette();
        string sourceName = selected?.Name ?? "Current Palette";
        IReadOnlyList<PaletteColorSetting> sourceColors = selected?.Colors
            ?? _pixelStudio.Palette.Select(ToPaletteColorSetting).ToList();
        string duplicateName = SanitizePaletteName(BuildDuplicatePaletteName(sourceName));
        SavedPixelPalette duplicate = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = duplicateName,
            IsLocked = false,
            Colors = sourceColors
                .Select(color => new PaletteColorSetting
                {
                    R = color.R,
                    G = color.G,
                    B = color.B,
                    A = color.A
                })
                .ToList()
        };

        ReplaceSavedPalette(duplicate);
        _selectedPixelPaletteId = duplicate.Id;
        _activePixelPaletteId = duplicate.Id;
        _paletteRenameBuffer = duplicate.Name;
        ApplySavedPalette(duplicate);
        RefreshPixelStudioView($"Duplicated palette as {duplicate.Name}.", rebuildLayout: true);
    }

    private void ImportPaletteDocument()
    {
        string initialDirectory = ResolvePixelStudioDocumentDirectory();
        string? palettePath = PixelStudioDocumentFilePicker.ShowPaletteOpenDialog(initialDirectory);
        if (string.IsNullOrWhiteSpace(palettePath))
        {
            RefreshPixelStudioView("Palette import cancelled.");
            return;
        }

        try
        {
            string json = File.ReadAllText(palettePath);
            PixelStudioPaletteDocument? document = JsonSerializer.Deserialize<PixelStudioPaletteDocument>(json, PixelStudioDocumentSerializerOptions);
            if (document is null)
            {
                RefreshPixelStudioView($"Could not read a palette from {Path.GetFileName(palettePath)}.");
                return;
            }

            SavedPixelPalette palette = new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = SanitizePaletteName(string.IsNullOrWhiteSpace(document.Name)
                    ? Path.GetFileNameWithoutExtension(palettePath)
                    : document.Name),
                IsLocked = false,
                Colors = document.Colors
                    .Select(color => new PaletteColorSetting
                    {
                        R = color.R,
                        G = color.G,
                        B = color.B,
                        A = color.A
                    })
                    .ToList()
            };

            ReplaceSavedPalette(palette);
            _selectedPixelPaletteId = palette.Id;
            _activePixelPaletteId = palette.Id;
            ApplySavedPalette(palette);
            RefreshPixelStudioView($"Imported palette {palette.Name}.", rebuildLayout: true);
        }
        catch (Exception ex)
        {
            RefreshPixelStudioView($"Palette import failed: {ex.Message}");
        }
    }

    private void ExportSelectedPalette()
    {
        SavedPixelPalette? selected = GetSelectedSavedPalette();
        string paletteName = selected?.Name ?? $"{_pixelStudio.DocumentName} Palette";
        PixelStudioPaletteDocument document = new()
        {
            Name = paletteName,
            Colors = (selected?.Colors
                    ?? _pixelStudio.Palette.Select(ToPaletteColorSetting).ToList())
                .Select(color => new PaletteColorSetting
                {
                    R = color.R,
                    G = color.G,
                    B = color.B,
                    A = color.A
                })
                .ToList()
        };

        string initialDirectory = ResolvePixelStudioDocumentDirectory();
        string suggestedFileName = $"{SanitizePaletteName(paletteName)}.kpal";
        string? exportPath = PixelStudioDocumentFilePicker.ShowPaletteSaveDialog(initialDirectory, suggestedFileName);
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            RefreshPixelStudioView("Palette export cancelled.");
            return;
        }

        string outputPath = Path.GetExtension(exportPath).Equals(".kpal", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(exportPath).Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? exportPath
            : $"{exportPath}.kpal";

        try
        {
            string json = JsonSerializer.Serialize(document, PixelStudioDocumentSerializerOptions);
            File.WriteAllText(outputPath, json);
            RefreshPixelStudioView($"Exported palette to {Path.GetFileName(outputPath)}.");
        }
        catch (Exception ex)
        {
            RefreshPixelStudioView($"Palette export failed: {ex.Message}");
        }
    }

    private void GeneratePaletteFromImage(string? filePath = null)
    {
        if (IsCurrentPaletteLocked())
        {
            string? pendingPath = filePath;
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.ModifyLockedPalette,
                "Palette Is Locked",
                $"{GetLockedPaletteSourceName()} is locked. Unlock the palette, replace the visible swatches from an image, or cancel.",
                confirmAction: () =>
                {
                    if (!UnlockLockedPaletteSource())
                    {
                        RefreshPixelStudioView("Could not unlock the current palette.", rebuildLayout: true);
                        return;
                    }

                    GeneratePaletteFromImage(pendingPath);
                });
            return;
        }

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
            ReplaceCurrentPaletteColors(generatedPalette, remapArtwork: false);
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
        SelectAllText(EditableTextTarget.PaletteRename);
        RefreshPixelStudioView($"Renaming {selected.Name}.", rebuildLayout: true);
    }

    private void CommitPaletteRename()
    {
        SavedPixelPalette? selected = GetSelectedSavedPalette();
        if (selected is null)
        {
            _paletteRenameActive = false;
            ClearSelectedText(EditableTextTarget.PaletteRename);
            RefreshPixelStudioView("No saved palette selected.");
            return;
        }

        string name = SanitizePaletteName(_paletteRenameBuffer);
        ReplaceSavedPalette(new SavedPixelPalette
        {
            Id = selected.Id,
            Name = name,
            IsLocked = selected.IsLocked,
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
        ClearSelectedText(EditableTextTarget.PaletteRename);
        ClosePixelContextMenu();
        RefreshPixelStudioView($"Renamed palette to {name}.", rebuildLayout: true);
    }

    private void CancelPaletteRename()
    {
        _paletteRenameActive = false;
        _paletteRenameBuffer = string.Empty;
        ClearSelectedText(EditableTextTarget.PaletteRename);
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
        _layerRenameTargetsGroup = false;
        _layerRenameGroupId = null;
        _layerRenameBuffer = CurrentPixelFrame.Layers[layerIndex].Name;
        SelectAllText(EditableTextTarget.LayerRename);
        RefreshPixelStudioView($"Renaming {CurrentPixelFrame.Layers[layerIndex].Name}.", rebuildLayout: true);
    }

    private void StartLayerGroupRename(string groupId)
    {
        string? groupName = GetLayerGroupDisplayName(groupId);
        if (string.IsNullOrWhiteSpace(groupName))
        {
            RefreshPixelStudioView("Select a group to rename.");
            return;
        }

        ClosePixelContextMenu();
        _paletteRenameActive = false;
        _layerRenameActive = true;
        _layerRenameTargetsGroup = true;
        _layerRenameGroupId = groupId;
        _layerRenameBuffer = groupName;
        SelectAllText(EditableTextTarget.LayerRename);
        RefreshPixelStudioView($"Renaming {groupName}.", rebuildLayout: true);
    }

    private void CommitLayerRename()
    {
        if (_layerRenameTargetsGroup && !string.IsNullOrWhiteSpace(_layerRenameGroupId))
        {
            string groupId = _layerRenameGroupId;
            string groupName = BuildUniqueLayerGroupName(_layerRenameBuffer, groupId);
            ApplyPixelStudioChange($"Renamed group to {groupName}.", () =>
            {
                foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
                {
                    foreach (PixelStudioLayerState layer in frame.Layers.Where(layer => string.Equals(layer.GroupId, groupId, StringComparison.Ordinal)))
                    {
                        layer.GroupName = groupName;
                    }
                }

                return true;
            });
            _layerRenameActive = false;
            _layerRenameTargetsGroup = false;
            _layerRenameGroupId = null;
            _layerRenameBuffer = groupName;
            ClearSelectedText(EditableTextTarget.LayerRename);
            return;
        }

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
        ClearSelectedText(EditableTextTarget.LayerRename);
    }

    private void CancelLayerRename()
    {
        _layerRenameActive = false;
        _layerRenameTargetsGroup = false;
        _layerRenameGroupId = null;
        _layerRenameBuffer = string.Empty;
        ClearSelectedText(EditableTextTarget.LayerRename);
        RefreshPixelStudioView("Layer rename cancelled.", rebuildLayout: true);
    }

    private void StartFrameRename(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _pixelStudio.Frames.Count)
        {
            RefreshPixelStudioView("Select a frame to rename.");
            return;
        }

        CancelFrameDurationEdit(silent: true);
        ClosePixelContextMenu();
        _pixelStudio.ActiveFrameIndex = frameIndex;
        _pixelStudio.PreviewFrameIndex = frameIndex;
        _frameRenameActive = true;
        _frameRenameBuffer = _pixelStudio.Frames[frameIndex].Name;
        SelectAllText(EditableTextTarget.FrameRename);
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
        ClearSelectedText(EditableTextTarget.FrameRename);
    }

    private void CancelFrameRename()
    {
        _frameRenameActive = false;
        _frameRenameBuffer = string.Empty;
        ClearSelectedText(EditableTextTarget.FrameRename);
        RefreshPixelStudioView("Frame rename cancelled.", rebuildLayout: true);
    }

    private void ActivateFrameDurationField()
    {
        if (_pixelStudio.ActiveFrameIndex < 0 || _pixelStudio.ActiveFrameIndex >= _pixelStudio.Frames.Count)
        {
            return;
        }

        if (_frameRenameActive)
        {
            return;
        }

        _frameDurationFieldActive = true;
        _frameDurationBuffer = GetFrameDurationMilliseconds(_pixelStudio.ActiveFrameIndex).ToString(CultureInfo.InvariantCulture);
        SelectAllText(EditableTextTarget.FrameDuration);
        RefreshPixelStudioView($"Editing {CurrentPixelFrame.Name} timing.");
    }

    private void CommitFrameDurationEdit()
    {
        if (!_frameDurationFieldActive)
        {
            return;
        }

        if (_pixelStudio.ActiveFrameIndex < 0 || _pixelStudio.ActiveFrameIndex >= _pixelStudio.Frames.Count)
        {
            CancelFrameDurationEdit(silent: true);
            return;
        }

        if (!TryGetBufferedFrameDuration(out int durationMilliseconds))
        {
            CancelFrameDurationEdit(silent: true);
            RefreshPixelStudioView("Frame duration edit cancelled.");
            return;
        }

        int frameIndex = Math.Clamp(_pixelStudio.ActiveFrameIndex, 0, _pixelStudio.Frames.Count - 1);
        int currentDurationMilliseconds = Math.Clamp(_pixelStudio.Frames[frameIndex].DurationMilliseconds, 40, 1000);
        _frameDurationFieldActive = false;
        ClearSelectedText(EditableTextTarget.FrameDuration);
        if (durationMilliseconds == currentDurationMilliseconds)
        {
            _frameDurationBuffer = durationMilliseconds.ToString(CultureInfo.InvariantCulture);
            RefreshPixelStudioView($"{CurrentPixelFrame.Name} already uses {durationMilliseconds} ms.");
            return;
        }

        _frameDurationBuffer = durationMilliseconds.ToString(CultureInfo.InvariantCulture);
        ApplyPixelStudioChange($"Set {CurrentPixelFrame.Name} to {durationMilliseconds} ms.", () =>
        {
            CurrentPixelFrame.DurationMilliseconds = durationMilliseconds;
            return true;
        }, rebuildLayout: false);
    }

    private void CancelFrameDurationEdit(bool silent = false)
    {
        if (!_frameDurationFieldActive && string.IsNullOrEmpty(_frameDurationBuffer))
        {
            return;
        }

        _frameDurationFieldActive = false;
        _frameDurationBuffer = string.Empty;
        ClearSelectedText(EditableTextTarget.FrameDuration);
        if (!silent)
        {
            RefreshPixelStudioView("Frame duration edit cancelled.");
        }
    }

    private void DeleteSelectionPixels()
    {
        if (!_selectionActive)
        {
            return;
        }

        if (!CanTransformCurrentLayer("clearing the selection"))
        {
            return;
        }

        ApplyPixelStudioChange("Cleared selected pixels.", () =>
        {
            bool changed = false;
            foreach (int index in EnumerateSelectedIndices())
            {
                if (index < 0 || index >= CurrentPixelLayer.Pixels.Length)
                {
                    continue;
                }

                if (IsTransparentPixel(CurrentPixelLayer.Pixels[index]))
                {
                    continue;
                }

                if (TryWritePixelToCurrentLayer(index, -1))
                {
                    changed = true;
                }
            }

            return changed;
        }, rebuildLayout: true);
    }

    private void CopySelectionPixels()
    {
        CaptureSelectionClipboard(showStatus: true);
    }

    private void CutSelectionPixels()
    {
        if (!CanTransformCurrentLayer("cutting"))
        {
            return;
        }

        if (!CaptureSelectionClipboard(showStatus: false))
        {
            RefreshPixelStudioView("Create a selection before cutting.");
            return;
        }

        ApplyPixelStudioChange("Cut selected pixels.", () =>
        {
            bool changed = false;
            foreach (int index in EnumerateSelectedIndices())
            {
                if (index < 0 || index >= CurrentPixelLayer.Pixels.Length)
                {
                    continue;
                }

                if (IsTransparentPixel(CurrentPixelLayer.Pixels[index]))
                {
                    continue;
                }

                if (TryWritePixelToCurrentLayer(index, -1))
                {
                    changed = true;
                }
            }

            if (!changed)
            {
                return false;
            }

            _selectionCommitted = true;
            _selectionDragActive = false;
            return true;
        }, rebuildLayout: false);
    }

    private void PasteSelectionPixels()
    {
        if (!CanTransformCurrentLayer("pasting"))
        {
            return;
        }

        if (!HasSelectionClipboard() || _selectionClipboardPixels is null)
        {
            RefreshPixelStudioView("Copy a selection before pasting.");
            return;
        }

        GetPasteSelectionTarget(out int left, out int top);
        ApplyPixelStudioChange("Pasted selection.", () =>
        {
            int pasteWidth = Math.Min(_selectionClipboardWidth, Math.Max(_pixelStudio.CanvasWidth - left, 0));
            int pasteHeight = Math.Min(_selectionClipboardHeight, Math.Max(_pixelStudio.CanvasHeight - top, 0));
            if (pasteWidth <= 0 || pasteHeight <= 0)
            {
                return false;
            }

            HashSet<int> pastedIndices = [];
            for (int y = 0; y < pasteHeight; y++)
            {
                for (int x = 0; x < pasteWidth; x++)
                {
                    int source = _selectionClipboardPixels[(y * _selectionClipboardWidth) + x];
                    if (source == ClipboardEmptyPixel)
                    {
                        continue;
                    }

                    int targetIndex = ((top + y) * _pixelStudio.CanvasWidth) + (left + x);
                    if (TryWritePixelToCurrentLayer(targetIndex, source))
                    {
                        pastedIndices.Add(targetIndex);
                    }
                }
            }

            if (pastedIndices.Count == 0)
            {
                return false;
            }

            ApplySelectionMask(pastedIndices, committed: true);
            return true;
        }, rebuildLayout: false);
    }

    private void FlipSelectionPixels(bool horizontal)
    {
        if (!CanTransformCurrentLayer(horizontal ? "flipping horizontally" : "flipping vertically"))
        {
            return;
        }

        if (!TryGetSelectionBounds(out int left, out int top, out int width, out int height))
        {
            RefreshPixelStudioView("Create a selection before flipping.");
            return;
        }

        ApplyPixelStudioChange(horizontal ? "Flipped selection horizontally." : "Flipped selection vertically.", () =>
        {
            int[] source = BuildSelectionClipboardBuffer(left, top, width, height);
            HashSet<int> flippedIndices = [];

            foreach (int index in EnumerateSelectedIndices())
            {
                if (index >= 0 && index < CurrentPixelLayer.Pixels.Length)
                {
                    if (CanWritePixelToLayer(CurrentPixelLayer, index, TransparentPixelValue))
                    {
                        CurrentPixelLayer.Pixels[index] = TransparentPixelValue;
                    }
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sourceX = horizontal ? (width - 1 - x) : x;
                    int sourceY = horizontal ? y : (height - 1 - y);
                    int sourceValue = source[(sourceY * width) + sourceX];
                    if (sourceValue == ClipboardEmptyPixel)
                    {
                        continue;
                    }

                    int targetIndex = ((top + y) * _pixelStudio.CanvasWidth) + (left + x);
                    if (TryWritePixelToCurrentLayer(targetIndex, sourceValue))
                    {
                        flippedIndices.Add(targetIndex);
                    }
                }
            }

            if (flippedIndices.Count == 0)
            {
                return false;
            }

            ApplySelectionMask(flippedIndices, committed: true);
            return true;
        }, rebuildLayout: false);
    }

    private void RotateSelectionPixels(bool clockwise)
    {
        if (!CanTransformCurrentLayer(clockwise ? "rotating clockwise" : "rotating counterclockwise"))
        {
            return;
        }

        if (!TryGetSelectionBounds(out int left, out int top, out int width, out int height))
        {
            RefreshPixelStudioView("Create a selection before rotating.");
            return;
        }

        int targetWidth = height;
        int targetHeight = width;
        ComputeCenteredSelectionTarget(left, top, width, height, targetWidth, targetHeight, out int targetLeft, out int targetTop);

        ApplyPixelStudioChange(clockwise ? "Rotated selection clockwise." : "Rotated selection counterclockwise.", () =>
        {
            return ApplySelectionBufferTransform(
                left,
                top,
                width,
                height,
                targetLeft,
                targetTop,
                targetWidth,
                targetHeight,
                clockwise
                    ? static (targetX, targetY, sourceWidth, sourceHeight) => (targetY, sourceHeight - 1 - targetX)
                    : static (targetX, targetY, sourceWidth, sourceHeight) => (sourceWidth - 1 - targetY, targetX));
        }, rebuildLayout: true);
    }

    private void ScaleSelectionPixels(bool scaleUp, bool requireWarning = true)
    {
        if (!CanTransformCurrentLayer(scaleUp ? "scaling up" : "scaling down"))
        {
            return;
        }

        if (!TryGetSelectionBounds(out int left, out int top, out int width, out int height))
        {
            RefreshPixelStudioView("Create a selection before scaling.");
            return;
        }

        int targetWidth = scaleUp
            ? Math.Min(width * 2, _pixelStudio.CanvasWidth)
            : Math.Max((int)MathF.Ceiling(width / 2f), 1);
        int targetHeight = scaleUp
            ? Math.Min(height * 2, _pixelStudio.CanvasHeight)
            : Math.Max((int)MathF.Ceiling(height / 2f), 1);
        if (targetWidth == width && targetHeight == height)
        {
            RefreshPixelStudioView(scaleUp
                ? "Selection is already at the current maximum scale."
                : "Selection is already at the current minimum scale.");
            return;
        }

        ComputeCenteredSelectionTarget(left, top, width, height, targetWidth, targetHeight, out int targetLeft, out int targetTop);
        if (requireWarning)
        {
            OpenPixelWarningDialog(
                scaleUp ? PixelStudioWarningDialogKind.ScaleSelectionUp : PixelStudioWarningDialogKind.ScaleSelectionDown,
                scaleUp ? "Scale Selection Up?" : "Scale Selection Down?",
                scaleUp
                    ? "Scaling a selection up can introduce distortion, especially if you repeat the 2x step several times. Continue with the larger selection?"
                    : "Scaling a selection down can permanently remove pixel detail. Continue with the smaller selection?");
            return;
        }

        string scaleStatus = scaleUp
            ? "Scaled selection up."
            : "Scaled selection down.";
        ApplyPixelStudioChange(scaleStatus, () =>
        {
            return ApplySelectionBufferTransform(
                left,
                top,
                width,
                height,
                targetLeft,
                targetTop,
                targetWidth,
                targetHeight,
                (targetX, targetY, sourceWidth, sourceHeight) =>
                {
                    int sourceX = Math.Clamp((int)MathF.Floor((targetX / (float)Math.Max(targetWidth - 1, 1)) * Math.Max(sourceWidth - 1, 0)), 0, Math.Max(sourceWidth - 1, 0));
                    int sourceY = Math.Clamp((int)MathF.Floor((targetY / (float)Math.Max(targetHeight - 1, 1)) * Math.Max(sourceHeight - 1, 0)), 0, Math.Max(sourceHeight - 1, 0));
                    return (sourceX, sourceY);
                });
        }, rebuildLayout: true);
    }

    private void NudgeSelectionBy(int deltaX, int deltaY)
    {
        if (!CanTransformCurrentLayer("nudging the selection"))
        {
            return;
        }

        if (!TryGetSelectionBounds(out int left, out int top, out int width, out int height))
        {
            RefreshPixelStudioView("Create a selection before nudging.");
            return;
        }

        int targetLeft = Math.Clamp(left + deltaX, 0, Math.Max(_pixelStudio.CanvasWidth - width, 0));
        int targetTop = Math.Clamp(top + deltaY, 0, Math.Max(_pixelStudio.CanvasHeight - height, 0));
        if (targetLeft == left && targetTop == top)
        {
            RefreshPixelStudioView("Selection is already at the canvas edge.");
            return;
        }

        ApplyPixelStudioChange($"Moved selection to {targetLeft + 1},{targetTop + 1}.", () =>
        {
            return ApplySelectionBufferTransform(
                left,
                top,
                width,
                height,
                targetLeft,
                targetTop,
                width,
                height,
                static (targetX, targetY, sourceWidth, sourceHeight) => (targetX, targetY));
        }, rebuildLayout: true);
    }

    private bool ApplySelectionBufferTransform(
        int sourceLeft,
        int sourceTop,
        int sourceWidth,
        int sourceHeight,
        int targetLeft,
        int targetTop,
        int targetWidth,
        int targetHeight,
        Func<int, int, int, int, (int SourceX, int SourceY)> sourceCoordinateSelector)
    {
        int[] source = BuildSelectionClipboardBuffer(sourceLeft, sourceTop, sourceWidth, sourceHeight);
        bool usesMask = SelectionUsesMask();
        bool mirrorTransform = _mirrorMode != PixelStudioMirrorMode.Off;
        List<(int TargetIndex, int SourceValue)> writes = [];
        HashSet<int> transformedIndices = [];
        bool changedPixels = false;
        bool hasSourcePixels = false;
        for (int targetY = 0; targetY < targetHeight; targetY++)
        {
            for (int targetX = 0; targetX < targetWidth; targetX++)
            {
                (int sourceX, int sourceY) = sourceCoordinateSelector(targetX, targetY, sourceWidth, sourceHeight);
                if (sourceX < 0 || sourceX >= sourceWidth || sourceY < 0 || sourceY >= sourceHeight)
                {
                    continue;
                }

                int sourceValue = source[(sourceY * sourceWidth) + sourceX];
                if (sourceValue == ClipboardEmptyPixel)
                {
                    continue;
                }

                hasSourcePixels = true;

                int absoluteX = targetLeft + targetX;
                int absoluteY = targetTop + targetY;
                if (!IsWithinCanvas(absoluteX, absoluteY))
                {
                    continue;
                }

                HashSet<int> mirroredTargets = [];
                if (mirrorTransform)
                {
                    AddMirroredCanvasTargets(mirroredTargets, absoluteX, absoluteY);
                }
                else
                {
                    AddCanvasTarget(mirroredTargets, absoluteX, absoluteY);
                }

                foreach (int targetIndex in mirroredTargets)
                {
                    if (!CanWritePixelToLayer(CurrentPixelLayer, targetIndex, sourceValue))
                    {
                        continue;
                    }

                    writes.Add((targetIndex, sourceValue));
                    transformedIndices.Add(targetIndex);
                }
            }
        }

        if (!hasSourcePixels || writes.Count == 0)
        {
            return false;
        }

        foreach (int index in EnumerateSelectedIndices())
        {
            if (index >= 0 && index < CurrentPixelLayer.Pixels.Length)
            {
                    if (CanWritePixelToLayer(CurrentPixelLayer, index, TransparentPixelValue))
                    {
                        CurrentPixelLayer.Pixels[index] = TransparentPixelValue;
                }
            }
        }

        foreach ((int targetIndex, int sourceValue) in writes)
        {
            if (CurrentPixelLayer.Pixels[targetIndex] != sourceValue)
            {
                changedPixels = true;
            }

            CurrentPixelLayer.Pixels[targetIndex] = sourceValue;
        }

        bool targetFullyWithinCanvas =
            targetLeft >= 0 &&
            targetTop >= 0 &&
            targetLeft + targetWidth <= _pixelStudio.CanvasWidth &&
            targetTop + targetHeight <= _pixelStudio.CanvasHeight;
        if (usesMask || mirrorTransform || !targetFullyWithinCanvas)
        {
            ApplySelectionMask(transformedIndices, committed: true);
        }
        else
        {
            SetSelectionRect(targetLeft, targetTop, targetWidth, targetHeight);
            _selectionCommitted = true;
            _selectionDragActive = false;
        }

        return changedPixels
            || targetLeft != sourceLeft
            || targetTop != sourceTop
            || targetWidth != sourceWidth
            || targetHeight != sourceHeight;
    }

    private void ComputeCenteredSelectionTarget(
        int sourceLeft,
        int sourceTop,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        out int targetLeft,
        out int targetTop)
    {
        float centerX = sourceLeft + ((sourceWidth - 1) * 0.5f);
        float centerY = sourceTop + ((sourceHeight - 1) * 0.5f);
        targetLeft = (int)MathF.Round(centerX - ((targetWidth - 1) * 0.5f));
        targetTop = (int)MathF.Round(centerY - ((targetHeight - 1) * 0.5f));
        targetLeft = Math.Clamp(targetLeft, 0, Math.Max(_pixelStudio.CanvasWidth - targetWidth, 0));
        targetTop = Math.Clamp(targetTop, 0, Math.Max(_pixelStudio.CanvasHeight - targetHeight, 0));
    }

    private void GetPasteSelectionTarget(out int left, out int top)
    {
        if (TryGetSelectionBounds(out left, out top, out _, out _))
        {
            left = Math.Clamp(left, 0, Math.Max(_pixelStudio.CanvasWidth - _selectionClipboardWidth, 0));
            top = Math.Clamp(top, 0, Math.Max(_pixelStudio.CanvasHeight - _selectionClipboardHeight, 0));
            return;
        }

        if (_layoutSnapshot?.PixelStudio is not null)
        {
            PixelStudioLayoutSnapshot layout = _layoutSnapshot.PixelStudio;
            float clipCenterX = layout.CanvasClipRect.X + (layout.CanvasClipRect.Width * 0.5f);
            float clipCenterY = layout.CanvasClipRect.Y + (layout.CanvasClipRect.Height * 0.5f);
            float canvasCenterX = (clipCenterX - layout.CanvasViewportRect.X) / Math.Max(layout.CanvasCellSize, 1);
            float canvasCenterY = (clipCenterY - layout.CanvasViewportRect.Y) / Math.Max(layout.CanvasCellSize, 1);
            left = (int)MathF.Round(canvasCenterX - (_selectionClipboardWidth * 0.5f));
            top = (int)MathF.Round(canvasCenterY - (_selectionClipboardHeight * 0.5f));
        }
        else
        {
            left = (_pixelStudio.CanvasWidth - _selectionClipboardWidth) / 2;
            top = (_pixelStudio.CanvasHeight - _selectionClipboardHeight) / 2;
        }

        left = Math.Clamp(left, 0, Math.Max(_pixelStudio.CanvasWidth - _selectionClipboardWidth, 0));
        top = Math.Clamp(top, 0, Math.Max(_pixelStudio.CanvasHeight - _selectionClipboardHeight, 0));
    }

    private void DeleteSelectedSavedPalette()
    {
        SavedPixelPalette? selected = GetSelectedSavedPalette();
        if (selected is null)
        {
            RefreshPixelStudioView("Select a saved palette to delete.");
            return;
        }

        if (selected.IsLocked)
        {
            int selectedIndex = _savedPixelPalettes.FindIndex(palette => string.Equals(palette.Id, selected.Id, StringComparison.Ordinal));
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.DeleteLockedSavedPalette,
                "Palette Is Locked",
                $"Palette {selected.Name} is locked. Unlock the palette, delete it, or cancel.",
                paletteTargetIndex: selectedIndex);
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

    private void ToggleSelectedPaletteLock()
    {
        SavedPixelPalette? selected = GetSelectedSavedPalette();
        if (selected is null)
        {
            RefreshPixelStudioView("Select a saved palette to lock.");
            return;
        }

        SavedPixelPalette updated = new()
        {
            Id = selected.Id,
            Name = selected.Name,
            IsLocked = !selected.IsLocked,
            Colors = selected.Colors
                .Select(color => new PaletteColorSetting
                {
                    R = color.R,
                    G = color.G,
                    B = color.B,
                    A = color.A
                })
                .ToList()
        };

        ReplaceSavedPalette(updated);
        if (string.Equals(_activePixelPaletteId, updated.Id, StringComparison.Ordinal))
        {
            if (updated.IsLocked)
            {
                SetLockedPaletteProtection(updated);
            }
            else
            {
                ClearLockedPaletteProtection();
            }
        }
        else if (!updated.IsLocked && string.Equals(_lockedPixelPaletteSourceId, updated.Id, StringComparison.Ordinal))
        {
            ClearLockedPaletteProtection();
        }

        RefreshPixelStudioView(
            updated.IsLocked
                ? $"Locked palette {updated.Name}. Palette edits are blocked until you unlock it."
                : $"Unlocked palette {updated.Name}.",
            rebuildLayout: true);
    }

    private List<ThemeColor?> ComposeVisiblePixels(PixelStudioFrameState frame)
    {
        List<ThemeColor?> composite = Enumerable.Repeat<ThemeColor?>(null, _pixelStudio.CanvasWidth * _pixelStudio.CanvasHeight).ToList();
        for (int layerIndex = frame.Layers.Count - 1; layerIndex >= 0; layerIndex--)
        {
            PixelStudioLayerState layer = frame.Layers[layerIndex];
            if (!layer.IsVisible)
            {
                continue;
            }

            for (int pixelIndex = 0; pixelIndex < layer.Pixels.Length; pixelIndex++)
            {
                ThemeColor? layerColor = UnpackPixelColor(layer.Pixels[pixelIndex]);
                if (layerColor is not null)
                {
                    composite[pixelIndex] = BlendLayerColorOver(composite[pixelIndex], layerColor.Value, layer.Opacity);
                }
            }
        }

        return composite;
    }

    private void MergeLayerPixelsIntoDestination(PixelStudioLayerState sourceLayer, PixelStudioLayerState destinationLayer)
    {
        for (int pixelIndex = 0; pixelIndex < destinationLayer.Pixels.Length; pixelIndex++)
        {
            ThemeColor? flattenedColor = GetLayerPixelCompositeColor(destinationLayer, pixelIndex);
            ThemeColor? sourceColor = UnpackPixelColor(sourceLayer.Pixels[pixelIndex]);
            if (sourceColor is not null)
            {
                flattenedColor = BlendLayerColorOver(flattenedColor, sourceColor.Value, sourceLayer.Opacity);
            }

            destinationLayer.Pixels[pixelIndex] = FlattenCompositeColorToPixelValue(flattenedColor);
        }
    }

    private ThemeColor? GetLayerPixelCompositeColor(PixelStudioLayerState layer, int pixelIndex)
    {
        if (pixelIndex < 0 || pixelIndex >= layer.Pixels.Length)
        {
            return null;
        }

        ThemeColor? color = UnpackPixelColor(layer.Pixels[pixelIndex]);
        if (color is null)
        {
            return null;
        }

        ThemeColor baseColor = color.Value;
        float resolvedAlpha = Math.Clamp(baseColor.A * NormalizeLayerOpacity(layer.Opacity), 0f, 1f);
        if (resolvedAlpha <= 0.0001f)
        {
            return null;
        }

        return new ThemeColor(baseColor.R, baseColor.G, baseColor.B, resolvedAlpha);
    }

    private static int FlattenCompositeColorToPixelValue(ThemeColor? color)
    {
        if (color is null || color.Value.A <= 0.025f)
        {
            return TransparentPixelValue;
        }

        return PackPixelColor(color.Value);
    }

    private static ThemeColor? BlendLayerColorOver(ThemeColor? destination, ThemeColor source, float layerOpacity)
    {
        float sourceAlpha = Math.Clamp(source.A * NormalizeLayerOpacity(layerOpacity), 0f, 1f);
        if (sourceAlpha <= 0.0001f)
        {
            return destination;
        }

        if (destination is null || destination.Value.A <= 0.0001f)
        {
            return new ThemeColor(source.R, source.G, source.B, sourceAlpha);
        }

        ThemeColor destinationColor = destination.Value;
        float destinationAlpha = Math.Clamp(destinationColor.A, 0f, 1f);
        float outputAlpha = sourceAlpha + (destinationAlpha * (1f - sourceAlpha));
        if (outputAlpha <= 0.0001f)
        {
            return null;
        }

        float outputRed = ((source.R * sourceAlpha) + (destinationColor.R * destinationAlpha * (1f - sourceAlpha))) / outputAlpha;
        float outputGreen = ((source.G * sourceAlpha) + (destinationColor.G * destinationAlpha * (1f - sourceAlpha))) / outputAlpha;
        float outputBlue = ((source.B * sourceAlpha) + (destinationColor.B * destinationAlpha * (1f - sourceAlpha))) / outputAlpha;
        return new ThemeColor(outputRed, outputGreen, outputBlue, outputAlpha);
    }

    private List<ThemeColor?> ComposeOnionSkinPixels(int frameIndex, float alpha)
    {
        if (frameIndex < 0 || frameIndex >= _pixelStudio.Frames.Count)
        {
            return [];
        }

        List<ThemeColor?> sourcePixels = ComposeVisiblePixels(_pixelStudio.Frames[frameIndex]);
        List<ThemeColor?> tintedPixels = new(sourcePixels.Count);
        foreach (ThemeColor? pixel in sourcePixels)
        {
            if (pixel is null)
            {
                tintedPixels.Add(null);
                continue;
            }

            ThemeColor source = pixel.Value;
            tintedPixels.Add(new ThemeColor(source.R, source.G, source.B, alpha));
        }

        return tintedPixels;
    }

    private void EnsurePixelStudioIndices()
    {
        if (_pixelStudio.Frames.Count == 0)
        {
            ReplacePixelStudioDocument(CreateBlankPixelStudio(_pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight));
        }

        _pixelStudio.ActiveFrameIndex = Math.Clamp(_pixelStudio.ActiveFrameIndex, 0, _pixelStudio.Frames.Count - 1);
        _pixelStudio.PreviewFrameIndex = Math.Clamp(_pixelStudio.PreviewFrameIndex, 0, _pixelStudio.Frames.Count - 1);
        _pixelStudio.ActiveLayerIndex = Math.Clamp(_pixelStudio.ActiveLayerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        _pixelStudio.ActivePaletteIndex = _pixelStudio.Palette.Count == 0
            ? 0
            : Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1);
        NormalizeLoopRangeState();
    }

    private PixelStudioFrameState CurrentPixelFrame => _pixelStudio.Frames[_pixelStudio.ActiveFrameIndex];

    private PixelStudioFrameState PreviewPixelFrame => _pixelStudio.Frames[_pixelStudio.PreviewFrameIndex];

    private PixelStudioLayerState CurrentPixelLayer => CurrentPixelFrame.Layers[_pixelStudio.ActiveLayerIndex];

    private static float NormalizeLayerOpacity(float value)
    {
        return Math.Clamp(value, 0.05f, 1f);
    }

    private bool CanTransformCurrentLayer(string actionLabel)
    {
        int layerIndex = Math.Clamp(_pixelStudio.ActiveLayerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        if (!EnsureLayerVisibleForEditing(
                layerIndex,
                "Active Layer Is Hidden",
                $"The active layer \"{CurrentPixelFrame.Layers[layerIndex].Name}\" is hidden. Show it before {actionLabel} so your edits stay visible?"))
        {
            return false;
        }

        if (!EnsureLayerUnlockedForEditing(
                layerIndex,
                "Active Layer Is Locked",
                $"The active layer \"{CurrentPixelFrame.Layers[layerIndex].Name}\" is locked. Unlock it before {actionLabel}?"))
        {
            return false;
        }

        if (!EnsureLayerAlphaUnlockedForEditing(
                layerIndex,
                "Alpha Lock Is Active",
                $"Alpha lock is enabled on \"{CurrentPixelFrame.Layers[layerIndex].Name}\". Disable it before {actionLabel} so the layer can change shape or transparency?"))
        {
            return false;
        }

        return true;
    }

    private bool EnsureCurrentLayerVisibleForEditing(string actionLabel)
    {
        int layerIndex = Math.Clamp(_pixelStudio.ActiveLayerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        return EnsureLayerVisibleForEditing(
            layerIndex,
            "Active Layer Is Hidden",
            $"The active layer \"{CurrentPixelFrame.Layers[layerIndex].Name}\" is hidden. Show it before {actionLabel} so your edits stay visible?");
    }

    private bool EnsureLayerVisibleForEditing(int layerIndex, string title, string message)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count || CurrentPixelFrame.Layers[layerIndex].IsVisible)
        {
            return true;
        }

        OpenPixelWarningDialog(
            PixelStudioWarningDialogKind.HiddenLayerEdit,
            title,
            message,
            targetLayerIndex: layerIndex);
        return false;
    }

    private bool EnsureLayerUnlockedForEditing(int layerIndex, string title, string message)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count || !CurrentPixelFrame.Layers[layerIndex].IsLocked)
        {
            return true;
        }

        OpenPixelWarningDialog(
            PixelStudioWarningDialogKind.LockedLayerEdit,
            title,
            message,
            targetLayerIndex: layerIndex);
        return false;
    }

    private bool EnsureLayerAlphaUnlockedForEditing(int layerIndex, string title, string message)
    {
        if (layerIndex < 0 || layerIndex >= CurrentPixelFrame.Layers.Count || !CurrentPixelFrame.Layers[layerIndex].IsAlphaLocked)
        {
            return true;
        }

        OpenPixelWarningDialog(
            PixelStudioWarningDialogKind.AlphaLockedLayerEdit,
            title,
            message,
            targetLayerIndex: layerIndex);
        return false;
    }

    private void RevealLayerForEditing(int layerIndex)
    {
        if (_pixelStudio.Frames.Count == 0)
        {
            return;
        }

        layerIndex = Math.Clamp(layerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            frame.Layers[layerIndex].IsVisible = true;
        }

        MarkPixelStudioRecoveryDirty();
        RefreshPixelStudioView($"Shown layer: {CurrentPixelFrame.Layers[layerIndex].Name}.", rebuildLayout: true, refreshPixelBuffers: true);
    }

    private void UnlockLayerForEditing(int layerIndex)
    {
        if (_pixelStudio.Frames.Count == 0)
        {
            return;
        }

        layerIndex = Math.Clamp(layerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            frame.Layers[layerIndex].IsLocked = false;
        }

        MarkPixelStudioRecoveryDirty();
        RefreshPixelStudioView($"Unlocked layer: {CurrentPixelFrame.Layers[layerIndex].Name}.", rebuildLayout: true, refreshPixelBuffers: true);
    }

    private void DisableLayerAlphaLockForEditing(int layerIndex)
    {
        if (_pixelStudio.Frames.Count == 0)
        {
            return;
        }

        layerIndex = Math.Clamp(layerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
        {
            frame.Layers[layerIndex].IsAlphaLocked = false;
        }

        MarkPixelStudioRecoveryDirty();
        RefreshPixelStudioView($"Disabled alpha lock on layer: {CurrentPixelFrame.Layers[layerIndex].Name}.", rebuildLayout: true, refreshPixelBuffers: true);
    }

    private string GetPixelWarningDialogConfirmLabel()
    {
        return _pixelWarningDialogKind switch
        {
            PixelStudioWarningDialogKind.HiddenLayerEdit => "Show Layer",
            PixelStudioWarningDialogKind.LockedLayerEdit => "Unlock Layer",
            PixelStudioWarningDialogKind.AlphaLockedLayerEdit => "Disable Alpha Lock",
            PixelStudioWarningDialogKind.ModifyLockedPalette => "Unlock & Continue",
            PixelStudioWarningDialogKind.DeleteLockedSavedPalette => "Unlock & Delete",
            PixelStudioWarningDialogKind.DeleteLockedPaletteSwatch => "Unlock & Delete",
            PixelStudioWarningDialogKind.DeleteUsedPaletteSwatch when _pixelWarningPaletteSwapTargetIndex >= 0 => "Swap Colors",
            PixelStudioWarningDialogKind.DeleteUsedPaletteSwatch => "Delete",
            PixelStudioWarningDialogKind.ApplyPaletteWithArtworkColors => "Save & Swap",
            _ => "Continue"
        };
    }

    private bool HasPixelWarningDialogAlternateAction()
    {
        return _pixelWarningDialogKind switch
        {
            PixelStudioWarningDialogKind.DeleteUsedPaletteSwatch when _pixelWarningPaletteSwapTargetIndex >= 0 => true,
            PixelStudioWarningDialogKind.ApplyPaletteWithArtworkColors => true,
            _ => false
        };
    }

    private bool HasPixelWarningDialogTertiaryAction()
    {
        return false;
    }

    private string GetPixelWarningDialogAlternateLabel()
    {
        return _pixelWarningDialogKind switch
        {
            PixelStudioWarningDialogKind.DeleteUsedPaletteSwatch when _pixelWarningPaletteSwapTargetIndex >= 0 => "Delete",
            PixelStudioWarningDialogKind.ApplyPaletteWithArtworkColors => "Apply Palette",
            _ => string.Empty
        };
    }

    private string GetPixelWarningDialogTertiaryLabel()
    {
        return string.Empty;
    }

    private string GetPixelWarningDialogCancelLabel()
    {
        return _pixelWarningDialogKind switch
        {
            PixelStudioWarningDialogKind.HiddenLayerEdit => "Cancel",
            PixelStudioWarningDialogKind.LockedLayerEdit => "Cancel",
            PixelStudioWarningDialogKind.AlphaLockedLayerEdit => "Cancel",
            PixelStudioWarningDialogKind.ModifyLockedPalette => "Cancel",
            PixelStudioWarningDialogKind.DeleteUsedPaletteSwatch => "Cancel",
            _ => "Cancel"
        };
    }

    private static bool CanWritePixelToLayer(PixelStudioLayerState layer, int pixelIndex, int nextValue)
    {
        if (pixelIndex < 0 || pixelIndex >= layer.Pixels.Length)
        {
            return false;
        }

        if (!layer.IsAlphaLocked)
        {
            return true;
        }

        bool currentTransparent = IsTransparentPixel(layer.Pixels[pixelIndex]);
        bool nextTransparent = IsTransparentPixel(nextValue);
        return currentTransparent == nextTransparent;
    }

    private bool TryWritePixelToCurrentLayer(int pixelIndex, int nextValue)
    {
        if (!CanWritePixelToLayer(CurrentPixelLayer, pixelIndex, nextValue))
        {
            if (!_pixelWarningDialogVisible)
            {
                OpenPixelWarningDialog(
                    PixelStudioWarningDialogKind.AlphaLockedLayerEdit,
                    "Alpha Lock Blocks This Edit",
                    $"Alpha lock on \"{CurrentPixelLayer.Name}\" only lets you recolor existing painted pixels. Disable alpha lock to add or remove transparency.",
                    targetLayerIndex: _pixelStudio.ActiveLayerIndex);
            }
            return false;
        }

        if (CurrentPixelLayer.Pixels[pixelIndex] == nextValue)
        {
            return false;
        }

        CurrentPixelLayer.Pixels[pixelIndex] = nextValue;
        return true;
    }

    private static PixelStudioState CreateDefaultPixelStudio()
    {
        return CreateBlankPixelStudio(32, 32);
    }

    private static PixelStudioMirrorMode GetNextMirrorMode(PixelStudioMirrorMode mode)
    {
        return mode switch
        {
            PixelStudioMirrorMode.Off => PixelStudioMirrorMode.Horizontal,
            PixelStudioMirrorMode.Horizontal => PixelStudioMirrorMode.Vertical,
            PixelStudioMirrorMode.Vertical => PixelStudioMirrorMode.Both,
            _ => PixelStudioMirrorMode.Off
        };
    }

    private string BuildMirrorModeStatusText()
    {
        return _mirrorMode switch
        {
            PixelStudioMirrorMode.Horizontal => "Mirror drawing enabled: horizontal symmetry. Drag the guide handle to move the axis.",
            PixelStudioMirrorMode.Vertical => "Mirror drawing enabled: vertical symmetry. Drag the guide handle to move the axis.",
            PixelStudioMirrorMode.Both => "Mirror drawing enabled: four-way symmetry. Drag either guide handle to move the axes.",
            _ => "Mirror drawing disabled."
        };
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
                    DurationMilliseconds = 125,
                    Layers =
                    [
                        new PixelStudioLayerState
                        {
                            Name = "Layer 1",
                            Opacity = 1f,
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

    private PixelStudioEditorToolState CapturePixelStudioEditorToolState()
    {
        return new PixelStudioEditorToolState(
            _pixelStudio.ActiveTool,
            _pixelStudio.BrushSize,
            _pencilRoundBrushSize,
            _pencilHardBrushSize,
            _pencilPixelPerfectBrushSize,
            _eraserRoundBrushSize,
            _eraserHardBrushSize,
            _eraserPixelPerfectBrushSize,
            _lineBrushSize,
            _brushMode,
            _pixelStudio.ActivePaletteIndex,
            _selectionMode,
            _mirrorMode,
            _mirrorAxisX,
            _mirrorAxisY,
            _rectangleRenderMode,
            _ellipseRenderMode,
            _shapePreset,
            _shapeRenderMode,
            _collapsedLayerGroupIds.ToArray());
    }

    private void ApplyPixelStudioEditorToolState(PixelStudioEditorToolState state)
    {
        _pixelStudio.ActiveTool = state.ActiveTool;
        _pixelStudio.BrushSize = Math.Clamp(state.BrushSize, 1, 16);
        _pencilRoundBrushSize = Math.Clamp(state.PencilRoundBrushSize, 1, 16);
        _pencilHardBrushSize = Math.Clamp(state.PencilHardBrushSize, 1, 16);
        _pencilPixelPerfectBrushSize = Math.Clamp(state.PencilPixelPerfectBrushSize, 1, 16);
        _eraserRoundBrushSize = Math.Clamp(state.EraserRoundBrushSize, 1, 16);
        _eraserHardBrushSize = Math.Clamp(state.EraserHardBrushSize, 1, 16);
        _eraserPixelPerfectBrushSize = Math.Clamp(state.EraserPixelPerfectBrushSize, 1, 16);
        _lineBrushSize = Math.Clamp(state.LineBrushSize, 1, 16);
        _brushMode = state.BrushMode;
        _selectionMode = state.SelectionMode;
        _mirrorMode = state.MirrorMode;
        _mirrorAxisX = state.MirrorAxisX;
        _mirrorAxisY = state.MirrorAxisY;
        _rectangleRenderMode = state.RectangleRenderMode;
        _ellipseRenderMode = state.EllipseRenderMode;
        _shapePreset = state.ShapePreset;
        _shapeRenderMode = state.ShapeRenderMode;
        _collapsedLayerGroupIds.Clear();
        foreach (string groupId in state.CollapsedLayerGroupIds.Where(groupId => !string.IsNullOrWhiteSpace(groupId)))
        {
            _collapsedLayerGroupIds.Add(groupId);
        }
        PruneCollapsedLayerGroups();
        _pixelStudio.SelectionMode = state.SelectionMode;
        _pixelStudio.RectangleRenderMode = state.RectangleRenderMode;
        _pixelStudio.EllipseRenderMode = state.EllipseRenderMode;
        _pixelStudio.ShapePreset = state.ShapePreset;
        _pixelStudio.ShapeRenderMode = state.ShapeRenderMode;
        _pixelStudio.ActivePaletteIndex = Math.Clamp(state.ActivePaletteIndex, 0, Math.Max(_pixelStudio.Palette.Count - 1, 0));
    }

    private void ReplacePixelStudioDocument(PixelStudioState source, bool clearSelection = true)
    {
        ClearSelectionTransformPreview();
        CancelFrameDurationEdit(silent: true);
        if (clearSelection)
        {
            ClearSelection();
        }
        else
        {
            ResetSelectionMoveState(restoreOriginalPixels: _selectionMoveActive);
        }

        _pixelStudio.DocumentName = source.DocumentName;
        _pixelStudio.CanvasWidth = source.CanvasWidth;
        _pixelStudio.CanvasHeight = source.CanvasHeight;
        _pixelStudio.DesiredZoom = source.DesiredZoom;
        _pixelStudio.BrushSize = source.BrushSize;
        InitializeRememberedBrushSizes(source.BrushSize);
        _pixelStudio.CanvasPanX = source.CanvasPanX;
        _pixelStudio.CanvasPanY = source.CanvasPanY;
        _pixelStudio.ShowGrid = source.ShowGrid;
        _pixelStudio.FramesPerSecond = source.FramesPerSecond;
        _pixelStudio.ShowOnionSkin = source.ShowOnionSkin;
        _pixelStudio.ShowPreviousOnion = source.ShowPreviousOnion;
        _pixelStudio.ShowNextOnion = source.ShowNextOnion;
        _pixelStudio.AllowDualOnion = source.AllowDualOnion;
        _pixelStudio.OnionOpacity = source.OnionOpacity;
        _pixelStudio.IsPlaying = source.IsPlaying;
        _pixelStudio.LoopRangeEnabled = source.LoopRangeEnabled;
        _pixelStudio.LoopStartFrameIndex = source.LoopStartFrameIndex;
        _pixelStudio.LoopEndFrameIndex = source.LoopEndFrameIndex;
        _pixelStudio.PlaybackLoopMode = source.PlaybackLoopMode;
        _pixelStudio.ActiveTool = source.ActiveTool;
        _pixelStudio.RectangleRenderMode = source.RectangleRenderMode;
        _pixelStudio.EllipseRenderMode = source.EllipseRenderMode;
        _pixelStudio.ShapePreset = source.ShapePreset;
        _pixelStudio.ShapeRenderMode = source.ShapeRenderMode;
        _pixelStudio.ActivePaletteIndex = source.ActivePaletteIndex;
        _pixelStudio.ActiveFrameIndex = source.ActiveFrameIndex;
        _pixelStudio.ActiveLayerIndex = source.ActiveLayerIndex;
        _pixelStudio.PreviewFrameIndex = source.PreviewFrameIndex;
        _pixelStudio.Palette.Clear();
        _pixelStudio.Palette.AddRange(source.Palette);
        _pixelStudio.Frames.Clear();
        _pixelStudio.Frames.AddRange(source.Frames.Select(CloneFrameState));
        _rectangleRenderMode = source.RectangleRenderMode;
        _ellipseRenderMode = source.EllipseRenderMode;
        _shapePreset = source.ShapePreset;
        _shapeRenderMode = source.ShapeRenderMode;
        RelinkSharedLayerReferences(_pixelStudio.Frames);
        EnsurePixelStudioIndices();
        NormalizeOnionDisplayState();
        ResetPaletteInteractionState();
        InvalidatePixelStudioPixelBuffers();
        _pixelPlaybackDirection = 1;
    }

    private static PixelStudioState ClonePixelStudioState(PixelStudioState source)
    {
        List<PixelStudioFrameState> frames = source.Frames.Select(CloneFrameState).ToList();
        RelinkSharedLayerReferences(frames);

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
            ShowOnionSkin = source.ShowOnionSkin,
            ShowPreviousOnion = source.ShowPreviousOnion,
            ShowNextOnion = source.ShowNextOnion,
            AllowDualOnion = source.AllowDualOnion,
            OnionOpacity = source.OnionOpacity,
            IsPlaying = source.IsPlaying,
            LoopRangeEnabled = source.LoopRangeEnabled,
            LoopStartFrameIndex = source.LoopStartFrameIndex,
            LoopEndFrameIndex = source.LoopEndFrameIndex,
            PlaybackLoopMode = source.PlaybackLoopMode,
            ActiveTool = source.ActiveTool,
            SelectionMode = source.SelectionMode,
            RectangleRenderMode = source.RectangleRenderMode,
            EllipseRenderMode = source.EllipseRenderMode,
            ShapePreset = source.ShapePreset,
            ShapeRenderMode = source.ShapeRenderMode,
            HasSelection = source.HasSelection,
            SelectionCommitted = source.SelectionCommitted,
            SelectionStartX = source.SelectionStartX,
            SelectionStartY = source.SelectionStartY,
            SelectionEndX = source.SelectionEndX,
            SelectionEndY = source.SelectionEndY,
            SelectionMaskIndices = source.SelectionMaskIndices.ToList(),
            ActivePaletteIndex = source.ActivePaletteIndex,
            ActiveFrameIndex = source.ActiveFrameIndex,
            ActiveLayerIndex = source.ActiveLayerIndex,
            PreviewFrameIndex = source.PreviewFrameIndex,
            Palette = source.Palette.ToList(),
            Frames = frames
        };
    }

    private PixelStudioState CapturePixelStudioState()
    {
        PixelStudioState snapshot = ClonePixelStudioState(_pixelStudio);
        snapshot.SelectionMode = _selectionMode;
        snapshot.HasSelection = _selectionActive;
        snapshot.SelectionCommitted = _selectionCommitted;
        snapshot.SelectionStartX = _selectionStartX;
        snapshot.SelectionStartY = _selectionStartY;
        snapshot.SelectionEndX = _selectionEndX;
        snapshot.SelectionEndY = _selectionEndY;
        snapshot.SelectionMaskIndices = SelectionUsesMask()
            ? _selectionMask.OrderBy(index => index).ToList()
            : [];
        return snapshot;
    }

    private void RestorePixelStudioState(PixelStudioState snapshot)
    {
        PixelStudioEditorToolState editorToolState = CapturePixelStudioEditorToolState();
        ReplacePixelStudioDocument(ClonePixelStudioState(snapshot), clearSelection: true);
        ApplyPixelStudioEditorToolState(editorToolState);
        if (snapshot.HasSelection)
        {
            if (snapshot.SelectionMaskIndices.Count > 0)
            {
                ApplySelectionMask(snapshot.SelectionMaskIndices, snapshot.SelectionCommitted);
            }
            else
            {
                _selectionActive = true;
                _selectionCommitted = snapshot.SelectionCommitted;
                _selectionDragActive = false;
                int left = Math.Min(snapshot.SelectionStartX, snapshot.SelectionEndX);
                int top = Math.Min(snapshot.SelectionStartY, snapshot.SelectionEndY);
                int width = Math.Abs(snapshot.SelectionEndX - snapshot.SelectionStartX) + 1;
                int height = Math.Abs(snapshot.SelectionEndY - snapshot.SelectionStartY) + 1;
                SetSelectionRect(left, top, width, height);
            }
        }

        StopPixelPlayback();
    }

    private void InvalidatePixelStudioPixelBuffers()
    {
        _pixelCompositePixels = [];
        _pixelOnionPreviousPixels = [];
        _pixelOnionNextPixels = [];
        _pixelPreviewPixels = [];
        _pixelCompositeFrameIndex = -1;
        _pixelOnionPreviousFrameIndex = -1;
        _pixelOnionNextFrameIndex = -1;
        _pixelPreviewFrameIndex = -1;
        _pixelDirtyIndices.Clear();
        _linePreviewIndices.Clear();
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

            string json = SerializeCurrentPixelStudioSnapshot();
            File.WriteAllText(documentPath, json);
            _currentPixelDocumentPath = documentPath;
            _pixelStudioLastSavedSnapshotJson = json;
            _pixelStudioLastAutosavedSnapshotJson = null;
            _pixelStudioAutosavePending = false;
            _pixelStudioLastAutosaveAt = DateTimeOffset.UtcNow;
            _pixelStudioAutosaveIndicatorUntil = DateTimeOffset.MinValue;
            _pixelRecoveryBannerVisible = false;
            if (_pixelRecoveryOwnedByCurrentSession || !_preserveDeferredRecoveryOnCleanExit)
            {
                PixelStudioRecoveryManager.Clear();
            }

            _pixelRecoveryOwnedByCurrentSession = false;
            _pixelRecoveryBackupCount = PixelStudioRecoveryManager.GetBackupCount();
            RegisterRecentPixelStudioDocument(documentPath);
            RefreshPixelStudioView(
                $"Saved {EditorBranding.PixelToolName} file {Path.GetFileName(documentPath)}.{BuildHiddenArtworkStatusSuffix()}");
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
            WriteCompositePixelsToImage(image, composite, 0);

            image.SaveAsPng(outputPath);
            RefreshPixelStudioView($"Exported PNG to {Path.GetFileName(outputPath)}.");
        }
        catch (Exception ex)
        {
            RefreshPixelStudioView($"PNG export failed: {ex.Message}");
        }
    }

    private void ExportPixelStudioSpriteStrip()
    {
        string initialDirectory = ResolvePixelStudioDocumentDirectory();
        string suggestedFileName = $"{SanitizePixelStudioDocumentName(_pixelStudio.DocumentName)}-strip.png";
        string? exportPath = PixelStudioDocumentFilePicker.ShowPngSaveDialog(initialDirectory, suggestedFileName);
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            RefreshPixelStudioView("Strip export cancelled.");
            return;
        }

        string outputPath = Path.GetExtension(exportPath).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? exportPath
            : $"{exportPath}.png";

        try
        {
            int frameCount = Math.Max(_pixelStudio.Frames.Count, 1);
            using Image<Rgba32> image = new(_pixelStudio.CanvasWidth * frameCount, _pixelStudio.CanvasHeight);
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                List<ThemeColor?> composite = ComposeVisiblePixels(_pixelStudio.Frames[frameIndex]);
                WriteCompositePixelsToImage(image, composite, frameIndex * _pixelStudio.CanvasWidth);
            }

            image.SaveAsPng(outputPath);
            RefreshPixelStudioView($"Exported sprite strip to {Path.GetFileName(outputPath)}.");
        }
        catch (Exception ex)
        {
            RefreshPixelStudioView($"Strip export failed: {ex.Message}");
        }
    }

    private void ExportPixelStudioPngSequence()
    {
        string initialDirectory = ResolvePixelStudioDocumentDirectory();
        string suggestedFileName = $"{SanitizePixelStudioDocumentName(_pixelStudio.DocumentName)}-frames.png";
        string? exportPath = PixelStudioDocumentFilePicker.ShowPngSaveDialog(initialDirectory, suggestedFileName);
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            RefreshPixelStudioView("Sequence export cancelled.");
            return;
        }

        string outputPath = Path.GetExtension(exportPath).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? exportPath
            : $"{exportPath}.png";
        string directory = Path.GetDirectoryName(outputPath) ?? initialDirectory;
        string fileStem = Path.GetFileNameWithoutExtension(outputPath);

        try
        {
            Directory.CreateDirectory(directory);
            for (int frameIndex = 0; frameIndex < _pixelStudio.Frames.Count; frameIndex++)
            {
                List<ThemeColor?> composite = ComposeVisiblePixels(_pixelStudio.Frames[frameIndex]);
                using Image<Rgba32> image = new(_pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight);
                WriteCompositePixelsToImage(image, composite, 0);
                string framePath = Path.Combine(directory, $"{fileStem}_{frameIndex + 1:D3}.png");
                image.SaveAsPng(framePath);
            }

            RefreshPixelStudioView($"Exported {_pixelStudio.Frames.Count} PNG frame(s) to {Path.GetFileName(directory)}.");
        }
        catch (Exception ex)
        {
            RefreshPixelStudioView($"Sequence export failed: {ex.Message}");
        }
    }

    private void ExportPixelStudioGif()
    {
        string initialDirectory = ResolvePixelStudioDocumentDirectory();
        string suggestedFileName = $"{SanitizePixelStudioDocumentName(_pixelStudio.DocumentName)}.gif";
        string? exportPath = PixelStudioDocumentFilePicker.ShowGifSaveDialog(initialDirectory, suggestedFileName);
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            RefreshPixelStudioView("GIF export cancelled.");
            return;
        }

        string outputPath = Path.GetExtension(exportPath).Equals(".gif", StringComparison.OrdinalIgnoreCase)
            ? exportPath
            : $"{exportPath}.gif";

        try
        {
            using Image<Rgba32> gifImage = BuildFrameImage(_pixelStudio.Frames[0]);
            GifMetadata gifMetadata = gifImage.Metadata.GetGifMetadata();
            gifMetadata.RepeatCount = 0;
            gifImage.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = Math.Max(1, GetFrameDurationMilliseconds(0) / 10);

            for (int frameIndex = 1; frameIndex < _pixelStudio.Frames.Count; frameIndex++)
            {
                using Image<Rgba32> frameImage = BuildFrameImage(_pixelStudio.Frames[frameIndex]);
                frameImage.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = Math.Max(1, GetFrameDurationMilliseconds(frameIndex) / 10);
                gifImage.Frames.AddFrame(frameImage.Frames.RootFrame);
            }

            gifImage.SaveAsGif(outputPath);
            RefreshPixelStudioView($"Exported GIF to {Path.GetFileName(outputPath)}.");
        }
        catch (Exception ex)
        {
            RefreshPixelStudioView($"GIF export failed: {ex.Message}");
        }
    }

    private Image<Rgba32> BuildFrameImage(PixelStudioFrameState frame)
    {
        List<ThemeColor?> composite = ComposeVisiblePixels(frame);
        Image<Rgba32> image = new(_pixelStudio.CanvasWidth, _pixelStudio.CanvasHeight);
        WriteCompositePixelsToImage(image, composite, 0);
        return image;
    }

    private void WriteCompositePixelsToImage(Image<Rgba32> image, IReadOnlyList<ThemeColor?> composite, int offsetX)
    {
        for (int y = 0; y < _pixelStudio.CanvasHeight; y++)
        {
            for (int x = 0; x < _pixelStudio.CanvasWidth; x++)
            {
                ThemeColor? pixelColor = composite[(y * _pixelStudio.CanvasWidth) + x];
                if (pixelColor is null)
                {
                    image[offsetX + x, y] = new Rgba32(0, 0, 0, 0);
                    continue;
                }

                image[offsetX + x, y] = new Rgba32(
                    ToColorByte(pixelColor.Value.R),
                    ToColorByte(pixelColor.Value.G),
                    ToColorByte(pixelColor.Value.B),
                    ToColorByte(pixelColor.Value.A));
            }
        }
    }

    private int GetDefaultFrameDurationMilliseconds()
    {
        return GetFrameDurationFromFramesPerSecond(_pixelStudio.FramesPerSecond);
    }

    private static int GetFrameDurationFromFramesPerSecond(int framesPerSecond)
    {
        return Math.Clamp((int)MathF.Round(1000f / Math.Max(framesPerSecond, 1)), 40, 1000);
    }

    private bool TryGetBufferedFrameDuration(out int durationMilliseconds)
    {
        durationMilliseconds = 0;
        if (!_frameDurationFieldActive || string.IsNullOrWhiteSpace(_frameDurationBuffer))
        {
            return false;
        }

        if (!int.TryParse(_frameDurationBuffer, out int parsedDuration))
        {
            return false;
        }

        durationMilliseconds = Math.Clamp(parsedDuration, 40, 1000);
        return true;
    }

    private int GetFrameDurationMilliseconds(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _pixelStudio.Frames.Count)
        {
            return GetDefaultFrameDurationMilliseconds();
        }

        if (_frameDurationFieldActive
            && frameIndex == Math.Clamp(_pixelStudio.ActiveFrameIndex, 0, _pixelStudio.Frames.Count - 1)
            && TryGetBufferedFrameDuration(out int bufferedDurationMilliseconds))
        {
            return bufferedDurationMilliseconds;
        }

        return Math.Clamp(_pixelStudio.Frames[frameIndex].DurationMilliseconds, 40, 1000);
    }

    private string BuildUniqueFrameName(string baseName)
    {
        string requested = SanitizeFrameName(baseName);
        HashSet<string> existingNames = _pixelStudio.Frames
            .Select(frame => frame.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(requested))
        {
            return requested;
        }

        int suffix = 2;
        string candidate = $"{requested} {suffix}";
        while (existingNames.Contains(candidate))
        {
            suffix++;
            candidate = $"{requested} {suffix}";
        }

        return candidate;
    }

    private string BuildUniqueLayerName(string baseName)
    {
        string requested = SanitizeLayerName(baseName);
        HashSet<string> existingNames = CurrentPixelFrame.Layers
            .Select(layer => layer.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(requested))
        {
            return requested;
        }

        int suffix = 2;
        string candidate = $"{requested} {suffix}";
        while (existingNames.Contains(candidate))
        {
            suffix++;
            candidate = $"{requested} {suffix}";
        }

        return candidate;
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
        RefreshPixelStudioCameraLayout("Canvas fitted to viewport.");
    }

    private void ResetCanvasView()
    {
        _pixelStudio.DesiredZoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.CanvasWidth >= 128 || _pixelStudio.CanvasHeight >= 128 ? 8 : 24);
        _pixelStudio.CanvasPanX = 0;
        _pixelStudio.CanvasPanY = 0;
        LogCurrentCanvasCamera("ResetRequested");
        RefreshPixelStudioCameraLayout("Canvas view reset.");
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
            _pixelRecoveryOwnedByCurrentSession = false;
            _pixelRecoveryBannerVisible = false;
            ResetPixelStudioRecoveryTracking(useCurrentAsSavedBaseline: true, useCurrentAsAutosavedBaseline: false);
            RegisterRecentPixelStudioDocument(documentPath);
            status = $"Opened {EditorBranding.PixelToolName} file {Path.GetFileName(documentPath)}.{BuildHiddenArtworkStatusSuffix()}";
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
        NormalizeLoopRangeState();
        return new PixelStudioProjectDocument
        {
            DocumentName = _pixelStudio.DocumentName,
            UsesDirectColorPixels = true,
            CanvasWidth = _pixelStudio.CanvasWidth,
            CanvasHeight = _pixelStudio.CanvasHeight,
            DesiredZoom = _pixelStudio.DesiredZoom,
            BrushSize = _pixelStudio.BrushSize,
            CanvasPanX = _pixelStudio.CanvasPanX,
            CanvasPanY = _pixelStudio.CanvasPanY,
            ShowGrid = _pixelStudio.ShowGrid,
            FramesPerSecond = _pixelStudio.FramesPerSecond,
            ShowOnionSkin = _pixelStudio.ShowOnionSkin,
            ShowPreviousOnion = _pixelStudio.ShowPreviousOnion,
            ShowNextOnion = _pixelStudio.ShowNextOnion,
            AllowDualOnion = _pixelStudio.AllowDualOnion,
            OnionOpacity = _pixelStudio.OnionOpacity,
            IsPlaying = false,
            PreviewFrameIndex = _pixelStudio.PreviewFrameIndex,
            LoopRangeEnabled = _pixelStudio.LoopRangeEnabled,
            LoopStartFrameIndex = _pixelStudio.LoopStartFrameIndex,
            LoopEndFrameIndex = _pixelStudio.LoopEndFrameIndex,
            PlaybackLoopMode = _pixelStudio.PlaybackLoopMode,
            ActiveTool = _pixelStudio.ActiveTool,
            RectangleRenderMode = _rectangleRenderMode,
            EllipseRenderMode = _ellipseRenderMode,
            ShapePreset = _shapePreset,
            ShapeRenderMode = _shapeRenderMode,
            ActivePaletteIndex = _pixelStudio.ActivePaletteIndex,
            ActiveFrameIndex = _pixelStudio.ActiveFrameIndex,
            ActiveLayerIndex = _pixelStudio.ActiveLayerIndex,
            Palette = _pixelStudio.Palette.Select(ToPaletteColorSetting).ToList(),
            Frames = _pixelStudio.Frames.Select(frame => new PixelStudioProjectFrameDocument
            {
                Name = frame.Name,
                DurationMilliseconds = frame.DurationMilliseconds,
                Layers = frame.Layers.Select(layer => new PixelStudioProjectLayerDocument
                {
                    Name = layer.Name,
                    GroupId = layer.GroupId,
                    GroupName = layer.GroupName,
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked,
                    IsAlphaLocked = layer.IsAlphaLocked,
                    IsSharedAcrossFrames = layer.IsSharedAcrossFrames,
                    Opacity = NormalizeLayerOpacity(layer.Opacity),
                    Pixels = layer.Pixels.ToArray()
                }).ToList()
            }).ToList()
        };
    }

    private static PixelStudioState CreatePixelStudioState(PixelStudioProjectDocument document)
    {
        int width = Math.Clamp(document.CanvasWidth, 1, MaxPixelCanvasDimension);
        int height = Math.Clamp(document.CanvasHeight, 1, MaxPixelCanvasDimension);
        int pixelCount = width * height;
        List<ThemeColor> palette = document.Palette.Select(ToThemeColor).ToList();
        IReadOnlyList<ThemeColor> legacyPalette = palette.Count > 0 ? palette : CreateDefaultPalette();

        List<PixelStudioFrameState> frames = document.Frames
            .Select(frame => new PixelStudioFrameState
            {
                Name = string.IsNullOrWhiteSpace(frame.Name) ? "Frame" : frame.Name,
                DurationMilliseconds = Math.Clamp(frame.DurationMilliseconds, 40, 1000),
                Layers = frame.Layers.Select(layer => new PixelStudioLayerState
                {
                    Name = string.IsNullOrWhiteSpace(layer.Name) ? "Layer" : layer.Name,
                    GroupId = string.IsNullOrWhiteSpace(layer.GroupId) ? null : layer.GroupId,
                    GroupName = string.IsNullOrWhiteSpace(layer.GroupName) ? null : layer.GroupName,
                    IsVisible = layer.IsVisible,
                    IsLocked = layer.IsLocked,
                    IsAlphaLocked = layer.IsAlphaLocked,
                    IsSharedAcrossFrames = layer.IsSharedAcrossFrames,
                    Opacity = NormalizeLayerOpacity(layer.Opacity),
                    Pixels = document.UsesDirectColorPixels
                        ? NormalizeDirectPixelBuffer(layer.Pixels, pixelCount)
                        : ConvertLegacyIndexedPixelBuffer(layer.Pixels, pixelCount, legacyPalette)
                }).ToList()
            })
            .Where(frame => frame.Layers.Count > 0)
            .ToList();

        if (frames.Count == 0)
        {
            frames.Add(new PixelStudioFrameState
            {
                Name = "Frame 1",
                DurationMilliseconds = 125,
                Layers =
                [
                    new PixelStudioLayerState
                    {
                        Name = "Layer 1",
                        Opacity = 1f,
                        Pixels = CreateBlankPixels(width, height)
                    }
                ]
            });
        }

        RelinkSharedLayerReferences(frames);

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
            ShowOnionSkin = document.ShowOnionSkin,
            ShowPreviousOnion = document.ShowPreviousOnion,
            ShowNextOnion = document.ShowNextOnion,
            AllowDualOnion = document.AllowDualOnion,
            OnionOpacity = Math.Clamp(document.OnionOpacity, 0f, 1f),
            IsPlaying = false,
            PreviewFrameIndex = document.PreviewFrameIndex,
            LoopRangeEnabled = document.LoopRangeEnabled,
            LoopStartFrameIndex = document.LoopStartFrameIndex,
            LoopEndFrameIndex = document.LoopEndFrameIndex,
            PlaybackLoopMode = document.PlaybackLoopMode,
            ActiveTool = document.ActiveTool,
            RectangleRenderMode = document.RectangleRenderMode,
            EllipseRenderMode = document.EllipseRenderMode,
            ShapePreset = document.ShapePreset,
            ShapeRenderMode = document.ShapeRenderMode,
            ActivePaletteIndex = document.ActivePaletteIndex,
            ActiveFrameIndex = document.ActiveFrameIndex,
            ActiveLayerIndex = document.ActiveLayerIndex,
            Palette = palette,
            Frames = frames
        };
    }

    private static int[] NormalizeDirectPixelBuffer(IReadOnlyList<int> pixels, int pixelCount)
    {
        int[] normalized = new int[pixelCount];
        Array.Fill(normalized, TransparentPixelValue);
        int length = Math.Min(pixelCount, pixels.Count);
        for (int index = 0; index < length; index++)
        {
            normalized[index] = pixels[index];
        }

        return normalized;
    }

    private static int[] ConvertLegacyIndexedPixelBuffer(IReadOnlyList<int> pixels, int pixelCount, IReadOnlyList<ThemeColor> palette)
    {
        int[] normalized = new int[pixelCount];
        Array.Fill(normalized, TransparentPixelValue);
        int length = Math.Min(pixelCount, pixels.Count);
        for (int index = 0; index < length; index++)
        {
            int paletteIndex = pixels[index];
            if (paletteIndex < 0 || paletteIndex >= palette.Count)
            {
                continue;
            }

            normalized[index] = PackPixelColor(palette[paletteIndex]);
        }

        return normalized;
    }

    private string BuildHiddenArtworkStatusSuffix()
    {
        int hiddenArtworkLayerCount = CountHiddenArtworkLayers();
        if (hiddenArtworkLayerCount <= 0)
        {
            return string.Empty;
        }

        return hiddenArtworkLayerCount == 1
            ? " Hidden layer artwork was preserved."
            : $" Hidden artwork was preserved on {hiddenArtworkLayerCount} hidden layers.";
    }

    private string BuildPixelStudioRecoveryStatusMessage()
    {
        string baseMessage = _pixelRecoveryBackupCount > 0
            ? $"Recovered session restored. Backups kept: {_pixelRecoveryBackupCount}."
            : "Recovered session restored.";
        string hiddenArtworkSuffix = BuildHiddenArtworkStatusSuffix();
        return string.IsNullOrWhiteSpace(hiddenArtworkSuffix)
            ? $"{baseMessage} Save when you're ready."
            : $"{baseMessage} Save when you're ready.{hiddenArtworkSuffix}";
    }

    private int CountHiddenArtworkLayers()
    {
        return _pixelStudio.Frames
            .SelectMany(frame => frame.Layers)
            .Count(layer => !layer.IsVisible && LayerContainsArtwork(layer));
    }

    private static bool LayerContainsArtwork(PixelStudioLayerState layer)
    {
        for (int index = 0; index < layer.Pixels.Length; index++)
        {
            if (!IsTransparentPixel(layer.Pixels[index]))
            {
                return true;
            }
        }

        return false;
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

        return AppStoragePaths.DefaultProjectLibraryPath;
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

    private void RegisterRecentPixelStudioDocument(string documentPath)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return;
        }

        string normalizedDocumentPath = Path.GetFullPath(documentPath);
        string? directory = Path.GetDirectoryName(normalizedDocumentPath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            _lastProjectPath = directory;
        }

        AddRecentProject(new RecentProjectEntry
        {
            Name = Path.GetFileNameWithoutExtension(normalizedDocumentPath),
            Path = normalizedDocumentPath
        });
    }

    private static string? FindPreferredPixelStudioDocument(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return null;
        }

        if (File.Exists(projectDirectory))
        {
            return projectDirectory;
        }

        if (!Directory.Exists(projectDirectory))
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
            DurationMilliseconds = frame.DurationMilliseconds,
            Layers = frame.Layers.Select(CloneLayerState).ToList()
        };
    }

    private static PixelStudioLayerState CloneLayerState(PixelStudioLayerState layer)
    {
        return new PixelStudioLayerState
        {
            Name = layer.Name,
            GroupId = layer.GroupId,
            GroupName = layer.GroupName,
            IsVisible = layer.IsVisible,
            IsLocked = layer.IsLocked,
            IsAlphaLocked = layer.IsAlphaLocked,
            IsSharedAcrossFrames = layer.IsSharedAcrossFrames,
            Opacity = NormalizeLayerOpacity(layer.Opacity),
            Pixels = layer.Pixels.ToArray()
        };
    }

    private static void RelinkSharedLayerReferences(IReadOnlyList<PixelStudioFrameState> frames)
    {
        if (frames.Count == 0)
        {
            return;
        }

        int layerCount = frames.Min(frame => frame.Layers.Count);
        for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            if (!frames.All(frame => frame.Layers[layerIndex].IsSharedAcrossFrames))
            {
                continue;
            }

            int[] sharedPixels = frames[0].Layers[layerIndex].Pixels;
            foreach (PixelStudioFrameState frame in frames)
            {
                frame.Layers[layerIndex].IsSharedAcrossFrames = true;
                frame.Layers[layerIndex].Pixels = sharedPixels;
            }
        }
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
            DurationMilliseconds = 125,
            Layers = [colorLayer, detailLayer]
        };
    }

    private static void ApplyPattern(PixelStudioLayerState layer, IReadOnlyList<string> pattern, IReadOnlyDictionary<char, int> paletteMap)
    {
        IReadOnlyList<ThemeColor> defaultPalette = CreateDefaultPalette();
        for (int y = 0; y < pattern.Count; y++)
        {
            for (int x = 0; x < pattern[y].Length; x++)
            {
                char symbol = pattern[y][x];
                if (paletteMap.TryGetValue(symbol, out int paletteIndex)
                    && paletteIndex >= 0
                    && paletteIndex < defaultPalette.Count)
                {
                    layer.Pixels[(y * 16) + x] = PackPixelColor(defaultPalette[paletteIndex]);
                }
            }
        }
    }

    private static int[] CreateBlankPixels(int width, int height)
    {
        int[] pixels = new int[width * height];
        Array.Fill(pixels, TransparentPixelValue);
        return pixels;
    }

    private static bool IsTransparentPixel(int pixelValue)
    {
        return ((uint)pixelValue >> 24) == 0u;
    }

    private static int PackPixelColor(ThemeColor color)
    {
        int alpha = ToColorByte(color.A);
        if (alpha <= 0)
        {
            return TransparentPixelValue;
        }

        int red = ToColorByte(color.R);
        int green = ToColorByte(color.G);
        int blue = ToColorByte(color.B);
        return (alpha << 24) | (red << 16) | (green << 8) | blue;
    }

    private static ThemeColor? UnpackPixelColor(int pixelValue)
    {
        byte alpha = (byte)((uint)pixelValue >> 24);
        if (alpha == 0)
        {
            return null;
        }

        byte red = (byte)((uint)pixelValue >> 16);
        byte green = (byte)((uint)pixelValue >> 8);
        byte blue = (byte)pixelValue;
        return new ThemeColor(red / 255f, green / 255f, blue / 255f, alpha / 255f);
    }

    private int GetCurrentToolPixelValue()
    {
        return _pixelStudio.ActiveTool == PixelStudioToolKind.Eraser
            ? TransparentPixelValue
            : PackPixelColor(GetActivePaletteColor());
    }

    private static string ToHex(ThemeColor color)
    {
        int r = Math.Clamp((int)MathF.Round(color.R * 255), 0, 255);
        int g = Math.Clamp((int)MathF.Round(color.G * 255), 0, 255);
        int b = Math.Clamp((int)MathF.Round(color.B * 255), 0, 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static ThemeColor FromHsv(float hue, float saturation, float value, float alpha = 1f)
    {
        hue = ((hue % 360f) + 360f) % 360f;
        saturation = Math.Clamp(saturation, 0f, 1f);
        value = Math.Clamp(value, 0f, 1f);
        alpha = Math.Clamp(alpha, 0f, 1f);

        if (saturation <= 0.0001f)
        {
            return new ThemeColor(value, value, value, alpha);
        }

        float sector = hue / 60f;
        int sectorIndex = (int)MathF.Floor(sector);
        float fraction = sector - sectorIndex;
        float p = value * (1f - saturation);
        float q = value * (1f - (saturation * fraction));
        float t = value * (1f - (saturation * (1f - fraction)));

        return sectorIndex switch
        {
            0 => new ThemeColor(value, t, p, alpha),
            1 => new ThemeColor(q, value, p, alpha),
            2 => new ThemeColor(p, value, t, alpha),
            3 => new ThemeColor(p, q, value, alpha),
            4 => new ThemeColor(t, p, value, alpha),
            _ => new ThemeColor(value, p, q, alpha)
        };
    }

    private static (float Hue, float Saturation, float Value) ToHsv(ThemeColor color)
    {
        float max = Math.Max(color.R, Math.Max(color.G, color.B));
        float min = Math.Min(color.R, Math.Min(color.G, color.B));
        float delta = max - min;
        float hue;

        if (delta <= 0.0001f)
        {
            hue = 0f;
        }
        else if (Math.Abs(max - color.R) < 0.0001f)
        {
            hue = 60f * (((color.G - color.B) / delta) % 6f);
        }
        else if (Math.Abs(max - color.G) < 0.0001f)
        {
            hue = 60f * (((color.B - color.R) / delta) + 2f);
        }
        else
        {
            hue = 60f * (((color.R - color.G) / delta) + 4f);
        }

        if (hue < 0f)
        {
            hue += 360f;
        }

        float saturation = max <= 0.0001f ? 0f : delta / max;
        return (hue, saturation, max);
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
            ClearLockedPaletteProtection();
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
            ClearLockedPaletteProtection();
            return;
        }

        _selectedPixelPaletteId = activePalette.Id;
        ReplaceCurrentPaletteColors(activePalette.Colors.Select(ToThemeColor).ToList(), remapArtwork: false);
        _paletteRenameBuffer = activePalette.Name;
        if (activePalette.IsLocked)
        {
            SetLockedPaletteProtection(activePalette);
        }
        else
        {
            ClearLockedPaletteProtection();
        }
    }

    private void MarkCurrentPaletteAsUnsaved()
    {
        string? selectionId = !string.IsNullOrWhiteSpace(_selectedPixelPaletteId)
            ? _selectedPixelPaletteId
            : _activePixelPaletteId;
        _activePixelPaletteId = null;
        _selectedPixelPaletteId = string.Equals(selectionId, DefaultPaletteSelectionId, StringComparison.Ordinal)
            ? DefaultPaletteSelectionId
            : _savedPixelPalettes.Any(palette => string.Equals(palette.Id, selectionId, StringComparison.Ordinal))
                ? selectionId
                : null;
        ClearLockedPaletteProtection();
    }

    private string GetActivePaletteDisplayName()
    {
        if (string.IsNullOrWhiteSpace(_activePixelPaletteId))
        {
            if (string.Equals(_selectedPixelPaletteId, DefaultPaletteSelectionId, StringComparison.Ordinal))
            {
                return "Default Palette";
            }

            return "Working Palette";
        }

        return _savedPixelPalettes
            .FirstOrDefault(palette => string.Equals(palette.Id, _activePixelPaletteId, StringComparison.Ordinal))
            ?.Name ?? "Working Palette";
    }

    private void ApplySavedPalette(SavedPixelPalette palette)
    {
        ReplaceCurrentPaletteColors(palette.Colors.Select(ToThemeColor).ToList(), remapArtwork: false);
        _activePixelPaletteId = palette.Id;
        _selectedPixelPaletteId = palette.Id;
        _paletteRenameActive = false;
        _layerRenameActive = false;
        _paletteRenameBuffer = palette.Name;
        if (palette.IsLocked)
        {
            SetLockedPaletteProtection(palette);
        }
        else
        {
            ClearLockedPaletteProtection();
        }
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
        ClearLockedPaletteProtection();
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

        if (selected is not null)
        {
            _selectedPixelPaletteId = selected.Id;
        }

        return selected;
    }

    private void SetLockedPaletteProtection(SavedPixelPalette palette)
    {
        _lockedPixelPaletteSourceId = palette.Id;
    }

    private void ClearLockedPaletteProtection()
    {
        _lockedPixelPaletteSourceId = null;
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

    private string BuildDuplicatePaletteName(string sourceName)
    {
        HashSet<string> existingNames = _savedPixelPalettes
            .Select(palette => palette.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string baseName = string.IsNullOrWhiteSpace(sourceName) ? "Palette" : sourceName.Trim();
        string candidate = $"{baseName} Copy";
        int copyNumber = 2;
        while (existingNames.Contains(candidate))
        {
            candidate = $"{baseName} Copy {copyNumber}";
            copyNumber++;
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
        List<ThemeColor> normalizedPalette = newPalette.ToList();

        _pixelStudio.Palette.Clear();
        _pixelStudio.Palette.AddRange(normalizedPalette);
        _pixelStudio.ActivePaletteIndex = _pixelStudio.Palette.Count == 0
            ? 0
            : Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, _pixelStudio.Palette.Count - 1);
        ResetPaletteInteractionState();
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
            IsLocked = palette.IsLocked,
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

    private void UpdatePaletteSwatchReorderDrag(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (_paletteSwatchDragSourceIndex < 0 || _paletteSwatchDragSourceIndex >= _pixelStudio.Palette.Count)
        {
            return;
        }

        float deltaX = mouseX - _paletteSwatchDragStartMouseX;
        float deltaY = mouseY - _paletteSwatchDragStartMouseY;
        if (!_paletteSwatchDragMoved && ((deltaX * deltaX) + (deltaY * deltaY)) >= 16f)
        {
            _paletteSwatchDragMoved = true;
            RefreshPixelStudioInteraction();
        }

        if (!_paletteSwatchDragMoved)
        {
            return;
        }

        int nextTargetIndex = ResolvePaletteSwatchReorderTargetIndex(layout, mouseX, mouseY, _paletteSwatchDragSourceIndex);
        if (nextTargetIndex != _paletteSwatchDragTargetIndex)
        {
            _paletteSwatchDragTargetIndex = nextTargetIndex;
            RefreshPixelStudioInteraction();
        }
    }

    private int ResolvePaletteSwatchReorderTargetIndex(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, int fallbackIndex)
    {
        if (layout.PaletteSwatches.Count == 0 || _pixelStudio.Palette.Count == 0)
        {
            return Math.Clamp(fallbackIndex, 0, Math.Max(_pixelStudio.Palette.Count - 1, 0));
        }

        IndexedRect? hoveredSwatch = layout.PaletteSwatches.FirstOrDefault(entry => entry.Rect.Contains(mouseX, mouseY));
        if (hoveredSwatch is not null)
        {
            return Math.Clamp(hoveredSwatch.Index, 0, _pixelStudio.Palette.Count - 1);
        }

        if (layout.PaletteSwatchViewportRect is null || !layout.PaletteSwatchViewportRect.Value.Contains(mouseX, mouseY))
        {
            return Math.Clamp(fallbackIndex, 0, _pixelStudio.Palette.Count - 1);
        }

        IndexedRect closestSwatch = layout.PaletteSwatches
            .OrderBy(entry =>
            {
                float centerX = entry.Rect.X + (entry.Rect.Width * 0.5f);
                float centerY = entry.Rect.Y + (entry.Rect.Height * 0.5f);
                float dx = centerX - mouseX;
                float dy = centerY - mouseY;
                return (dx * dx) + (dy * dy);
            })
            .First();
        return Math.Clamp(closestSwatch.Index, 0, _pixelStudio.Palette.Count - 1);
    }

    private void CompletePaletteSwatchReorderDrag()
    {
        int sourceIndex = _paletteSwatchDragSourceIndex;
        int targetIndex = _paletteSwatchDragTargetIndex;
        bool moved = _paletteSwatchDragMoved;
        _paletteSwatchDragSourceIndex = -1;
        _paletteSwatchDragTargetIndex = -1;
        _paletteSwatchDragStartMouseX = 0f;
        _paletteSwatchDragStartMouseY = 0f;
        _paletteSwatchDragMoved = false;

        if (sourceIndex < 0 || sourceIndex >= _pixelStudio.Palette.Count)
        {
            RefreshPixelStudioInteraction();
            return;
        }

        if (!moved)
        {
            TrySelectActivePaletteIndex(sourceIndex);
            RefreshPixelStudioView($"{EditorBranding.PixelToolName} color set to {ToHex(GetActivePaletteColor())}.");
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, _pixelStudio.Palette.Count - 1);
        if (targetIndex == sourceIndex)
        {
            RefreshPixelStudioInteraction();
            return;
        }

        if (IsCurrentPaletteLocked())
        {
            OpenPixelWarningDialog(
                PixelStudioWarningDialogKind.ModifyLockedPalette,
                "Palette Is Locked",
                $"{GetLockedPaletteSourceName()} is locked. Unlock the palette, reorder swatches, or cancel.",
                confirmAction: () =>
                {
                    if (!UnlockLockedPaletteSource())
                    {
                        RefreshPixelStudioView("Could not unlock the current palette.", rebuildLayout: true);
                        return;
                    }

                    ReorderPaletteSwatch(sourceIndex, targetIndex);
                });
            return;
        }

        ReorderPaletteSwatch(sourceIndex, targetIndex);
    }

    private void ReorderPaletteSwatch(int sourceIndex, int targetIndex)
    {
        ApplyPixelStudioChange("Reordered palette swatches.", () =>
        {
            if (sourceIndex < 0
                || sourceIndex >= _pixelStudio.Palette.Count
                || targetIndex < 0
                || targetIndex >= _pixelStudio.Palette.Count
                || sourceIndex == targetIndex)
            {
                return false;
            }

            ThemeColor movedColor = _pixelStudio.Palette[sourceIndex];
            _pixelStudio.Palette.RemoveAt(sourceIndex);
            _pixelStudio.Palette.Insert(targetIndex, movedColor);

            if (_pixelStudio.ActivePaletteIndex == sourceIndex)
            {
                _pixelStudio.ActivePaletteIndex = targetIndex;
            }
            else if (sourceIndex < _pixelStudio.ActivePaletteIndex && targetIndex >= _pixelStudio.ActivePaletteIndex)
            {
                _pixelStudio.ActivePaletteIndex--;
            }
            else if (sourceIndex > _pixelStudio.ActivePaletteIndex && targetIndex <= _pixelStudio.ActivePaletteIndex)
            {
                _pixelStudio.ActivePaletteIndex++;
            }

            _pixelStudio.ActivePaletteIndex = Math.Clamp(_pixelStudio.ActivePaletteIndex, 0, Math.Max(_pixelStudio.Palette.Count - 1, 0));
            MarkCurrentPaletteAsUnsaved();
            return true;
        });
    }

    private void UpdateFrameReorderDrag(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (_frameDragSourceIndex < 0 || _frameDragSourceIndex >= _pixelStudio.Frames.Count)
        {
            return;
        }

        float deltaX = mouseX - _frameDragStartMouseX;
        float deltaY = mouseY - _frameDragStartMouseY;
        if (!_frameDragMoved && ((deltaX * deltaX) + (deltaY * deltaY)) >= 16f)
        {
            _frameDragMoved = true;
            RefreshPixelStudioInteraction();
        }

        if (!_frameDragMoved)
        {
            return;
        }

        int nextInsertIndex = ResolveFrameInsertIndex(layout, mouseX, mouseY, _frameDragSourceIndex);
        if (nextInsertIndex != _frameDragInsertIndex)
        {
            _frameDragInsertIndex = nextInsertIndex;
            RefreshPixelStudioInteraction();
        }
    }

    private int ResolveFrameInsertIndex(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, int fallbackIndex)
    {
        if (layout.FrameRows.Count == 0)
        {
            return Math.Clamp(fallbackIndex, 0, _pixelStudio.Frames.Count);
        }

        IndexedRect? hoveredRow = layout.FrameRows.FirstOrDefault(row => row.Rect.Contains(mouseX, mouseY));
        if (hoveredRow is not null)
        {
            return mouseX <= hoveredRow.Rect.X + (hoveredRow.Rect.Width * 0.5f)
                ? hoveredRow.Index
                : hoveredRow.Index + 1;
        }

        if (layout.FrameListViewportRect is null || !layout.FrameListViewportRect.Value.Contains(mouseX, mouseY))
        {
            return Math.Clamp(fallbackIndex, 0, _pixelStudio.Frames.Count);
        }

        IndexedRect closestRow = layout.FrameRows
            .OrderBy(row =>
            {
                float centerX = row.Rect.X + (row.Rect.Width * 0.5f);
                float centerY = row.Rect.Y + (row.Rect.Height * 0.5f);
                float dx = centerX - mouseX;
                float dy = centerY - mouseY;
                return (dx * dx) + (dy * dy);
            })
            .First();
        return mouseX <= closestRow.Rect.X + (closestRow.Rect.Width * 0.5f)
            ? closestRow.Index
            : closestRow.Index + 1;
    }

    private void UpdateLayerReorderDrag(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (_layerDragSourceIndex < 0 || _layerDragSourceIndex >= CurrentPixelFrame.Layers.Count)
        {
            return;
        }

        float deltaX = mouseX - _layerDragStartMouseX;
        float deltaY = mouseY - _layerDragStartMouseY;
        if (!_layerDragMoved && ((deltaX * deltaX) + (deltaY * deltaY)) >= 16f)
        {
            _layerDragMoved = true;
            RefreshPixelStudioInteraction();
        }

        if (!_layerDragMoved)
        {
            return;
        }

        string? nextJoinGroupId = ResolveLayerDragJoinGroupId(layout, mouseX, mouseY, _layerDragSourceIndex, out int nextJoinAnchorLayerIndex);
        int nextInsertIndex = ResolveLayerInsertIndex(layout, mouseX, mouseY, _layerDragSourceIndex);
        if (nextInsertIndex != _layerDragInsertIndex
            || !string.Equals(nextJoinGroupId, _layerDragJoinGroupId, StringComparison.Ordinal)
            || nextJoinAnchorLayerIndex != _layerDragJoinAnchorLayerIndex)
        {
            _layerDragInsertIndex = nextInsertIndex;
            _layerDragJoinGroupId = nextJoinGroupId;
            _layerDragJoinAnchorLayerIndex = nextJoinAnchorLayerIndex;
            RefreshPixelStudioInteraction();
        }
    }

    private string? ResolveLayerDragJoinGroupId(
        PixelStudioLayoutSnapshot layout,
        float mouseX,
        float mouseY,
        int sourceLayerIndex,
        out int anchorLayerIndex)
    {
        anchorLayerIndex = -1;
        if (layout.LayerListViewportRect is null || !layout.LayerListViewportRect.Value.Contains(mouseX, mouseY))
        {
            return null;
        }

        PixelStudioLayerGroupHeaderRect? hoveredGroup = layout.LayerGroupRows.FirstOrDefault(row => row.Rect.Contains(mouseX, mouseY));
        if (hoveredGroup is not null)
        {
            if (hoveredGroup.FirstLayerIndex == sourceLayerIndex)
            {
                return null;
            }

            anchorLayerIndex = hoveredGroup.FirstLayerIndex;
            return hoveredGroup.GroupId;
        }

        IndexedRect? hoveredLayerRow = layout.LayerRows.FirstOrDefault(row => row.Rect.Contains(mouseX, mouseY));
        if (hoveredLayerRow is null || hoveredLayerRow.Index == sourceLayerIndex)
        {
            return null;
        }

        PixelStudioLayerState hoveredLayer = CurrentPixelFrame.Layers[Math.Clamp(hoveredLayerRow.Index, 0, CurrentPixelFrame.Layers.Count - 1)];
        if (string.IsNullOrWhiteSpace(hoveredLayer.GroupId))
        {
            return null;
        }

        float innerTop = hoveredLayerRow.Rect.Y + (hoveredLayerRow.Rect.Height * 0.22f);
        float innerBottom = hoveredLayerRow.Rect.Y + (hoveredLayerRow.Rect.Height * 0.78f);
        if (mouseY < innerTop || mouseY > innerBottom)
        {
            return null;
        }

        anchorLayerIndex = hoveredLayerRow.Index;
        return hoveredLayer.GroupId;
    }

    private int ResolveLayerInsertIndex(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY, int fallbackIndex)
    {
        if (layout.LayerRows.Count == 0)
        {
            return Math.Clamp(fallbackIndex, 0, CurrentPixelFrame.Layers.Count);
        }

        IndexedRect? hoveredRow = layout.LayerRows.FirstOrDefault(row => row.Rect.Contains(mouseX, mouseY));
        if (hoveredRow is not null)
        {
            return mouseY <= hoveredRow.Rect.Y + (hoveredRow.Rect.Height * 0.5f)
                ? hoveredRow.Index
                : hoveredRow.Index + 1;
        }

        if (layout.LayerListViewportRect is null || !layout.LayerListViewportRect.Value.Contains(mouseX, mouseY))
        {
            return Math.Clamp(fallbackIndex, 0, CurrentPixelFrame.Layers.Count);
        }

        IndexedRect closestRow = layout.LayerRows
            .OrderBy(row =>
            {
                float centerX = row.Rect.X + (row.Rect.Width * 0.5f);
                float centerY = row.Rect.Y + (row.Rect.Height * 0.5f);
                float dx = centerX - mouseX;
                float dy = centerY - mouseY;
                return (dx * dx) + (dy * dy);
            })
            .First();
        return mouseY <= closestRow.Rect.Y + (closestRow.Rect.Height * 0.5f)
            ? closestRow.Index
            : closestRow.Index + 1;
    }

    private void CompleteLayerReorderDrag()
    {
        int sourceIndex = _layerDragSourceIndex;
        int insertIndex = _layerDragInsertIndex;
        bool moved = _layerDragMoved;
        string? joinGroupId = _layerDragJoinGroupId;
        int joinAnchorLayerIndex = _layerDragJoinAnchorLayerIndex;
        _layerDragSourceIndex = -1;
        _layerDragInsertIndex = -1;
        _layerDragMoved = false;
        _layerDragJoinGroupId = null;
        _layerDragJoinAnchorLayerIndex = -1;

        if (!moved || sourceIndex < 0 || sourceIndex >= CurrentPixelFrame.Layers.Count)
        {
            RefreshPixelStudioInteraction();
            return;
        }

        if (!string.IsNullOrWhiteSpace(joinGroupId) && joinAnchorLayerIndex >= 0 && joinAnchorLayerIndex < CurrentPixelFrame.Layers.Count)
        {
            JoinDraggedLayerToGroup(sourceIndex, joinGroupId, joinAnchorLayerIndex);
            return;
        }

        insertIndex = Math.Clamp(insertIndex, 0, CurrentPixelFrame.Layers.Count);
        int targetIndex = insertIndex > sourceIndex
            ? insertIndex - 1
            : insertIndex;
        targetIndex = Math.Clamp(targetIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        if (targetIndex == sourceIndex)
        {
            RefreshPixelStudioInteraction();
            return;
        }

        MovePixelLayerToIndex(sourceIndex, targetIndex, "Reordered layer.");
    }

    private void JoinDraggedLayerToGroup(int sourceIndex, string targetGroupId, int anchorLayerIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= CurrentPixelFrame.Layers.Count)
        {
            RefreshPixelStudioInteraction();
            return;
        }

        anchorLayerIndex = Math.Clamp(anchorLayerIndex, 0, CurrentPixelFrame.Layers.Count - 1);
        string targetGroupName = GetLayerGroupDisplayName(targetGroupId) ?? "Group";
        int segmentEnd = anchorLayerIndex;
        while (segmentEnd + 1 < CurrentPixelFrame.Layers.Count
            && string.Equals(CurrentPixelFrame.Layers[segmentEnd + 1].GroupId, targetGroupId, StringComparison.Ordinal))
        {
            segmentEnd++;
        }

        int insertIndex = segmentEnd + 1;
        int targetIndex = sourceIndex < insertIndex
            ? insertIndex - 1
            : insertIndex;
        targetIndex = Math.Clamp(targetIndex, 0, CurrentPixelFrame.Layers.Count - 1);

        ApplyPixelStudioChange($"Moved layer into {targetGroupName}.", () =>
        {
            foreach (PixelStudioFrameState frame in _pixelStudio.Frames)
            {
                PixelStudioLayerState layer = frame.Layers[sourceIndex];
                frame.Layers.RemoveAt(sourceIndex);
                int frameTargetIndex = sourceIndex < insertIndex
                    ? insertIndex - 1
                    : insertIndex;
                frameTargetIndex = Math.Clamp(frameTargetIndex, 0, frame.Layers.Count);
                frame.Layers.Insert(frameTargetIndex, layer);
                frame.Layers[frameTargetIndex].GroupId = targetGroupId;
                frame.Layers[frameTargetIndex].GroupName = targetGroupName;
            }

            _pixelStudio.ActiveLayerIndex = targetIndex;
            return true;
        });
    }

    private void CompleteFrameReorderDrag()
    {
        int sourceIndex = _frameDragSourceIndex;
        int insertIndex = _frameDragInsertIndex;
        bool moved = _frameDragMoved;
        _frameDragSourceIndex = -1;
        _frameDragInsertIndex = -1;
        _frameDragMoved = false;

        if (!moved || sourceIndex < 0 || sourceIndex >= _pixelStudio.Frames.Count)
        {
            RefreshPixelStudioInteraction();
            return;
        }

        insertIndex = Math.Clamp(insertIndex, 0, _pixelStudio.Frames.Count);
        int targetIndex = insertIndex > sourceIndex
            ? insertIndex - 1
            : insertIndex;
        targetIndex = Math.Clamp(targetIndex, 0, _pixelStudio.Frames.Count - 1);
        if (targetIndex == sourceIndex)
        {
            RefreshPixelStudioInteraction();
            return;
        }

        MovePixelFrameToIndex(sourceIndex, targetIndex, "Reordered frame.");
    }

    private static bool TryGetNavigatorResizeCorner(UiRect panelRect, float mouseX, float mouseY, out NavigatorResizeCorner corner)
    {
        const float handleSize = 14f;
        UiRect topLeft = new(panelRect.X, panelRect.Y, handleSize, handleSize);
        UiRect topRight = new(panelRect.X + Math.Max(panelRect.Width - handleSize, 0f), panelRect.Y, handleSize, handleSize);
        UiRect bottomLeft = new(panelRect.X, panelRect.Y + Math.Max(panelRect.Height - handleSize, 0f), handleSize, handleSize);
        UiRect bottomRight = new(panelRect.X + Math.Max(panelRect.Width - handleSize, 0f), panelRect.Y + Math.Max(panelRect.Height - handleSize, 0f), handleSize, handleSize);

        if (topLeft.Contains(mouseX, mouseY))
        {
            corner = NavigatorResizeCorner.TopLeft;
            return true;
        }

        if (topRight.Contains(mouseX, mouseY))
        {
            corner = NavigatorResizeCorner.TopRight;
            return true;
        }

        if (bottomLeft.Contains(mouseX, mouseY))
        {
            corner = NavigatorResizeCorner.BottomLeft;
            return true;
        }

        if (bottomRight.Contains(mouseX, mouseY))
        {
            corner = NavigatorResizeCorner.BottomRight;
            return true;
        }

        corner = NavigatorResizeCorner.None;
        return false;
    }

    private void ResizeNavigatorPanel(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (layout.NavigatorPanelRect is null)
        {
            return;
        }

        UiRect canvasBounds = layout.CanvasClipRect;
        GetNavigatorPanelSizeLimits(canvasBounds, out float minWidth, out float minHeight, out float maxWidth, out float maxHeight);
        float deltaX = mouseX - _navigatorResizeStartMouseX;
        float deltaY = mouseY - _navigatorResizeStartMouseY;

        ResolveResizeAxis(
            _navigatorResizeCorner is NavigatorResizeCorner.TopLeft or NavigatorResizeCorner.BottomLeft,
            deltaX,
            _navigatorResizeStartOffsetX,
            _navigatorResizeStartWidth,
            minWidth,
            canvasBounds.Width,
            maxWidth,
            out float offsetX,
            out float width);
        ResolveResizeAxis(
            _navigatorResizeCorner is NavigatorResizeCorner.TopLeft or NavigatorResizeCorner.TopRight,
            deltaY,
            _navigatorResizeStartOffsetY,
            _navigatorResizeStartHeight,
            minHeight,
            canvasBounds.Height,
            maxHeight,
            out float offsetY,
            out float height);

        bool xChanged = !float.IsFinite(_pixelNavigatorPanelOffsetX) || MathF.Abs(_pixelNavigatorPanelOffsetX - offsetX) > 0.1f;
        bool yChanged = !float.IsFinite(_pixelNavigatorPanelOffsetY) || MathF.Abs(_pixelNavigatorPanelOffsetY - offsetY) > 0.1f;
        bool widthChanged = !float.IsFinite(_pixelNavigatorPanelWidth) || MathF.Abs(_pixelNavigatorPanelWidth - width) > 0.1f;
        bool heightChanged = !float.IsFinite(_pixelNavigatorPanelHeight) || MathF.Abs(_pixelNavigatorPanelHeight - height) > 0.1f;
        if (!xChanged && !yChanged && !widthChanged && !heightChanged)
        {
            return;
        }

        _pixelNavigatorPanelOffsetX = offsetX;
        _pixelNavigatorPanelOffsetY = offsetY;
        _pixelNavigatorPanelWidth = width;
        _pixelNavigatorPanelHeight = height;

        UiRect updatedPanelRect = new(
            canvasBounds.X + offsetX,
            canvasBounds.Y + offsetY,
            width,
            height);
        UiRect updatedPreviewRect = GetNavigatorPreviewPanelRect(updatedPanelRect);
        ReplacePixelStudioLayoutSnapshot(layout.WithNavigatorPanel(updatedPanelRect, updatedPreviewRect));
        RefreshPixelStudioInteraction();
    }

    private static void ResolveResizeAxis(
        bool resizeLeadingEdge,
        float delta,
        float startOffset,
        float startSize,
        float minSize,
        float canvasExtent,
        float maxPanelSize,
        out float offset,
        out float size)
    {
        float maxShrink = Math.Max(startSize - minSize, 0f);
        if (resizeLeadingEdge)
        {
            float maxGrow = Math.Min(startOffset, Math.Max(maxPanelSize - startSize, 0f));
            float clampedDelta = Math.Clamp(delta, -maxGrow, maxShrink);
            offset = startOffset + clampedDelta;
            size = startSize - clampedDelta;
        }
        else
        {
            float availableTrailingSpace = Math.Max(canvasExtent - (startOffset + startSize), 0f);
            float maxGrow = Math.Min(availableTrailingSpace, Math.Max(maxPanelSize - startSize, 0f));
            float clampedDelta = Math.Clamp(delta, -maxShrink, maxGrow);
            offset = startOffset;
            size = startSize + clampedDelta;
        }
    }

    private static void GetNavigatorPanelSizeLimits(UiRect canvasBounds, out float minWidth, out float minHeight, out float maxWidth, out float maxHeight)
    {
        minWidth = Math.Clamp(MathF.Min(canvasBounds.Width * 0.24f, 196f), 144f, 196f);
        minHeight = Math.Clamp(MathF.Min(canvasBounds.Height * 0.30f, 196f), 144f, 196f);
        minWidth = Math.Min(minWidth, Math.Max(canvasBounds.Width, 0f));
        minHeight = Math.Min(minHeight, Math.Max(canvasBounds.Height, 0f));
        maxWidth = Math.Min(Math.Max(minWidth, MathF.Min(canvasBounds.Width * 0.58f, 420f)), Math.Max(canvasBounds.Width, minWidth));
        maxHeight = Math.Min(Math.Max(minHeight, MathF.Min(canvasBounds.Height * 0.58f, 420f)), Math.Max(canvasBounds.Height, minHeight));
    }

    private static UiRect GetNavigatorPreviewPanelRect(UiRect panelRect)
    {
        const float previewInset = 16f;
        return new UiRect(
            panelRect.X + previewInset,
            panelRect.Y + previewInset,
            Math.Max(panelRect.Width - (previewInset * 2f), 0f),
            Math.Max(panelRect.Height - (previewInset * 2f), 0f));
    }

    private void MoveNavigatorPanel(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (layout.NavigatorPanelRect is null || layout.NavigatorPreviewRect is null)
        {
            return;
        }

        UiRect canvasBounds = layout.CanvasClipRect;
        float requestedX = _navigatorDragStartOffsetX + (mouseX - _navigatorDragStartMouseX);
        float requestedY = _navigatorDragStartOffsetY + (mouseY - _navigatorDragStartMouseY);
        const float snapThreshold = 18f;
        float maxOffsetX = Math.Max(canvasBounds.Width - layout.NavigatorPanelRect.Value.Width, 0f);
        float maxOffsetY = Math.Max(canvasBounds.Height - layout.NavigatorPanelRect.Value.Height, 0f);
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

        bool xChanged = !float.IsFinite(_pixelNavigatorPanelOffsetX) || MathF.Abs(_pixelNavigatorPanelOffsetX - clampedX) > 0.1f;
        bool yChanged = !float.IsFinite(_pixelNavigatorPanelOffsetY) || MathF.Abs(_pixelNavigatorPanelOffsetY - clampedY) > 0.1f;
        if (!xChanged && !yChanged)
        {
            return;
        }

        _pixelNavigatorPanelOffsetX = clampedX;
        _pixelNavigatorPanelOffsetY = clampedY;
        _pixelNavigatorPanelWidth = layout.NavigatorPanelRect.Value.Width;
        _pixelNavigatorPanelHeight = layout.NavigatorPanelRect.Value.Height;
        UiRect updatedPanelRect = new(
            canvasBounds.X + clampedX,
            canvasBounds.Y + clampedY,
            layout.NavigatorPanelRect.Value.Width,
            layout.NavigatorPanelRect.Value.Height);
        UiRect updatedPreviewRect = GetNavigatorPreviewPanelRect(updatedPanelRect);
        ReplacePixelStudioLayoutSnapshot(layout.WithNavigatorPanel(updatedPanelRect, updatedPreviewRect));
        RefreshPixelStudioInteraction();
    }

    private void UpdateNavigatorCamera(PixelStudioLayoutSnapshot layout, float mouseX, float mouseY)
    {
        if (layout.NavigatorPreviewRect is null)
        {
            return;
        }

        if (!TryGetNavigatorCanvasPoint(layout.NavigatorPreviewRect.Value, mouseX, mouseY, out float canvasX, out float canvasY))
        {
            return;
        }

        ApplyNavigatorCameraTarget(layout.CanvasClipRect, canvasX + 0.5f, canvasY + 0.5f);
        RefreshPixelStudioPanLayout();
    }

    private bool TryGetNavigatorCanvasPoint(UiRect previewRect, float mouseX, float mouseY, out float canvasX, out float canvasY)
    {
        canvasX = 0f;
        canvasY = 0f;
        if (_pixelStudio.CanvasWidth <= 0 || _pixelStudio.CanvasHeight <= 0)
        {
            return false;
        }

        UiRect imageRect = GetNavigatorPreviewImageRect(previewRect);
        if (imageRect.Width <= 0f || imageRect.Height <= 0f)
        {
            return false;
        }

        float localX = Math.Clamp(mouseX, imageRect.X, imageRect.X + imageRect.Width) - imageRect.X;
        float localY = Math.Clamp(mouseY, imageRect.Y, imageRect.Y + imageRect.Height) - imageRect.Y;
        canvasX = Math.Clamp((localX / imageRect.Width) * _pixelStudio.CanvasWidth, 0f, Math.Max(_pixelStudio.CanvasWidth - 0.001f, 0f));
        canvasY = Math.Clamp((localY / imageRect.Height) * _pixelStudio.CanvasHeight, 0f, Math.Max(_pixelStudio.CanvasHeight - 0.001f, 0f));
        return float.IsFinite(canvasX) && float.IsFinite(canvasY);
    }

    private UiRect GetNavigatorPreviewImageRect(UiRect previewRect)
    {
        UiRect paddedRect = new(previewRect.X + 6, previewRect.Y + 6, Math.Max(previewRect.Width - 12, 0), Math.Max(previewRect.Height - 12, 0));
        float scale = MathF.Min(
            Math.Max(paddedRect.Width, 1f) / Math.Max(_pixelStudio.CanvasWidth, 1),
            Math.Max(paddedRect.Height, 1f) / Math.Max(_pixelStudio.CanvasHeight, 1));
        float cellSize = Math.Max(scale, 0.25f);
        float viewportWidth = cellSize * _pixelStudio.CanvasWidth;
        float viewportHeight = cellSize * _pixelStudio.CanvasHeight;
        return new UiRect(
            paddedRect.X + Math.Max((paddedRect.Width - viewportWidth) * 0.5f, 0f),
            paddedRect.Y + Math.Max((paddedRect.Height - viewportHeight) * 0.5f, 0f),
            viewportWidth,
            viewportHeight);
    }

    private void ApplyNavigatorCameraTarget(UiRect clipRect, float canvasX, float canvasY)
    {
        int zoom = PixelStudioCameraMath.ClampZoom(_pixelStudio.DesiredZoom);
        float baseViewportX = clipRect.X + ((clipRect.Width - (_pixelStudio.CanvasWidth * zoom)) * 0.5f);
        float baseViewportY = clipRect.Y + ((clipRect.Height - (_pixelStudio.CanvasHeight * zoom)) * 0.5f);
        float clipCenterX = clipRect.X + (clipRect.Width * 0.5f);
        float clipCenterY = clipRect.Y + (clipRect.Height * 0.5f);
        _pixelStudio.CanvasPanX = clipCenterX - (canvasX * zoom) - baseViewportX;
        _pixelStudio.CanvasPanY = clipCenterY - (canvasY * zoom) - baseViewportY;
    }

    private static byte ToColorByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }
}
