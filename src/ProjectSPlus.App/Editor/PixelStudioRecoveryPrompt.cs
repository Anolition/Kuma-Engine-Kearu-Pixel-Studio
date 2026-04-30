using System.Runtime.InteropServices;

namespace ProjectSPlus.App.Editor;

public enum PixelStudioRecoveryPromptResult
{
    Restore,
    Discard,
    Defer
}

public static class PixelStudioRecoveryPrompt
{
    private const uint MessageBoxYesNoCancel = 0x00000003;
    private const uint MessageBoxTopMost = 0x00040000;
    private const uint MessageBoxSetForeground = 0x00010000;
    private const int MessageBoxResultYes = 6;
    private const int MessageBoxResultNo = 7;

    public static PixelStudioRecoveryPromptResult Show(PixelStudioRecoverySnapshot snapshot)
    {
        if (!OperatingSystem.IsWindows())
        {
            return PixelStudioRecoveryPromptResult.Restore;
        }

        string documentName = string.IsNullOrWhiteSpace(snapshot.Document.DocumentName)
            ? "Recovered Sprite"
            : snapshot.Document.DocumentName;
        string savedAt = snapshot.SavedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        int backupCount = PixelStudioRecoveryManager.GetBackupCount();
        string summary =
            "Kuma Engine found an autosaved Kearu Studio recovery file.\n\n" +
            $"Document: {documentName}\n" +
            $"Saved: {savedAt}\n" +
            $"Canvas: {snapshot.Document.CanvasWidth} x {snapshot.Document.CanvasHeight}\n" +
            $"Frames: {snapshot.Document.Frames.Count}\n" +
            $"Backups Kept: {backupCount}\n\n" +
            "Yes = Restore autosaved work\n" +
            "No = Discard recovery\n" +
            "Cancel = Start without restoring";

        int result = MessageBoxW(
            IntPtr.Zero,
            summary,
            "Kuma Engine Recovery",
            MessageBoxYesNoCancel | MessageBoxTopMost | MessageBoxSetForeground);

        return result switch
        {
            MessageBoxResultYes => PixelStudioRecoveryPromptResult.Restore,
            MessageBoxResultNo => PixelStudioRecoveryPromptResult.Discard,
            _ => PixelStudioRecoveryPromptResult.Defer
        };
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
