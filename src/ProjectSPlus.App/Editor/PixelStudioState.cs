using ProjectSPlus.Editor.Themes;

namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioState
{
    public string DocumentName { get; set; } = "Blank Sprite";

    public int CanvasWidth { get; set; } = 32;

    public int CanvasHeight { get; set; } = 32;

    public int DesiredZoom { get; set; } = 24;

    public float CanvasPanX { get; set; }

    public float CanvasPanY { get; set; }

    public bool ShowGrid { get; set; } = true;

    public int FramesPerSecond { get; set; } = 8;

    public bool IsPlaying { get; set; }

    public int PreviewFrameIndex { get; set; }

    public PixelStudioToolKind ActiveTool { get; set; } = PixelStudioToolKind.Pencil;

    public int ActivePaletteIndex { get; set; } = 0;

    public int ActiveFrameIndex { get; set; }

    public int ActiveLayerIndex { get; set; }

    public required List<ThemeColor> Palette { get; init; }

    public required List<PixelStudioFrameState> Frames { get; init; }
}
