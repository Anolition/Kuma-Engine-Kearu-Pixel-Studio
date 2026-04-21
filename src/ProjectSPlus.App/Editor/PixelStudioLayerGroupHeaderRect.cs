namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioLayerGroupHeaderRect
{
    public required string GroupId { get; init; }

    public required string GroupName { get; init; }

    public required int FirstLayerIndex { get; init; }

    public required int MemberCount { get; init; }

    public required bool IsCollapsed { get; init; }

    public required UiRect Rect { get; init; }

    public required UiRect ToggleRect { get; init; }
}
