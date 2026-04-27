using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectSPlus.App;

public static class CrashReporter
{
    private const string ApplicationName = "Kuma Engine";
    private const uint MessageBoxYesNoCancel = 0x00000003;
    private const uint MessageBoxTopMost = 0x00040000;
    private const uint MessageBoxSetForeground = 0x00010000;
    private const int MessageBoxResultYes = 6;
    private const int MessageBoxResultNo = 7;
    private static int _reporting;

    public static void Handle(Exception exception, string primaryLogPath)
    {
        if (Interlocked.Exchange(ref _reporting, 1) != 0)
        {
            return;
        }

        try
        {
            string reportPath = WriteCrashReport(exception, primaryLogPath);
            TryOpenCrashDialog(exception, reportPath);
        }
        catch
        {
            try
            {
                File.WriteAllText(primaryLogPath, exception.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    private static string WriteCrashReport(Exception exception, string primaryLogPath)
    {
        string baseDirectory = Path.GetDirectoryName(primaryLogPath) ?? AppContext.BaseDirectory;
        string reportDirectory = Path.Combine(baseDirectory, "crash-reports");
        Directory.CreateDirectory(reportDirectory);

        string timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string reportPath = Path.Combine(reportDirectory, $"kuma-engine-crash-{timestamp}.log");
        string crashCode = $"0x{unchecked((uint)exception.HResult):X8}";
        string location = ResolveCrashLocation(exception);

        StringBuilder reportBuilder = new();
        reportBuilder.AppendLine($"{ApplicationName} Crash Report");
        reportBuilder.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        reportBuilder.AppendLine($"Crash Code: {crashCode}");
        reportBuilder.AppendLine($"Exception Type: {exception.GetType().FullName}");
        reportBuilder.AppendLine($"Message: {exception.Message}");
        reportBuilder.AppendLine($"Where: {location}");
        reportBuilder.AppendLine($"Report Path: {reportPath}");
        reportBuilder.AppendLine();
        reportBuilder.AppendLine("Full Exception");
        reportBuilder.AppendLine(exception.ToString());

        string reportText = reportBuilder.ToString();
        File.WriteAllText(primaryLogPath, reportText, Encoding.UTF8);
        File.WriteAllText(reportPath, reportText, Encoding.UTF8);
        return reportPath;
    }

    private static string ResolveCrashLocation(Exception exception)
    {
        StackTrace stackTrace = new(exception, true);
        foreach (StackFrame frame in stackTrace.GetFrames() ?? [])
        {
            string? fileName = frame.GetFileName();
            int lineNumber = frame.GetFileLineNumber();
            if (!string.IsNullOrWhiteSpace(fileName) && lineNumber > 0)
            {
                string methodName = frame.GetMethod()?.DeclaringType is null
                    ? frame.GetMethod()?.Name ?? "Unknown"
                    : $"{frame.GetMethod()!.DeclaringType!.FullName}.{frame.GetMethod()!.Name}";
                return $"{Path.GetFileName(fileName)}:{lineNumber} in {methodName}";
            }
        }

        return exception.TargetSite?.DeclaringType is null
            ? exception.TargetSite?.Name ?? "Unknown"
            : $"{exception.TargetSite.DeclaringType.FullName}.{exception.TargetSite.Name}";
    }

    private static void TryOpenCrashDialog(Exception exception, string reportPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string crashCode = $"0x{unchecked((uint)exception.HResult):X8}";
        string location = ResolveCrashLocation(exception);
        string summary =
            $"{ApplicationName} encountered an unexpected crash.\n\n" +
            $"Crash code: {crashCode}\n" +
            $"Where: {location}\n" +
            $"Message: {exception.Message}\n\n" +
            $"Report: {reportPath}\n\n" +
            $"Yes = Open report\n" +
            $"No = Open report folder\n" +
            $"Cancel = Close";

        NotificationSoundPlayer.PlayCrashForCrashReporter();
        int result = MessageBoxW(
            IntPtr.Zero,
            summary,
            $"{ApplicationName} Crash Reporter",
            MessageBoxYesNoCancel | MessageBoxTopMost | MessageBoxSetForeground);

        if (result == MessageBoxResultYes)
        {
            TryOpenProcess("notepad.exe", $"\"{reportPath}\"");
        }
        else if (result == MessageBoxResultNo)
        {
            TryOpenProcess("explorer.exe", $"/select,\"{reportPath}\"");
        }
    }

    private static void TryOpenProcess(string fileName, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
