namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioLayerState
{
    public required string Name { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsLocked { get; set; }

    public required int[] Pixels { get; set; }
}
