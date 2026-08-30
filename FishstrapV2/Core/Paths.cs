using System.IO;

namespace FishstrapV2.Core;

public static class Paths
{
    public static readonly string AppData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FishstrapV2");

    public static readonly string SettingsFile = Path.Combine(AppData, "settings.json");
    public static readonly string StatisticsFile = Path.Combine(AppData, "statistics.json");
    public static readonly string LogsDir = Path.Combine(AppData, "logs");
    public static readonly string ModsDir = Path.Combine(AppData, "Mods");
    public static readonly string BackupsDir = Path.Combine(AppData, "Backups");
    public static readonly string ProfilesDir = Path.Combine(AppData, "Profiles");
    public static readonly string DownloadsDir = Path.Combine(AppData, "Downloads");
    public static readonly string VersionsDir = Path.Combine(AppData, "Versions");
    public static readonly string CustomIconFile = Path.Combine(AppData, "custom-icon.png");

    public static string StockRobloxRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox");

    public static string StockVersionsDir => Path.Combine(StockRobloxRoot, "Versions");
    public static string RobloxLogsDir => Path.Combine(StockRobloxRoot, "logs");
    public static string RobloxHttpCache => Path.Combine(Path.GetTempPath(), "Roblox");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppData);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(ModsDir);
        Directory.CreateDirectory(BackupsDir);
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(DownloadsDir);
        Directory.CreateDirectory(VersionsDir);
    }
}
