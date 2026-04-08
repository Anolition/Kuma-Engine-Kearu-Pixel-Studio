using ProjectSPlus.Editor.Themes;

namespace ProjectSPlus.App.Editor;

public sealed class EditorThemeRoleView
{
    public EditorThemeColorRole Role { get; set; } = EditorThemeColorRole.Accent;

    public string Label { get; set; } = string.Empty;

    public ThemeColor Color { get; set; } = new(1f, 1f, 1f, 1f);

    public bool IsSelected { get; set; }
}
