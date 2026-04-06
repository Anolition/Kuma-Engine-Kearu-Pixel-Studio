namespace ProjectSPlus.Core.Configuration;

public sealed class WindowSettings
{
    public const int DefaultWidth = 1600;

    public const int DefaultHeight = 900;

    public const int MinimumWidth = 960;

    public const int MinimumHeight = 600;

    public string Title { get; init; } = "Project S+";

    public int Width { get; init; } = DefaultWidth;

    public int Height { get; init; } = DefaultHeight;

    public bool StartMaximized { get; init; }

    public WindowSettings Normalize()
    {
        return new WindowSettings
        {
            Title = string.IsNullOrWhiteSpace(Title) ? "Project S+" : Title,
            Width = NormalizeWidth(Width),
            Height = NormalizeHeight(Height),
            StartMaximized = StartMaximized
        };
    }

    public static int NormalizeWidth(int width)
    {
        return width < MinimumWidth ? DefaultWidth : width;
    }

    public static int NormalizeHeight(int height)
    {
        return height < MinimumHeight ? DefaultHeight : height;
    }
}
