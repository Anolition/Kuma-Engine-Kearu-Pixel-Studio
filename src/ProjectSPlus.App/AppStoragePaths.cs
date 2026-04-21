using ProjectSPlus.Core.Configuration;

namespace ProjectSPlus.App;

internal static class AppStoragePaths
{
    public static string AppDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kuma Engine");

    public static string SettingsDirectory => Path.Combine(AppDataRoot, "Settings");

    public static string SettingsFilePath => Path.Combine(SettingsDirectory, "appsettings.json");

    public static string LogsDirectory => Path.Combine(AppDataRoot, "Logs");

    public static string StartupLogPath => Path.Combine(LogsDirectory, "startup-error.log");

    public static string RecoveryDirectory => Path.Combine(AppDataRoot, "Recovery");

    public static string RecoveryFilePath => Path.Combine(RecoveryDirectory, "kearu-studio-recovery.json");

    public static string RecoveryBackupsDirectory => Path.Combine(RecoveryDirectory, "Backups");

    public static string DefaultProjectLibraryPath => Path.Combine(AppDataRoot, "Projects");

    public static string LegacySettingsFilePath => Path.Combine(AppContext.BaseDirectory, "settings", "appsettings.json");

    public static void EnsureWritableDirectories()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(RecoveryDirectory);
        Directory.CreateDirectory(RecoveryBackupsDirectory);
        Directory.CreateDirectory(DefaultProjectLibraryPath);
    }

    public static void TryMigrateLegacySettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath) || !File.Exists(LegacySettingsFilePath))
            {
                return;
            }

            Directory.CreateDirectory(SettingsDirectory);
            File.Copy(LegacySettingsFilePath, SettingsFilePath, overwrite: false);
        }
        catch
        {
        }
    }

    public static AppSettings NormalizeAppOwnedPaths(AppSettings settings)
    {
        string normalizedProjectLibraryPath = NormalizeManagedProjectLibraryPath(settings.Editor.ProjectLibraryPath);
        if (string.Equals(normalizedProjectLibraryPath, settings.Editor.ProjectLibraryPath, StringComparison.OrdinalIgnoreCase))
        {
            return settings;
        }

        return new AppSettings
        {
            Window = settings.Window,
            Editor = new EditorSettings
            {
                ThemeName = settings.Editor.ThemeName,
                PreferredFontFamily = settings.Editor.PreferredFontFamily,
                FontSizePreset = settings.Editor.FontSizePreset,
                Shortcuts = settings.Editor.Shortcuts,
                ProjectLibraryPath = normalizedProjectLibraryPath,
                RecentProjects = settings.Editor.RecentProjects,
                LastProjectPath = settings.Editor.LastProjectPath,
                Layout = settings.Editor.Layout,
                PixelPalettes = settings.Editor.PixelPalettes,
                ActivePixelPaletteId = settings.Editor.ActivePixelPaletteId,
                PixelWorkingPalette = settings.Editor.PixelWorkingPalette,
                PixelWorkingPaletteActiveIndex = settings.Editor.PixelWorkingPaletteActiveIndex,
                PixelRecentColors = settings.Editor.PixelRecentColors,
                PixelSecondaryColor = settings.Editor.PixelSecondaryColor,
                PromptForPaletteGenerationAfterImport = settings.Editor.PromptForPaletteGenerationAfterImport,
                PixelColorPickerMode = settings.Editor.PixelColorPickerMode,
                NotificationSoundMode = settings.Editor.NotificationSoundMode,
                PixelAutosaveIntervalSeconds = settings.Editor.PixelAutosaveIntervalSeconds,
                TransformRotationSnapDegrees = settings.Editor.TransformRotationSnapDegrees,
                CustomThemes = settings.Editor.CustomThemes
            }
        };
    }

    private static string NormalizeManagedProjectLibraryPath(string? projectLibraryPath)
    {
        if (string.IsNullOrWhiteSpace(projectLibraryPath) || IsLegacyManagedProjectLibraryPath(projectLibraryPath))
        {
            return DefaultProjectLibraryPath;
        }

        return projectLibraryPath;
    }

    private static bool IsLegacyManagedProjectLibraryPath(string path)
    {
        try
        {
            string normalizedPath = Path.GetFullPath(path);
            string documentsPath = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            string oneDriveDocumentsPath = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "OneDrive",
                "Documents"));
            string[] legacyNames =
            [
                "Project S+ Projects",
                "Kuma Engine Projects"
            ];

            foreach (string legacyName in legacyNames)
            {
                if (string.Equals(normalizedPath, Path.Combine(documentsPath, legacyName), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedPath, Path.Combine(oneDriveDocumentsPath, legacyName), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }
}
