namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioFrameState
{
    public required string Name { get; set; }

    public int DurationMilliseconds { get; set; } = 125;

    public required List<PixelStudioLayerState> Layers { get; init; }
}
