using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectSPlus.Core.Configuration;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public AppSettings LoadOrCreate(string settingsPath)
    {
        if (!File.Exists(settingsPath))
        {
            AppSettings defaults = AppSettingsDefaults.Create();
            Save(settingsPath, defaults);
            return defaults;
        }

        string json = File.ReadAllText(settingsPath);

        try
        {
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? AppSettingsDefaults.Create();
            AppSettings normalized = Normalize(settings);
            if (!ReferenceEquals(settings, normalized))
            {
                Save(settingsPath, normalized);
            }

            return normalized;
        }
        catch (JsonException)
        {
            AppSettings defaults = AppSettingsDefaults.Create();
            Save(settingsPath, defaults);
            return defaults;
        }
    }

    public void Save(string settingsPath, AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(settingsPath, json);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        WindowSettings normalizedWindow = settings.Window.Normalize();
        EditorLayoutSettings normalizedLayout = settings.Editor.Layout.Normalize();
        int normalizedAutosaveIntervalSeconds = EditorAutosaveOptions.Normalize(settings.Editor.PixelAutosaveIntervalSeconds);
        bool windowChanged =
            normalizedWindow.Width != settings.Window.Width
            || normalizedWindow.Height != settings.Window.Height
            || !string.Equals(normalizedWindow.Title, settings.Window.Title, StringComparison.Ordinal);
        bool layoutChanged =
            normalizedLayout.LeftPanelWidth != settings.Editor.Layout.LeftPanelWidth
            || normalizedLayout.RightPanelWidth != settings.Editor.Layout.RightPanelWidth
            || normalizedLayout.LeftPanelCollapsed != settings.Editor.Layout.LeftPanelCollapsed
            || normalizedLayout.RightPanelCollapsed != settings.Editor.Layout.RightPanelCollapsed
            || normalizedLayout.PixelToolsPanelWidth != settings.Editor.Layout.PixelToolsPanelWidth
            || normalizedLayout.PixelSidebarWidth != settings.Editor.Layout.PixelSidebarWidth
            || normalizedLayout.PixelToolsPanelCollapsed != settings.Editor.Layout.PixelToolsPanelCollapsed
            || normalizedLayout.PixelSidebarCollapsed != settings.Editor.Layout.PixelSidebarCollapsed
            || normalizedLayout.PixelToolSettingsDockSide != settings.Editor.Layout.PixelToolSettingsDockSide
            || normalizedLayout.PixelTimelineVisible != settings.Editor.Layout.PixelTimelineVisible
            || normalizedLayout.PixelToolSettingsOffsetX != settings.Editor.Layout.PixelToolSettingsOffsetX
            || normalizedLayout.PixelToolSettingsOffsetY != settings.Editor.Layout.PixelToolSettingsOffsetY
            || normalizedLayout.PixelNavigatorVisible != settings.Editor.Layout.PixelNavigatorVisible
            || normalizedLayout.PixelNavigatorOffsetX != settings.Editor.Layout.PixelNavigatorOffsetX
            || normalizedLayout.PixelNavigatorOffsetY != settings.Editor.Layout.PixelNavigatorOffsetY
            || normalizedLayout.PixelNavigatorWidth != settings.Editor.Layout.PixelNavigatorWidth
            || normalizedLayout.PixelNavigatorHeight != settings.Editor.Layout.PixelNavigatorHeight
            || normalizedLayout.PixelAnimationPreviewVisible != settings.Editor.Layout.PixelAnimationPreviewVisible
            || normalizedLayout.PixelAnimationPreviewOffsetX != settings.Editor.Layout.PixelAnimationPreviewOffsetX
            || normalizedLayout.PixelAnimationPreviewOffsetY != settings.Editor.Layout.PixelAnimationPreviewOffsetY
            || normalizedLayout.PixelAnimationPreviewWidth != settings.Editor.Layout.PixelAnimationPreviewWidth
            || normalizedLayout.PixelAnimationPreviewHeight != settings.Editor.Layout.PixelAnimationPreviewHeight;
        bool editorChanged = normalizedAutosaveIntervalSeconds != settings.Editor.PixelAutosaveIntervalSeconds;

        if (!windowChanged && !layoutChanged && !editorChanged)
        {
            return settings;
        }

        return new AppSettings
        {
            Window = normalizedWindow,
            Editor = new EditorSettings
            {
                ThemeName = settings.Editor.ThemeName,
                PreferredFontFamily = settings.Editor.PreferredFontFamily,
                FontSizePreset = settings.Editor.FontSizePreset,
                Shortcuts = settings.Editor.Shortcuts,
                ProjectLibraryPath = settings.Editor.ProjectLibraryPath,
                RecentProjects = settings.Editor.RecentProjects,
                LastProjectPath = settings.Editor.LastProjectPath,
                Layout = normalizedLayout,
                PixelPalettes = settings.Editor.PixelPalettes,
                ActivePixelPaletteId = settings.Editor.ActivePixelPaletteId,
                PromptForPaletteGenerationAfterImport = settings.Editor.PromptForPaletteGenerationAfterImport,
                PixelColorPickerMode = settings.Editor.PixelColorPickerMode,
                NotificationSoundMode = settings.Editor.NotificationSoundMode,
                PixelAutosaveIntervalSeconds = normalizedAutosaveIntervalSeconds,
                TransformRotationSnapDegrees = settings.Editor.TransformRotationSnapDegrees,
                CustomThemes = settings.Editor.CustomThemes
            }
        };
    }
}
