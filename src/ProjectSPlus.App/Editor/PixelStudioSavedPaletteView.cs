using ProjectSPlus.Editor.Themes;

namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioSavedPaletteView
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required bool IsActive { get; init; }

    public required bool IsSelected { get; init; }

    public IReadOnlyList<ThemeColor> PreviewColors { get; init; } = [];
}
