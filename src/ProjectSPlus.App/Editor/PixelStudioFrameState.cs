namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioFrameState
{
    public required string Name { get; set; }

    public required List<PixelStudioLayerState> Layers { get; init; }
}
