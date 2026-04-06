namespace ProjectSPlus.Core.Configuration;

public sealed class AppSettings
{
    public WindowSettings Window { get; init; } = new();

    public EditorSettings Editor { get; init; } = new();
}
