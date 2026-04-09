using System.Runtime.InteropServices;
using System.Text;

namespace ProjectSPlus.App.Editor;

internal static class WindowsNativeFileDialog
{
    private const int MaxFilePathLength = 4096;
    private const int OpenFileFlags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;
    private const int SaveFileFlags = 0x00080000 | 0x00000002 | 0x00000800 | 0x00000008;

    public static string? ShowOpenFileDialog(string initialDirectory, string filter)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        OpenFileName dialog = CreateDialog(initialDirectory, null, filter, null, OpenFileFlags);
        return GetOpenFileNameW(ref dialog) ? TrimDialogResult(dialog.FileBuffer) : null;
    }

    public static string? ShowSaveFileDialog(string initialDirectory, string suggestedFileName, string filter, string defaultExtension)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        OpenFileName dialog = CreateDialog(initialDirectory, suggestedFileName, filter, defaultExtension, SaveFileFlags);
        return GetSaveFileNameW(ref dialog) ? TrimDialogResult(dialog.FileBuffer) : null;
    }

    private static OpenFileName CreateDialog(string initialDirectory, string? suggestedFileName, string filter, string? defaultExtension, int flags)
    {
        string resolvedDirectory = Directory.Exists(initialDirectory)
            ? initialDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        StringBuilder fileBuffer = new(MaxFilePathLength);
        if (!string.IsNullOrWhiteSpace(suggestedFileName))
        {
            fileBuffer.Append(suggestedFileName);
        }

        return new OpenFileName
        {
            StructSize = Marshal.SizeOf<OpenFileName>(),
            OwnerHandle = GetActiveWindow(),
            Filter = BuildNativeFilter(filter),
            FilterIndex = 1,
            FileBuffer = fileBuffer,
            MaxFile = fileBuffer.Capacity,
            InitialDirectory = resolvedDirectory,
            Flags = flags,
            DefaultExtension = defaultExtension ?? string.Empty
        };
    }

    private static string BuildNativeFilter(string filter)
    {
        string normalized = string.IsNullOrWhiteSpace(filter)
            ? "All Files (*.*)|*.*"
            : filter;
        return normalized.Replace('|', '\0') + "\0\0";
    }

    private static string? TrimDialogResult(StringBuilder fileBuffer)
    {
        if (fileBuffer.Length == 0)
        {
            return null;
        }

        string value = fileBuffer.ToString();
        int nullIndex = value.IndexOf('\0');
        string trimmed = (nullIndex >= 0 ? value[..nullIndex] : value).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName dialog);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileNameW(ref OpenFileName dialog);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int StructSize;
        public IntPtr OwnerHandle;
        public IntPtr InstanceHandle;
        public string Filter;
        public string? CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;
        public StringBuilder FileBuffer;
        public int MaxFile;
        public StringBuilder? FileTitleBuffer;
        public int MaxFileTitle;
        public string InitialDirectory;
        public string? Title;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        public string DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        public string? TemplateName;
        public IntPtr ReservedPtr;
        public int ReservedInt;
        public int ExtendedFlags;
    }
}
