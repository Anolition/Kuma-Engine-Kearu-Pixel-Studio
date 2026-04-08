namespace ProjectSPlus.Editor.Themes;

public static class EditorThemeCatalog
{
    public const string DarkThemeName = "ProjectSPlus.Dark";
    public const string LightThemeName = "ProjectSPlus.Light";
    public const string KumaThemeName = "ProjectSPlus.Kuma";
    public const string KearuThemeName = "ProjectSPlus.Kearu";

    public static EditorTheme GetByName(string? themeName)
    {
        if (string.Equals(themeName, DarkThemeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateDarkTheme();
        }

        if (string.Equals(themeName, LightThemeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateLightTheme();
        }

        if (string.Equals(themeName, KumaThemeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateKumaTheme();
        }

        if (string.Equals(themeName, KearuThemeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateKearuTheme();
        }

        return CreateKumaTheme();
    }

    public static EditorTheme Toggle(EditorTheme theme)
    {
        if (string.Equals(theme.Name, DarkThemeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateLightTheme();
        }

        if (string.Equals(theme.Name, LightThemeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateKumaTheme();
        }

        if (string.Equals(theme.Name, KumaThemeName, StringComparison.OrdinalIgnoreCase))
        {
            return CreateKearuTheme();
        }

        return CreateDarkTheme();
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

    private static EditorTheme CreateKumaTheme()
    {
        return new EditorTheme
        {
            Name = KumaThemeName,
            Background = new ThemeColor(0.10f, 0.08f, 0.06f),
            MenuBar = new ThemeColor(0.18f, 0.13f, 0.09f),
            SidePanel = new ThemeColor(0.23f, 0.17f, 0.12f),
            Workspace = new ThemeColor(0.29f, 0.22f, 0.16f),
            TabStrip = new ThemeColor(0.21f, 0.16f, 0.11f),
            TabActive = new ThemeColor(0.28f, 0.42f, 0.28f),
            TabInactive = new ThemeColor(0.34f, 0.25f, 0.18f),
            StatusBar = new ThemeColor(0.15f, 0.11f, 0.08f),
            Divider = new ThemeColor(0.45f, 0.34f, 0.24f),
            Accent = new ThemeColor(0.45f, 0.63f, 0.37f)
        };
    }

    private static EditorTheme CreateKearuTheme()
    {
        return new EditorTheme
        {
            Name = KearuThemeName,
            Background = new ThemeColor(0.05f, 0.10f, 0.08f),
            MenuBar = new ThemeColor(0.08f, 0.16f, 0.12f),
            SidePanel = new ThemeColor(0.10f, 0.20f, 0.15f),
            Workspace = new ThemeColor(0.12f, 0.26f, 0.19f),
            TabStrip = new ThemeColor(0.09f, 0.18f, 0.14f),
            TabActive = new ThemeColor(0.30f, 0.43f, 0.30f),
            TabInactive = new ThemeColor(0.18f, 0.30f, 0.22f),
            StatusBar = new ThemeColor(0.07f, 0.14f, 0.11f),
            Divider = new ThemeColor(0.52f, 0.41f, 0.31f),
            Accent = new ThemeColor(0.84f, 0.54f, 0.66f)
        };
    }
}
