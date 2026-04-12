using System.Runtime.InteropServices;
using System.Text;
using ProjectSPlus.App;

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

        using NativeOpenFileDialog dialog = NativeOpenFileDialog.Create(initialDirectory, null, filter, null, OpenFileFlags);
        return GetOpenFileNameW(ref dialog.NativeData) ? dialog.ReadFilePath() : null;
    }

    public static string? ShowSaveFileDialog(string initialDirectory, string suggestedFileName, string filter, string defaultExtension)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using NativeOpenFileDialog dialog = NativeOpenFileDialog.Create(initialDirectory, suggestedFileName, filter, defaultExtension, SaveFileFlags);
        return GetSaveFileNameW(ref dialog.NativeData) ? dialog.ReadFilePath() : null;
    }

    private static string BuildNativeFilter(string filter)
    {
        string normalized = string.IsNullOrWhiteSpace(filter)
            ? "All Files (*.*)|*.*"
            : filter;
        return normalized.Replace('|', '\0') + "\0\0";
    }

    private static IntPtr AllocUnicodeBuffer(int charCapacity)
    {
        int byteCount = checked(charCapacity * sizeof(char));
        IntPtr pointer = Marshal.AllocHGlobal(byteCount);
        Marshal.Copy(new byte[byteCount], 0, pointer, byteCount);
        return pointer;
    }

    private static void WriteUnicodeStringToBuffer(IntPtr buffer, int charCapacity, string value)
    {
        if (buffer == IntPtr.Zero || charCapacity <= 0 || string.IsNullOrEmpty(value))
        {
            return;
        }

        string truncated = value.Length >= charCapacity
            ? value[..(charCapacity - 1)]
            : value;
        byte[] bytes = Encoding.Unicode.GetBytes(truncated + '\0');
        Marshal.Copy(bytes, 0, buffer, Math.Min(bytes.Length, charCapacity * sizeof(char)));
    }

    private static string? ReadUnicodeStringFromBuffer(IntPtr buffer)
    {
        if (buffer == IntPtr.Zero)
        {
            return null;
        }

        string? value = Marshal.PtrToStringUni(buffer);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void FreeIfAllocated(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pointer);
        }
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
        public IntPtr Filter;
        public IntPtr CustomFilter;
        public int MaxCustomFilter;
        public int FilterIndex;
        public IntPtr FileBuffer;
        public int MaxFile;
        public IntPtr FileTitleBuffer;
        public int MaxFileTitle;
        public IntPtr InitialDirectory;
        public IntPtr Title;
        public int Flags;
        public short FileOffset;
        public short FileExtension;
        public IntPtr DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        public IntPtr TemplateName;
        public IntPtr ReservedPtr;
        public int ReservedInt;
        public int ExtendedFlags;
    }

    private sealed class NativeOpenFileDialog : IDisposable
    {
        private readonly IntPtr _filterPointer;
        private readonly IntPtr _fileBufferPointer;
        private readonly IntPtr _initialDirectoryPointer;
        private readonly IntPtr _defaultExtensionPointer;

        private NativeOpenFileDialog(
            OpenFileName nativeData,
            IntPtr filterPointer,
            IntPtr fileBufferPointer,
            IntPtr initialDirectoryPointer,
            IntPtr defaultExtensionPointer)
        {
            NativeData = nativeData;
            _filterPointer = filterPointer;
            _fileBufferPointer = fileBufferPointer;
            _initialDirectoryPointer = initialDirectoryPointer;
            _defaultExtensionPointer = defaultExtensionPointer;
        }

        public OpenFileName NativeData;

        public static NativeOpenFileDialog Create(string initialDirectory, string? suggestedFileName, string filter, string? defaultExtension, int flags)
        {
            string resolvedDirectory = Directory.Exists(initialDirectory)
                ? initialDirectory
                : AppStoragePaths.DefaultProjectLibraryPath;

            IntPtr filterPointer = IntPtr.Zero;
            IntPtr fileBufferPointer = IntPtr.Zero;
            IntPtr initialDirectoryPointer = IntPtr.Zero;
            IntPtr defaultExtensionPointer = IntPtr.Zero;

            try
            {
                filterPointer = Marshal.StringToHGlobalUni(BuildNativeFilter(filter));
                fileBufferPointer = AllocUnicodeBuffer(MaxFilePathLength);
                if (!string.IsNullOrWhiteSpace(suggestedFileName))
                {
                    WriteUnicodeStringToBuffer(fileBufferPointer, MaxFilePathLength, suggestedFileName);
                }

                initialDirectoryPointer = Marshal.StringToHGlobalUni(resolvedDirectory);
                if (!string.IsNullOrWhiteSpace(defaultExtension))
                {
                    defaultExtensionPointer = Marshal.StringToHGlobalUni(defaultExtension);
                }

                OpenFileName nativeData = new()
                {
                    StructSize = Marshal.SizeOf<OpenFileName>(),
                    OwnerHandle = GetActiveWindow(),
                    InstanceHandle = IntPtr.Zero,
                    Filter = filterPointer,
                    CustomFilter = IntPtr.Zero,
                    MaxCustomFilter = 0,
                    FilterIndex = 1,
                    FileBuffer = fileBufferPointer,
                    MaxFile = MaxFilePathLength,
                    FileTitleBuffer = IntPtr.Zero,
                    MaxFileTitle = 0,
                    InitialDirectory = initialDirectoryPointer,
                    Title = IntPtr.Zero,
                    Flags = flags,
                    FileOffset = 0,
                    FileExtension = 0,
                    DefaultExtension = defaultExtensionPointer,
                    CustomData = IntPtr.Zero,
                    Hook = IntPtr.Zero,
                    TemplateName = IntPtr.Zero,
                    ReservedPtr = IntPtr.Zero,
                    ReservedInt = 0,
                    ExtendedFlags = 0
                };

                return new NativeOpenFileDialog(
                    nativeData,
                    filterPointer,
                    fileBufferPointer,
                    initialDirectoryPointer,
                    defaultExtensionPointer);
            }
            catch
            {
                FreeIfAllocated(filterPointer);
                FreeIfAllocated(fileBufferPointer);
                FreeIfAllocated(initialDirectoryPointer);
                FreeIfAllocated(defaultExtensionPointer);
                throw;
            }
        }

        public string? ReadFilePath()
        {
            return ReadUnicodeStringFromBuffer(_fileBufferPointer);
        }

        public void Dispose()
        {
            FreeIfAllocated(_filterPointer);
            FreeIfAllocated(_fileBufferPointer);
            FreeIfAllocated(_initialDirectoryPointer);
            FreeIfAllocated(_defaultExtensionPointer);
            NativeData = default;
            GC.SuppressFinalize(this);
        }
    }
}
