namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioExportMetadataDocument
{
    public string DocumentName { get; init; } = "Sprite";

    public string ExportKind { get; init; } = "png";

    public int CanvasWidth { get; init; }

    public int CanvasHeight { get; init; }

    public int FrameCount { get; init; }

    public int FramesPerSecond { get; init; }

    public PixelStudioPlaybackLoopMode PlaybackLoopMode { get; init; } = PixelStudioPlaybackLoopMode.Forward;

    public PixelStudioExportLoopRangeDocument? LoopRange { get; init; }

    public IReadOnlyList<PixelStudioExportClipDocument> Clips { get; init; } = [];

    public IReadOnlyList<PixelStudioExportFrameDocument> Frames { get; init; } = [];
}

public sealed class PixelStudioExportLoopRangeDocument
{
    public int StartFrameIndex { get; init; }

    public int EndFrameIndex { get; init; }
}

public sealed class PixelStudioExportClipDocument
{
    public string Name { get; init; } = "Clip";

    public int StartFrameIndex { get; init; }

    public int EndFrameIndex { get; init; }

    public PixelStudioPlaybackLoopMode PlaybackLoopMode { get; init; } = PixelStudioPlaybackLoopMode.Forward;
}

public sealed class PixelStudioExportFrameDocument
{
    public int Index { get; init; }

    public string Name { get; init; } = "Frame";

    public int DurationMilliseconds { get; init; }

    public string? FileName { get; init; }
}
