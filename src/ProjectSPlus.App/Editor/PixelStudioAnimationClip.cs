namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioAnimationClip
{
    public string Name { get; set; } = "Clip";

    public int StartFrameIndex { get; set; }

    public int EndFrameIndex { get; set; }

    public PixelStudioPlaybackLoopMode PlaybackLoopMode { get; set; } = PixelStudioPlaybackLoopMode.Forward;
}
