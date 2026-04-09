namespace ProjectSPlus.App.Editor;

public static class PixelStudioDocumentFilePicker
{
    private const string FileFilter = "Kearu Studio Sprite (*.kearu)|*.kearu|JSON Sprite (*.json)|*.json";
    private const string PngFilter = "PNG Image (*.png)|*.png";
    private const string GifFilter = "GIF Animation (*.gif)|*.gif";
    private const string PaletteFilter = "Kearu Palette (*.kpal)|*.kpal|JSON Palette (*.json)|*.json";
    public static string? ShowOpenDialog(string initialDirectory)
    {
        return OperatingSystem.IsWindows()
            ? WindowsNativeFileDialog.ShowOpenFileDialog(initialDirectory, FileFilter)
            : null;
    }

    public static string? ShowSaveDialog(string initialDirectory, string suggestedFileName)
    {
        return OperatingSystem.IsWindows()
            ? WindowsNativeFileDialog.ShowSaveFileDialog(initialDirectory, suggestedFileName, FileFilter, "kearu")
            : null;
    }

    public static string? ShowPngSaveDialog(string initialDirectory, string suggestedFileName)
    {
        return OperatingSystem.IsWindows()
            ? WindowsNativeFileDialog.ShowSaveFileDialog(initialDirectory, suggestedFileName, PngFilter, "png")
            : null;
    }

    public static string? ShowPaletteOpenDialog(string initialDirectory)
    {
        return OperatingSystem.IsWindows()
            ? WindowsNativeFileDialog.ShowOpenFileDialog(initialDirectory, PaletteFilter)
            : null;
    }

    public static string? ShowPaletteSaveDialog(string initialDirectory, string suggestedFileName)
    {
        return OperatingSystem.IsWindows()
            ? WindowsNativeFileDialog.ShowSaveFileDialog(initialDirectory, suggestedFileName, PaletteFilter, "kpal")
            : null;
    }

    public static string? ShowGifSaveDialog(string initialDirectory, string suggestedFileName)
    {
        return OperatingSystem.IsWindows()
            ? WindowsNativeFileDialog.ShowSaveFileDialog(initialDirectory, suggestedFileName, GifFilter, "gif")
            : null;
    }
}
