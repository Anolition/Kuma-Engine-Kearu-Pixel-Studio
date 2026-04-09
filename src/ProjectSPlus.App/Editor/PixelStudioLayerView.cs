namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioLayerView
{
    public required string Name { get; init; }

    public required bool IsVisible { get; init; }

    public required bool IsLocked { get; init; }

    public required bool IsAlphaLocked { get; init; }

    public required bool IsSharedAcrossFrames { get; init; }

    public required float Opacity { get; init; }

    public required bool IsActive { get; init; }
}
