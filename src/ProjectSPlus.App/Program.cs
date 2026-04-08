using ProjectSPlus.Core.Configuration;
using ProjectSPlus.Editor.Shell;
using ProjectSPlus.App.Editor;
using ProjectSPlus.Runtime.Application;

string settingsPath = Path.Combine(AppContext.BaseDirectory, "settings", "appsettings.json");
string logPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");

try
{
    JsonSettingsStore settingsStore = new();
    AppSettings settings = settingsStore.LoadOrCreate(settingsPath);
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
        settings.Editor.PromptForPaletteGenerationAfterImport,
        settings.Editor.PixelColorPickerMode,
        settings.Editor.CustomThemes);

    IApplicationHost host = new WindowHost();

    host.Run(settings, scene, updatedSettings => settingsStore.Save(settingsPath, updatedSettings));
}
catch (Exception ex)
{
    File.WriteAllText(logPath, ex.ToString());
    throw;
}
