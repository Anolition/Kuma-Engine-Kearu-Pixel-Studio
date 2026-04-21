namespace ProjectSPlus.Core.Configuration;

public sealed class EditorSettings
{
    public string ThemeName { get; init; } = "ProjectSPlus.Kuma";

    public string PreferredFontFamily { get; init; } = "Segoe UI";

    public FontSizePreset FontSizePreset { get; init; } = FontSizePreset.Medium;

    public ShortcutBindings Shortcuts { get; init; } = new();

    public string ProjectLibraryPath { get; init; } = string.Empty;

    public IReadOnlyList<RecentProjectEntry> RecentProjects { get; init; } = [];

    public string? LastProjectPath { get; init; }

    public EditorLayoutSettings Layout { get; init; } = new();

    public IReadOnlyList<SavedPixelPalette> PixelPalettes { get; init; } = [];

    public string? ActivePixelPaletteId { get; init; }

    public IReadOnlyList<PaletteColorSetting> PixelWorkingPalette { get; init; } = [];

    public int PixelWorkingPaletteActiveIndex { get; init; }

    public IReadOnlyList<PaletteColorSetting> PixelRecentColors { get; init; } = [];

    public PaletteColorSetting? PixelSecondaryColor { get; init; }

    public bool PromptForPaletteGenerationAfterImport { get; init; } = true;

    public PixelStudioColorPickerMode PixelColorPickerMode { get; init; } = PixelStudioColorPickerMode.RgbField;

    public EditorNotificationSoundMode NotificationSoundMode { get; init; } = EditorNotificationSoundMode.Custom;

    public int PixelAutosaveIntervalSeconds { get; init; } = 10;

    public int TransformRotationSnapDegrees { get; init; } = 45;

    public IReadOnlyList<SavedEditorTheme> CustomThemes { get; init; } = [];
}
