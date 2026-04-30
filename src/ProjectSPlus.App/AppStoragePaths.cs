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
        string normalizedProjectLibraryPath = NormalizeProjectLibraryPath(settings.Editor.ProjectLibraryPath);
        IReadOnlyList<RecentProjectEntry> normalizedRecentProjects = NormalizeRecentProjects(settings.Editor.RecentProjects);
        string? normalizedLastProjectPath = NormalizeStoredProjectPath(settings.Editor.LastProjectPath);
        if (string.Equals(normalizedProjectLibraryPath, settings.Editor.ProjectLibraryPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(normalizedLastProjectPath, settings.Editor.LastProjectPath, StringComparison.OrdinalIgnoreCase)
            && RecentProjectsEqual(normalizedRecentProjects, settings.Editor.RecentProjects))
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
                RecentProjects = normalizedRecentProjects,
                LastProjectPath = normalizedLastProjectPath,
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
                BackgroundMusicEnabled = settings.Editor.BackgroundMusicEnabled,
                BackgroundMusicFileName = settings.Editor.BackgroundMusicFileName,
                BackgroundMusicVolumePercent = settings.Editor.BackgroundMusicVolumePercent,
                BackgroundMusicPlaybackMode = settings.Editor.BackgroundMusicPlaybackMode,
                PixelAutosaveIntervalSeconds = settings.Editor.PixelAutosaveIntervalSeconds,
                TransformRotationSnapDegrees = settings.Editor.TransformRotationSnapDegrees,
                CustomThemes = settings.Editor.CustomThemes
            }
        };
    }

    public static bool CanStoreUserPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && !IsOneDrivePath(path);
    }

    public static string NormalizeProjectLibraryPath(string? projectLibraryPath)
    {
        if (string.IsNullOrWhiteSpace(projectLibraryPath))
        {
            return DefaultProjectLibraryPath;
        }

        try
        {
            string normalizedPath = Path.GetFullPath(projectLibraryPath);
            if (IsLegacyManagedProjectLibraryPath(normalizedPath) || IsOneDrivePath(normalizedPath))
            {
                return DefaultProjectLibraryPath;
            }

            return normalizedPath;
        }
        catch
        {
            return DefaultProjectLibraryPath;
        }
    }

    public static bool IsOneDrivePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            string normalizedPath = NormalizeDirectoryPrefix(path);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] candidateRoots =
            [
                Environment.GetEnvironmentVariable("OneDrive") ?? string.Empty,
                Environment.GetEnvironmentVariable("OneDriveConsumer") ?? string.Empty,
                Environment.GetEnvironmentVariable("OneDriveCommercial") ?? string.Empty,
                string.IsNullOrWhiteSpace(userProfile) ? string.Empty : Path.Combine(userProfile, "OneDrive")
            ];

            foreach (string candidateRoot in candidateRoots)
            {
                if (string.IsNullOrWhiteSpace(candidateRoot))
                {
                    continue;
                }

                string normalizedRoot = NormalizeDirectoryPrefix(candidateRoot);
                if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
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

    private static IReadOnlyList<RecentProjectEntry> NormalizeRecentProjects(IReadOnlyList<RecentProjectEntry> recentProjects)
    {
        List<RecentProjectEntry> normalizedProjects = [];
        foreach (RecentProjectEntry recentProject in recentProjects)
        {
            string? normalizedPath = NormalizeStoredProjectPath(recentProject.Path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            normalizedProjects.Add(new RecentProjectEntry
            {
                Name = recentProject.Name,
                Path = normalizedPath
            });
        }

        return normalizedProjects;
    }

    private static string? NormalizeStoredProjectPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsOneDrivePath(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    private static bool RecentProjectsEqual(IReadOnlyList<RecentProjectEntry> left, IReadOnlyList<RecentProjectEntry> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].Name, right[index].Name, StringComparison.Ordinal)
                || !string.Equals(left[index].Path, right[index].Path, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeDirectoryPrefix(string path)
    {
        string normalized = Path.GetFullPath(path);
        normalized = Path.TrimEndingDirectorySeparator(normalized);
        return normalized + Path.DirectorySeparatorChar;
    }
}
