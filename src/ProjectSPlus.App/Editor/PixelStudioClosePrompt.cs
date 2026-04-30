using System.Runtime.InteropServices;

namespace ProjectSPlus.App.Editor;

public enum PixelStudioClosePromptResult
{
    Save,
    Discard,
    Cancel
}

public static class PixelStudioClosePrompt
{
    private const uint MessageBoxYesNoCancel = 0x00000003;
    private const uint MessageBoxTopMost = 0x00040000;
    private const uint MessageBoxSetForeground = 0x00010000;
    private const int MessageBoxResultYes = 6;
    private const int MessageBoxResultNo = 7;

    public static PixelStudioClosePromptResult Show(string documentName, bool hasExistingFile)
    {
        if (!OperatingSystem.IsWindows())
        {
            return PixelStudioClosePromptResult.Save;
        }

        string resolvedDocumentName = string.IsNullOrWhiteSpace(documentName)
            ? "Untitled Kearu Artwork"
            : documentName;
        string targetLine = hasExistingFile
            ? "Yes = Save changes to the current Kearu file"
            : "Yes = Choose where to save this Kearu file";
        string message =
            $"You have unsaved Kearu Pixel Studio work in \"{resolvedDocumentName}\".\n\n" +
            $"{targetLine}\n" +
            "No = Exit without saving these changes\n" +
            "Cancel = Keep editing";

        int result = MessageBoxW(
            IntPtr.Zero,
            message,
            "Unsaved Kearu Artwork",
            MessageBoxYesNoCancel | MessageBoxTopMost | MessageBoxSetForeground);

        return result switch
        {
            MessageBoxResultYes => PixelStudioClosePromptResult.Save,
            MessageBoxResultNo => PixelStudioClosePromptResult.Discard,
            _ => PixelStudioClosePromptResult.Cancel
        };
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
