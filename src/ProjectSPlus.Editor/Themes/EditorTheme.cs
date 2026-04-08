namespace ProjectSPlus.Editor.Themes;

public sealed class EditorTheme
{
    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public required ThemeColor Background { get; init; }

    public required ThemeColor MenuBar { get; init; }

    public required ThemeColor SidePanel { get; init; }

    public required ThemeColor Workspace { get; init; }

    public required ThemeColor TabStrip { get; init; }

    public required ThemeColor TabActive { get; init; }

    public required ThemeColor TabInactive { get; init; }

    public required ThemeColor StatusBar { get; init; }

    public required ThemeColor Divider { get; init; }

    public required ThemeColor Accent { get; init; }
}
