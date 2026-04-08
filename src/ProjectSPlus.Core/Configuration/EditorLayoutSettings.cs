namespace ProjectSPlus.Core.Configuration;

public sealed class EditorLayoutSettings
{
    public float LeftPanelWidth { get; init; } = 280f;

    public float RightPanelWidth { get; init; } = 320f;

    public bool LeftPanelCollapsed { get; init; }

    public bool RightPanelCollapsed { get; init; }

    public float PixelToolsPanelWidth { get; init; } = 164f;

    public float PixelSidebarWidth { get; init; } = 360f;

    public bool PixelToolsPanelCollapsed { get; init; }

    public bool PixelSidebarCollapsed { get; init; }

    public PixelStudioDockSide PixelToolSettingsDockSide { get; init; } = PixelStudioDockSide.Right;

    public bool PixelTimelineVisible { get; init; }

    public float? PixelToolSettingsOffsetX { get; init; }

    public float? PixelToolSettingsOffsetY { get; init; }

    public EditorLayoutSettings Normalize()
    {
        return new EditorLayoutSettings
        {
            LeftPanelWidth = Math.Clamp(LeftPanelWidth, 220f, 520f),
            RightPanelWidth = Math.Clamp(RightPanelWidth, 240f, 560f),
            LeftPanelCollapsed = LeftPanelCollapsed,
            RightPanelCollapsed = RightPanelCollapsed,
            PixelToolsPanelWidth = Math.Clamp(PixelToolsPanelWidth, 132f, 280f),
            PixelSidebarWidth = Math.Clamp(PixelSidebarWidth, 248f, 440f),
            PixelToolsPanelCollapsed = PixelToolsPanelCollapsed,
            PixelSidebarCollapsed = PixelSidebarCollapsed,
            PixelToolSettingsDockSide = PixelToolSettingsDockSide,
            PixelTimelineVisible = PixelTimelineVisible,
            PixelToolSettingsOffsetX = PixelToolSettingsOffsetX,
            PixelToolSettingsOffsetY = PixelToolSettingsOffsetY
        };
    }
}
