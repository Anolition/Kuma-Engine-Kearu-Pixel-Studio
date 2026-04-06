namespace ProjectSPlus.App.Editor;

public sealed class EditorMenuEntry
{
    public required string Label { get; init; }

    public required EditorMenuAction Action { get; init; }
}
