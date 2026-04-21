namespace ProjectSPlus.Core.Configuration;

public sealed class SavedPixelPalette
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; init; } = "Palette";

    public bool IsLocked { get; init; }

    public IReadOnlyList<PaletteColorSetting> Colors { get; init; } = [];
}
