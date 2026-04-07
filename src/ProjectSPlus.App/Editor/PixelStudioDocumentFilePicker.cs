using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProjectSPlus.App.Editor;

public static class PixelStudioDocumentFilePicker
{
    private const string FileFilter = "Kearu Studio Sprite (*.kearu)|*.kearu|JSON Sprite (*.json)|*.json";

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

    private static string? ShowWindowsOpenDialog(string initialDirectory)
    {
        string directory = Directory.Exists(initialDirectory)
            ? initialDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        string escapedDirectory = directory.Replace("'", "''", StringComparison.Ordinal);
        string script =
            "$ErrorActionPreference='Stop'; " +
            "Add-Type -AssemblyName System.Windows.Forms; " +
            "$dialog = New-Object System.Windows.Forms.OpenFileDialog; " +
            $"$dialog.Filter = '{FileFilter}'; " +
            "$dialog.Multiselect = $false; " +
            $"$dialog.InitialDirectory = '{escapedDirectory}'; " +
            "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::Write($dialog.FileName) }";

        return RunDialogProcess("powershell", $"-NoProfile -STA -Command \"{script}\"");
    }

    private static string? ShowWindowsSaveDialog(string initialDirectory, string suggestedFileName)
    {
        string directory = Directory.Exists(initialDirectory)
            ? initialDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string escapedDirectory = directory.Replace("'", "''", StringComparison.Ordinal);
        string escapedFileName = suggestedFileName.Replace("'", "''", StringComparison.Ordinal);
        string script =
            "$ErrorActionPreference='Stop'; " +
            "Add-Type -AssemblyName System.Windows.Forms; " +
            "$dialog = New-Object System.Windows.Forms.SaveFileDialog; " +
            $"$dialog.Filter = '{FileFilter}'; " +
            "$dialog.DefaultExt = 'kearu'; " +
            "$dialog.AddExtension = $true; " +
            $"$dialog.InitialDirectory = '{escapedDirectory}'; " +
            $"$dialog.FileName = '{escapedFileName}'; " +
            "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::Write($dialog.FileName) }";

        return RunDialogProcess("powershell", $"-NoProfile -STA -Command \"{script}\"");
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
