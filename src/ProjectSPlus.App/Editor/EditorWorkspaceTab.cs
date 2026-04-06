namespace ProjectSPlus.App.Editor;

public sealed class EditorWorkspaceTab
{
    public required string Id { get; init; }

    public required string Title { get; set; }

    public required EditorPageKind Page { get; set; }
}
