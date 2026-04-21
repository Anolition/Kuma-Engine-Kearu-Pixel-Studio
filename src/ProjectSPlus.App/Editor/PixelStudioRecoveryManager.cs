using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectSPlus.App.Editor;

public static class PixelStudioRecoveryManager
{
    private const int BackupRetentionCount = 12;
    private static readonly JsonSerializerOptions RecoverySerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string RecoveryFilePath => AppStoragePaths.RecoveryFilePath;

    public static string RecoveryBackupsDirectory => AppStoragePaths.RecoveryBackupsDirectory;

    public static bool TryLoad(out PixelStudioRecoverySnapshot? snapshot)
    {
        snapshot = null;
        if (!File.Exists(RecoveryFilePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(RecoveryFilePath);
            snapshot = JsonSerializer.Deserialize<PixelStudioRecoverySnapshot>(json, RecoverySerializerOptions);
            if (snapshot?.Document is null)
            {
                Clear();
                snapshot = null;
                return false;
            }

            return true;
        }
        catch
        {
            Clear();
            return false;
        }
    }

    public static void Save(PixelStudioRecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        string? directory = Path.GetDirectoryName(RecoveryFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(snapshot, RecoverySerializerOptions);
        File.WriteAllText(RecoveryFilePath, json);
        ArchiveBackupSnapshot(snapshot, json);
    }

    public static void ArchiveBackup(PixelStudioRecoverySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string json = JsonSerializer.Serialize(snapshot, RecoverySerializerOptions);
        ArchiveBackupSnapshot(snapshot, json);
    }

    public static int GetBackupCount()
    {
        try
        {
            if (!Directory.Exists(RecoveryBackupsDirectory))
            {
                return 0;
            }

            return Directory
                .GetFiles(RecoveryBackupsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Length;
        }
        catch
        {
            return 0;
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(RecoveryFilePath))
            {
                File.Delete(RecoveryFilePath);
            }
        }
        catch
        {
        }
    }

    private static void ArchiveBackupSnapshot(PixelStudioRecoverySnapshot snapshot, string json)
    {
        try
        {
            Directory.CreateDirectory(RecoveryBackupsDirectory);
            string documentName = string.IsNullOrWhiteSpace(snapshot.Document.DocumentName)
                ? "recovery"
                : SanitizeBackupFileName(snapshot.Document.DocumentName);
            string timestamp = snapshot.SavedAtUtc.ToUniversalTime().ToString("yyyyMMdd-HHmmssfff");
            string backupPath = Path.Combine(RecoveryBackupsDirectory, $"{timestamp}-{documentName}.json");
            File.WriteAllText(backupPath, json);
            PruneBackupHistory();
        }
        catch
        {
        }
    }

    private static void PruneBackupHistory()
    {
        try
        {
            if (!Directory.Exists(RecoveryBackupsDirectory))
            {
                return;
            }

            string[] files = Directory
                .GetFiles(RecoveryBackupsDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            for (int index = BackupRetentionCount; index < files.Length; index++)
            {
                File.Delete(files[index]);
            }
        }
        catch
        {
        }
    }

    private static string SanitizeBackupFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] sanitized = value
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '-' : ch)
            .ToArray();
        string result = new string(sanitized).Trim('-', '.', ' ');
        return string.IsNullOrWhiteSpace(result) ? "recovery" : result;
    }
}
