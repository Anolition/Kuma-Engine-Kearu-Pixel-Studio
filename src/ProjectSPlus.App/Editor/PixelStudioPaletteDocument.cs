using ProjectSPlus.Core.Configuration;

namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioPaletteDocument
{
    public string Name { get; init; } = "Palette";

    public IReadOnlyList<PaletteColorSetting> Colors { get; init; } = [];
}
