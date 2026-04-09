namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioRecoverySnapshot
{
    public string? DocumentPath { get; init; }

    public string? ProjectPath { get; init; }

    public DateTimeOffset SavedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public required PixelStudioProjectDocument Document { get; init; }
}
