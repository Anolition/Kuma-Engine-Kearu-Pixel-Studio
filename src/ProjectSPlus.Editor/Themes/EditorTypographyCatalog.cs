using ProjectSPlus.Core.Configuration;

namespace ProjectSPlus.Editor.Themes;

public static class EditorTypographyCatalog
{
    public static readonly IReadOnlyList<string> ReadableFontCandidates =
    [
        "Segoe UI",
        "Arial",
        "Georgia",
        "Courier New",
        "Times New Roman",
        "Noto Sans",
        "DejaVu Sans",
        "Liberation Sans"
    ];

    public static EditorTypography Create(EditorTheme theme, string? preferredFontFamily, FontSizePreset fontSizePreset)
    {
        float baseSize = fontSizePreset switch
        {
            FontSizePreset.Small => 14.0f,
            FontSizePreset.Large => 20.0f,
            _ => 16.0f
        };

        return new EditorTypography
        {
            PreferredFontFamily = string.IsNullOrWhiteSpace(preferredFontFamily)
                ? ReadableFontCandidates[0]
                : preferredFontFamily,
            FontSizePreset = fontSizePreset,
            MenuText = new ThemeTextStyle(GetReadableTextColor(theme), baseSize),
            PanelTitleText = new ThemeTextStyle(GetReadableTextColor(theme), baseSize + 1.0f),
            BodyText = new ThemeTextStyle(GetReadableTextColor(theme), baseSize),
            StatusText = new ThemeTextStyle(GetReadableTextColor(theme), Math.Max(baseSize - 1.0f, 13.0f))
        };
    }

    public static FontSizePreset NextSize(FontSizePreset current)
    {
        return current switch
        {
            FontSizePreset.Small => FontSizePreset.Medium,
            FontSizePreset.Medium => FontSizePreset.Large,
            _ => FontSizePreset.Small
        };
    }

    public static string NextFontFamily(string currentFamily)
    {
        int index = ReadableFontCandidates
            .Select((font, idx) => new { font, idx })
            .FirstOrDefault(entry => string.Equals(entry.font, currentFamily, StringComparison.OrdinalIgnoreCase))
            ?.idx ?? -1;

        int nextIndex = (index + 1 + ReadableFontCandidates.Count) % ReadableFontCandidates.Count;
        return ReadableFontCandidates[nextIndex];
    }

    private static ThemeColor GetReadableTextColor(EditorTheme theme)
    {
        bool darkBackground = string.Equals(theme.Name, EditorThemeCatalog.DarkThemeName, StringComparison.OrdinalIgnoreCase);
        return darkBackground
            ? new ThemeColor(1.0f, 1.0f, 1.0f)
            : new ThemeColor(0.0f, 0.0f, 0.0f);
    }
}
