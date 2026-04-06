namespace ProjectSPlus.Editor.Themes;

public static class EditorThemeCatalog
{
    public const string DarkThemeName = "ProjectSPlus.Dark";
    public const string LightThemeName = "ProjectSPlus.Light";

    public static EditorTheme GetByName(string? themeName)
    {
        return string.Equals(themeName, LightThemeName, StringComparison.OrdinalIgnoreCase)
            ? CreateLightTheme()
            : CreateDarkTheme();
    }

    public static EditorTheme Toggle(EditorTheme theme)
    {
        return string.Equals(theme.Name, DarkThemeName, StringComparison.OrdinalIgnoreCase)
            ? CreateLightTheme()
            : CreateDarkTheme();
    }

    private static EditorTheme CreateDarkTheme()
    {
        return new EditorTheme
        {
            Name = DarkThemeName,
            Background = new ThemeColor(0.09f, 0.10f, 0.12f),
            MenuBar = new ThemeColor(0.12f, 0.14f, 0.18f),
            SidePanel = new ThemeColor(0.14f, 0.16f, 0.20f),
            Workspace = new ThemeColor(0.18f, 0.20f, 0.24f),
            TabStrip = new ThemeColor(0.15f, 0.17f, 0.21f),
            TabActive = new ThemeColor(0.27f, 0.34f, 0.45f),
            TabInactive = new ThemeColor(0.19f, 0.21f, 0.26f),
            StatusBar = new ThemeColor(0.11f, 0.12f, 0.15f),
            Divider = new ThemeColor(0.24f, 0.27f, 0.33f),
            Accent = new ThemeColor(0.33f, 0.53f, 0.81f)
        };
    }

    private static EditorTheme CreateLightTheme()
    {
        return new EditorTheme
        {
            Name = LightThemeName,
            Background = new ThemeColor(0.90f, 0.92f, 0.95f),
            MenuBar = new ThemeColor(0.82f, 0.85f, 0.90f),
            SidePanel = new ThemeColor(0.86f, 0.89f, 0.93f),
            Workspace = new ThemeColor(0.96f, 0.97f, 0.99f),
            TabStrip = new ThemeColor(0.84f, 0.87f, 0.91f),
            TabActive = new ThemeColor(0.38f, 0.57f, 0.86f),
            TabInactive = new ThemeColor(0.78f, 0.81f, 0.86f),
            StatusBar = new ThemeColor(0.80f, 0.83f, 0.88f),
            Divider = new ThemeColor(0.63f, 0.68f, 0.75f),
            Accent = new ThemeColor(0.19f, 0.36f, 0.70f)
        };
    }
}
