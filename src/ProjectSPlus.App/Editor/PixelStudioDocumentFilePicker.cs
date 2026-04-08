using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectSPlus.App.Editor;

public static class PixelStudioDocumentFilePicker
{
    private const string FileFilter = "Kearu Studio Sprite (*.kearu)|*.kearu|JSON Sprite (*.json)|*.json";
    private const string PngFilter = "PNG Image (*.png)|*.png";
    private const string WindowsDialogDpiSetupScript =
        "Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class NativeMethods { [DllImport(\"user32.dll\")] public static extern bool SetProcessDPIAware(); }'; " +
        "[NativeMethods]::SetProcessDPIAware() | Out-Null; " +
        "[System.Windows.Forms.Application]::EnableVisualStyles(); ";

    public static string? ShowOpenDialog(string initialDirectory)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ShowWindowsOpenDialog(initialDirectory)
            : null;
    }

    public static string? ShowSaveDialog(string initialDirectory, string suggestedFileName)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ShowWindowsSaveDialog(initialDirectory, suggestedFileName)
            : null;
    }

    public static string? ShowPngSaveDialog(string initialDirectory, string suggestedFileName)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ShowWindowsSaveDialog(initialDirectory, suggestedFileName, PngFilter, "png")
            : null;
    }

    private static string? ShowWindowsOpenDialog(string initialDirectory)
    {
        string directory = Directory.Exists(initialDirectory)
            ? initialDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        string escapedDirectory = directory.Replace("'", "''", StringComparison.Ordinal);
        string script =
            "$ErrorActionPreference='Stop'; " +
            "Add-Type -AssemblyName System.Windows.Forms; " +
            WindowsDialogDpiSetupScript +
            "$dialog = New-Object System.Windows.Forms.OpenFileDialog; " +
            $"$dialog.Filter = '{FileFilter}'; " +
            "$dialog.Multiselect = $false; " +
            $"$dialog.InitialDirectory = '{escapedDirectory}'; " +
            "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::Write($dialog.FileName) }";

        return RunWindowsPowerShellDialog(script);
    }

    private static string? ShowWindowsSaveDialog(string initialDirectory, string suggestedFileName)
    {
        return ShowWindowsSaveDialog(initialDirectory, suggestedFileName, FileFilter, "kearu");
    }

    private static string? ShowWindowsSaveDialog(string initialDirectory, string suggestedFileName, string filter, string defaultExtension)
    {
        string directory = Directory.Exists(initialDirectory)
            ? initialDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string escapedDirectory = directory.Replace("'", "''", StringComparison.Ordinal);
        string escapedFileName = suggestedFileName.Replace("'", "''", StringComparison.Ordinal);
        string escapedFilter = filter.Replace("'", "''", StringComparison.Ordinal);
        string script =
            "$ErrorActionPreference='Stop'; " +
            "Add-Type -AssemblyName System.Windows.Forms; " +
            WindowsDialogDpiSetupScript +
            "$dialog = New-Object System.Windows.Forms.SaveFileDialog; " +
            $"$dialog.Filter = '{escapedFilter}'; " +
            $"$dialog.DefaultExt = '{defaultExtension}'; " +
            "$dialog.AddExtension = $true; " +
            $"$dialog.InitialDirectory = '{escapedDirectory}'; " +
            $"$dialog.FileName = '{escapedFileName}'; " +
            "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::Write($dialog.FileName) }";

        return RunWindowsPowerShellDialog(script);
    }

    private static string? RunWindowsPowerShellDialog(string script)
    {
        string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return RunDialogProcess("powershell", $"-NoProfile -STA -EncodedCommand {encodedScript}");
    }

    private static string? RunDialogProcess(string fileName, string arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
