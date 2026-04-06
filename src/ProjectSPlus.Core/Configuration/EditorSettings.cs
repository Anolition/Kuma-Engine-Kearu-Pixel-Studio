namespace ProjectSPlus.Core.Configuration;

public sealed class EditorSettings
{
    public string ThemeName { get; init; } = "ProjectSPlus.Dark";

    public string PreferredFontFamily { get; init; } = "Segoe UI";

    public FontSizePreset FontSizePreset { get; init; } = FontSizePreset.Medium;

    public ShortcutBindings Shortcuts { get; init; } = new();

    public string ProjectLibraryPath { get; init; } = string.Empty;

    public IReadOnlyList<RecentProjectEntry> RecentProjects { get; init; } = [];

    public string? LastProjectPath { get; init; }

    public EditorLayoutSettings Layout { get; init; } = new();

    public IReadOnlyList<SavedPixelPalette> PixelPalettes { get; init; } = [];

    public string? ActivePixelPaletteId { get; init; }

    public bool PromptForPaletteGenerationAfterImport { get; init; } = true;
}
