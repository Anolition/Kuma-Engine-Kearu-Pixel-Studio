namespace ProjectSPlus.Core.Configuration;

public sealed class SavedEditorTheme
{
    public string Id { get; init; } = $"ProjectSPlus.Custom.{Guid.NewGuid():N}";

    public string Name { get; init; } = "Custom Theme";

    public PaletteColorSetting Background { get; init; } = new();

    public PaletteColorSetting MenuBar { get; init; } = new();

    public PaletteColorSetting SidePanel { get; init; } = new();

    public PaletteColorSetting Workspace { get; init; } = new();

    public PaletteColorSetting TabStrip { get; init; } = new();

    public PaletteColorSetting TabActive { get; init; } = new();

    public PaletteColorSetting TabInactive { get; init; } = new();

    public PaletteColorSetting StatusBar { get; init; } = new();

    public PaletteColorSetting Divider { get; init; } = new();

    public PaletteColorSetting Accent { get; init; } = new();
}
