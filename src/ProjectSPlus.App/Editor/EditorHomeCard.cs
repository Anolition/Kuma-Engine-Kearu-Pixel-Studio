namespace ProjectSPlus.App.Editor;

public sealed class EditorHomeCard
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public required EditorHomeAction Action { get; init; }
}
