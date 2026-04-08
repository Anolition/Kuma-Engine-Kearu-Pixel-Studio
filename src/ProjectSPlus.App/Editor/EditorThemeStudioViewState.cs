using ProjectSPlus.Editor.Themes;

namespace ProjectSPlus.App.Editor;

public sealed class EditorThemeStudioViewState
{
    public bool Visible { get; set; }

    public string ThemeName { get; set; } = string.Empty;

    public bool ThemeNameActive { get; set; }

    public bool ThemeNameSelected { get; set; }

    public bool CanDelete { get; set; }

    public string SaveLabel { get; set; } = "Save Theme";

    public EditorThemeColorRole SelectedRole { get; set; } = EditorThemeColorRole.Accent;

    public string SelectedRoleLabel { get; set; } = "Accent";

    public ThemeColor SelectedColor { get; set; } = new(1f, 1f, 1f, 1f);

    public IReadOnlyList<EditorThemeRoleView> Roles { get; set; } = [];
}
