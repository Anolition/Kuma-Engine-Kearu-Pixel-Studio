using ProjectSPlus.App;
using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Shell;
using ProjectSPlus.App.Editor;
using ProjectSPlus.Runtime.Application;

AppStoragePaths.EnsureWritableDirectories();
AppStoragePaths.TryMigrateLegacySettings();

string settingsPath = AppStoragePaths.SettingsFilePath;
string logPath = AppStoragePaths.StartupLogPath;

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    Exception exception = eventArgs.ExceptionObject as Exception
        ?? new InvalidOperationException($"Unhandled non-exception crash object: {eventArgs.ExceptionObject}");
    PixelStudioRecoveryCoordinator.TryFlushPendingRecovery();
    CrashReporter.Handle(exception, logPath);
};

TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
{
    PixelStudioRecoveryCoordinator.TryFlushPendingRecovery();
    CrashReporter.Handle(eventArgs.Exception, logPath);
    eventArgs.SetObserved();
};

try
{
    if (args.Contains("--test-crash-reporter", StringComparer.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Intentional crash reporter test triggered by launch argument.");
    }

    JsonSettingsStore settingsStore = new();
    AppSettings settings = settingsStore.LoadOrCreate(settingsPath);
    AppSettings normalizedAppOwnedPaths = AppStoragePaths.NormalizeAppOwnedPaths(settings);
    if (!ReferenceEquals(settings, normalizedAppOwnedPaths))
    {
        settingsStore.Save(settingsPath, normalizedAppOwnedPaths);
        settings = normalizedAppOwnedPaths;
    }
    NotificationSoundPlayer.SoundMode = settings.Editor.NotificationSoundMode;
    PixelStudioRecoverySnapshot? recoverySnapshot = null;
    bool preserveDeferredRecovery = false;
    if (PixelStudioRecoveryManager.TryLoad(out PixelStudioRecoverySnapshot? pendingRecovery) && pendingRecovery is not null)
    {
        switch (PixelStudioRecoveryPrompt.Show(pendingRecovery))
        {
            case PixelStudioRecoveryPromptResult.Restore:
                recoverySnapshot = pendingRecovery;
                break;
            case PixelStudioRecoveryPromptResult.Discard:
                PixelStudioRecoveryManager.Clear();
                break;
            case PixelStudioRecoveryPromptResult.Defer:
                preserveDeferredRecovery = true;
                break;
        }
    }

    EditorShell shell = EditorShell.CreateDefault();
    EditorWindowScene scene = new(
        shell,
        settings.Editor.ThemeName,
        settings.Editor.PreferredFontFamily,
        settings.Editor.FontSizePreset,
        settings.Editor.Shortcuts,
        settings.Editor.ProjectLibraryPath,
        settings.Editor.RecentProjects,
        settings.Editor.LastProjectPath,
        settings.Editor.Layout,
        settings.Editor.PixelPalettes,
        settings.Editor.ActivePixelPaletteId,
        settings.Editor.PixelWorkingPalette,
        settings.Editor.PixelWorkingPaletteActiveIndex,
        settings.Editor.PixelRecentColors,
        settings.Editor.PixelSecondaryColor,
        settings.Editor.PromptForPaletteGenerationAfterImport,
        settings.Editor.PixelColorPickerMode,
        settings.Editor.NotificationSoundMode,
        settings.Editor.BackgroundMusicEnabled,
        settings.Editor.BackgroundMusicFileName,
        settings.Editor.BackgroundMusicVolumePercent,
        settings.Editor.BackgroundMusicPlaybackMode,
        settings.Editor.PixelAutosaveIntervalSeconds,
        settings.Editor.TransformRotationSnapDegrees,
        settings.Editor.CustomThemes,
        recoverySnapshot,
        preserveDeferredRecovery);

    IApplicationHost host = new WindowHost();

    host.Run(settings, scene, updatedSettings => settingsStore.Save(settingsPath, updatedSettings));
}
catch (Exception ex)
{
    PixelStudioRecoveryCoordinator.TryFlushPendingRecovery();
    CrashReporter.Handle(ex, logPath);
    Environment.ExitCode = 1;
}
