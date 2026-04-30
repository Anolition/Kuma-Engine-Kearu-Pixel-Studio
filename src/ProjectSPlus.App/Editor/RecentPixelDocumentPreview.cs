using ProjectSPlus.Editor.Themes;

namespace ProjectSPlus.App.Editor;

public sealed class RecentPixelDocumentPreview
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string DisplayPath { get; init; }

    public bool IsRecoveryBackup { get; init; }

    public required string DuplicateKey { get; init; }

    public required int CanvasWidth { get; init; }

    public required int CanvasHeight { get; init; }

    public required int Revision { get; init; }

    public required long LastWriteTimeUtcTicks { get; init; }

    public required IReadOnlyList<ThemeColor?> Pixels { get; init; }
}
