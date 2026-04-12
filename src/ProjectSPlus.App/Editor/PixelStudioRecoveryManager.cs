using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjectSPlus.App.Editor;

public static class PixelStudioRecoveryManager
{
    private static readonly JsonSerializerOptions RecoverySerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string RecoveryFilePath => AppStoragePaths.RecoveryFilePath;

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
}
