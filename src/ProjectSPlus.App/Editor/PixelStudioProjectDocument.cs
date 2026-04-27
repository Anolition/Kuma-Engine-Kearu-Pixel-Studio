using ProjectSPlus.Core.Configuration;

namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioProjectDocument
{
    public string DocumentName { get; init; } = "Blank Sprite";

    public bool UsesDirectColorPixels { get; init; }

    public int CanvasWidth { get; init; } = 32;

    public int CanvasHeight { get; init; } = 32;

    public int DesiredZoom { get; init; } = 24;

    public int BrushSize { get; init; } = 1;

    public float CanvasPanX { get; init; }

    public float CanvasPanY { get; init; }

    public bool ShowGrid { get; init; } = true;

    public int FramesPerSecond { get; init; } = 8;

    public bool ShowOnionSkin { get; init; } = true;

    public bool ShowPreviousOnion { get; init; } = true;

    public bool ShowNextOnion { get; init; }

    public bool AllowDualOnion { get; init; }

    public float OnionOpacity { get; init; } = 0.42f;

    public bool IsPlaying { get; init; }

    public int PreviewFrameIndex { get; init; }

    public bool LoopRangeEnabled { get; init; }

    public int LoopStartFrameIndex { get; init; }

    public int LoopEndFrameIndex { get; init; }

    public PixelStudioPlaybackLoopMode PlaybackLoopMode { get; init; } = PixelStudioPlaybackLoopMode.Forward;

    public int ActiveAnimationClipIndex { get; init; } = -1;

    public PixelStudioToolKind ActiveTool { get; init; } = PixelStudioToolKind.Pencil;

    public PixelStudioShapeRenderMode RectangleRenderMode { get; init; } = PixelStudioShapeRenderMode.Outline;

    public PixelStudioShapeRenderMode EllipseRenderMode { get; init; } = PixelStudioShapeRenderMode.Outline;

    public PixelStudioShapePreset ShapePreset { get; init; } = PixelStudioShapePreset.Star;

    public PixelStudioShapeRenderMode ShapeRenderMode { get; init; } = PixelStudioShapeRenderMode.Outline;

    public int ActivePaletteIndex { get; init; }

    public int ActiveFrameIndex { get; init; }

    public int ActiveLayerIndex { get; init; }

    public IReadOnlyList<PaletteColorSetting> Palette { get; init; } = [];

    public IReadOnlyList<PixelStudioProjectAnimationClipDocument> AnimationClips { get; init; } = [];

    public IReadOnlyList<PixelStudioProjectFrameDocument> Frames { get; init; } = [];
}

public sealed class PixelStudioProjectAnimationClipDocument
{
    public string Name { get; init; } = "Clip";

    public int StartFrameIndex { get; init; }

    public int EndFrameIndex { get; init; }

    public PixelStudioPlaybackLoopMode PlaybackLoopMode { get; init; } = PixelStudioPlaybackLoopMode.Forward;
}

public sealed class PixelStudioProjectFrameDocument
{
    public string Name { get; init; } = "Frame";

    public int DurationMilliseconds { get; init; } = 125;

    public IReadOnlyList<PixelStudioProjectLayerDocument> Layers { get; init; } = [];
}

public sealed class PixelStudioProjectLayerDocument
{
    public string Name { get; init; } = "Layer";

    public string? GroupId { get; init; }

    public string? GroupName { get; init; }

    public bool IsVisible { get; init; } = true;

    public bool IsLocked { get; init; }

    public bool IsAlphaLocked { get; init; }

    public bool IsSharedAcrossFrames { get; init; }

    public bool IsIgnoredByOnionSkin { get; init; }

    public string? LinkedCelId { get; init; }

    public float Opacity { get; init; } = 1f;

    public IReadOnlyList<int> Pixels { get; init; } = [];
}
