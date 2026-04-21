namespace ProjectSPlus.App.Editor;

public sealed class PixelStudioFrameView
{
    public required string Name { get; init; }

    public required int DurationMilliseconds { get; init; }

    public required bool IsActive { get; init; }

    public required bool IsPreviewing { get; init; }

    public required bool IsSelected { get; init; }

    public bool IsInLoopRange { get; init; }

    public bool IsLoopStart { get; init; }

    public bool IsLoopEnd { get; init; }
}
