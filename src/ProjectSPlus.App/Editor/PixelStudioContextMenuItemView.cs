namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioContextMenuItemView
{
    public required PixelStudioContextMenuAction Action { get; init; }

    public required string Label { get; init; }

    public bool IsDestructive { get; init; }
}
