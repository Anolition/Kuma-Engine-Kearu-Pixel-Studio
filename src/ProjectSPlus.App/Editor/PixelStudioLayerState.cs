namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioLayerState
{
    public required string Name { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsLocked { get; set; }

    public bool IsAlphaLocked { get; set; }

    public bool IsSharedAcrossFrames { get; set; }

    public float Opacity { get; set; } = 1f;

    public required int[] Pixels { get; set; }
}
