using ProjectSPlus.Core.Configuration;

namespace ProjectSPlus.Editor.Themes;

public sealed class EditorTypography
{
    public required string PreferredFontFamily { get; init; }

    public required FontSizePreset FontSizePreset { get; init; }

    public required ThemeTextStyle MenuText { get; init; }

    public required ThemeTextStyle PanelTitleText { get; init; }

    public required ThemeTextStyle BodyText { get; init; }

    public required ThemeTextStyle StatusText { get; init; }
}
