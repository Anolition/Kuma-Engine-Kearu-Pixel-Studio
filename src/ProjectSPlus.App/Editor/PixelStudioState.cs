using ProjectSPlus.Editor.Themes;

namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioState
{
    public string DocumentName { get; set; } = "Blank Sprite";

    public int CanvasWidth { get; set; } = 32;

    public int CanvasHeight { get; set; } = 32;

    public int DesiredZoom { get; set; } = 24;

    public int BrushSize { get; set; } = 1;

    public float CanvasPanX { get; set; }

    public float CanvasPanY { get; set; }

    public bool ShowGrid { get; set; } = true;

    public int FramesPerSecond { get; set; } = 8;

    public bool ShowOnionSkin { get; set; } = true;

    public bool ShowPreviousOnion { get; set; } = true;

    public bool ShowNextOnion { get; set; }

    public float OnionOpacity { get; set; } = 0.42f;

    public bool IsPlaying { get; set; }

    public int PreviewFrameIndex { get; set; }

    public PixelStudioToolKind ActiveTool { get; set; } = PixelStudioToolKind.Pencil;

    public PixelStudioSelectionMode SelectionMode { get; set; } = PixelStudioSelectionMode.Box;

    public PixelStudioShapeRenderMode RectangleRenderMode { get; set; } = PixelStudioShapeRenderMode.Outline;

    public PixelStudioShapeRenderMode EllipseRenderMode { get; set; } = PixelStudioShapeRenderMode.Outline;

    public PixelStudioShapePreset ShapePreset { get; set; } = PixelStudioShapePreset.Star;

    public PixelStudioShapeRenderMode ShapeRenderMode { get; set; } = PixelStudioShapeRenderMode.Outline;

    public bool HasSelection { get; set; }

    public bool SelectionCommitted { get; set; }

    public int SelectionStartX { get; set; }

    public int SelectionStartY { get; set; }

    public int SelectionEndX { get; set; }

    public int SelectionEndY { get; set; }

    public List<int> SelectionMaskIndices { get; set; } = [];

    public int ActivePaletteIndex { get; set; } = 0;

    public int ActiveFrameIndex { get; set; }

    public int ActiveLayerIndex { get; set; }

    public required List<ThemeColor> Palette { get; init; }

    public required List<PixelStudioFrameState> Frames { get; init; }
}
