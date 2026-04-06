namespace ProjectSPlus.App.Editor;

public sealed class ActionRect<TAction>
{
    public required TAction Action { get; init; }

    public required UiRect Rect { get; init; }
}
