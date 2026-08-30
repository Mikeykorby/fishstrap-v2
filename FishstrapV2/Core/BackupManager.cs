using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FishstrapV2.Core;

/// <summary>Snapshots of the entire settings state (restore points).</summary>
public static class BackupManager
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static List<FileInfo> ListBackups()
    {
        try
        {
            return new DirectoryInfo(Paths.BackupsDir)
                .GetFiles("backup-*.json")
                .OrderByDescending(f => f.Name)
                .ToList();
        }
        catch
        {
            return new List<FileInfo>();
        }
    }

    public static string CreateBackup(string? label = null)
    {
        Paths.EnsureDirectories();
        var safeLabel = label is null ? "" : "-" + string.Join("", label.Where(char.IsLetterOrDigit)).Trim('-');
        var name = $"backup-{DateTime.Now:yyyyMMdd-HHmmss}{safeLabel}.json";
        var path = Path.Combine(Paths.BackupsDir, name);
        File.WriteAllText(path, JsonSerializer.Serialize(SettingsStore.Settings, JsonOpts));
        Logger.Info($"Created settings backup {name}");
        return name;
    }

    public static void Restore(string filePath)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), JsonOpts)
                       ?? throw new InvalidOperationException("Backup file is not valid.");
        SettingsStore.Replace(settings);
        Logger.Info($"Restored settings from {Path.GetFileName(filePath)}");
    }

    public static void DeleteBackup(string filePath)
    {
        try { File.Delete(filePath); } catch { }
    }
}
