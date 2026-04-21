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
        List<PaletteColorSetting> normalizedWorkingPalette = NormalizePaletteList(settings.Editor.PixelWorkingPalette, 24);
        int normalizedWorkingPaletteActiveIndex = normalizedWorkingPalette.Count == 0
            ? 0
            : Math.Clamp(settings.Editor.PixelWorkingPaletteActiveIndex, 0, normalizedWorkingPalette.Count - 1);
        List<PaletteColorSetting> normalizedRecentColors = NormalizePaletteList(settings.Editor.PixelRecentColors, 8);
        PaletteColorSetting? normalizedSecondaryColor = settings.Editor.PixelSecondaryColor is null
            ? null
            : ClonePaletteColor(settings.Editor.PixelSecondaryColor);
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
        bool editorChanged =
            normalizedAutosaveIntervalSeconds != settings.Editor.PixelAutosaveIntervalSeconds
            || normalizedWorkingPaletteActiveIndex != settings.Editor.PixelWorkingPaletteActiveIndex
            || !PaletteListsEqual(normalizedWorkingPalette, settings.Editor.PixelWorkingPalette)
            || !PaletteListsEqual(normalizedRecentColors, settings.Editor.PixelRecentColors)
            || !PaletteColorsEqual(normalizedSecondaryColor, settings.Editor.PixelSecondaryColor);

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
                PixelWorkingPalette = normalizedWorkingPalette,
                PixelWorkingPaletteActiveIndex = normalizedWorkingPaletteActiveIndex,
                PixelRecentColors = normalizedRecentColors,
                PixelSecondaryColor = normalizedSecondaryColor,
                PromptForPaletteGenerationAfterImport = settings.Editor.PromptForPaletteGenerationAfterImport,
                PixelColorPickerMode = settings.Editor.PixelColorPickerMode,
                NotificationSoundMode = settings.Editor.NotificationSoundMode,
                PixelAutosaveIntervalSeconds = normalizedAutosaveIntervalSeconds,
                TransformRotationSnapDegrees = settings.Editor.TransformRotationSnapDegrees,
                CustomThemes = settings.Editor.CustomThemes
            }
        };
    }

    private static List<PaletteColorSetting> NormalizePaletteList(IReadOnlyList<PaletteColorSetting>? colors, int maxCount)
    {
        if (colors is null || colors.Count == 0)
        {
            return [];
        }

        List<PaletteColorSetting> normalized = [];
        foreach (PaletteColorSetting? color in colors.Take(maxCount))
        {
            if (color is null)
            {
                continue;
            }

            normalized.Add(ClonePaletteColor(color));
        }

        return normalized;
    }

    private static PaletteColorSetting ClonePaletteColor(PaletteColorSetting color)
    {
        return new PaletteColorSetting
        {
            R = color.R,
            G = color.G,
            B = color.B,
            A = color.A
        };
    }

    private static bool PaletteListsEqual(IReadOnlyList<PaletteColorSetting>? left, IReadOnlyList<PaletteColorSetting>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!PaletteColorsEqual(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PaletteColorsEqual(PaletteColorSetting? left, PaletteColorSetting? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.R == right.R
            && left.G == right.G
            && left.B == right.B
            && left.A == right.A;
    }
}
