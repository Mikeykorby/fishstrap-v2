using System.IO;

namespace FishstrapV2.Core;

public static class CacheCleaner
{
    // Scheduled cleaning (ported from Froststrap's Cleaner): delete cache files older
    // than the configured age, capped per directory so a huge backlog can't stall startup.
    private const int MaxFilesPerDirectory = 200;

    /// <summary>Max age in hours for the AutoCleanCache setting; null means disabled.</summary>
    private static int? MaxAgeHours(string setting) => setting switch
    {
        "Daily" => 24,
        "Weekly" => 24 * 7,
        "Monthly" => 24 * 30,
        "Two Months" => 24 * 60,
        _ => null,
    };

    /// <summary>
    /// Runs the scheduled cleaner if enabled. Called at app startup; best-effort.
    /// </summary>
    public static void RunScheduled()
    {
        var hours = MaxAgeHours(SettingsStore.Settings.Launcher.AutoCleanCache);
        if (hours is null) return;

        int deleted = CleanOldFiles(hours.Value);
        if (deleted > 0)
            Logger.Info($"Scheduled cache clean removed {deleted} old file(s)");
    }

    /// <summary>Deletes files older than maxAgeHours across all cache targets.</summary>
    public static int CleanOldFiles(int maxAgeHours)
    {
        var threshold = DateTime.UtcNow - TimeSpan.FromHours(maxAgeHours);
        var roots = new[] { Paths.LogsDir, Paths.DownloadsDir, Paths.RobloxHttpCache, Paths.RobloxLogsDir };
        var totalDeleted = 0;

        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;

            // Upstream caps deletions per directory per run so a huge backlog can't stall startup.
            var deleted = 0;
            foreach (var file in EnumerateFilesSafe(root))
            {
                if (deleted >= MaxFilesPerDirectory) break;
                if (File.GetLastWriteTimeUtc(file) > threshold) continue;
                if (!IsSafeToDelete(file, root)) continue;

                try { File.Delete(file); deleted++; totalDeleted++; }
                catch { /* locked files (e.g. the live client log) stay */ }
            }
        }

        return totalDeleted;
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var files = Enumerable.Empty<string>();
        try { files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
        catch (Exception ex) { Logger.Warn($"Cache clean skipped {root}: {ex.Message}"); }
        return files;
    }

    /// <summary>Only paths under our own cache roots may be deleted (path-prefix, not substring).</summary>
    public static bool IsSafeToDelete(string file, string root)
    {
        var fullFile = Path.GetFullPath(file);
        var fullRoot = Path.GetFullPath(root);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return fullFile.StartsWith(fullRoot, comparison)
            && !fullFile.Contains("Windows", comparison);
    }

    public static List<(string Label, string Path)> GetTargets()
    {
        var targets = new List<(string, string)>();
        void Add(string label, string path)
        {
            if (Directory.Exists(path)) targets.Add((label, path));
        }

        Add("Roblox HTTP cache", Paths.RobloxHttpCache);
        Add("Download cache", Paths.DownloadsDir);
        Add("Old log files", Paths.LogsDir);
        return targets;
    }

    public static long GetDirectorySize(string path) => RobloxInstallManager.GetDirectorySize(path);

    public static long GetTotalSize()
    {
        long total = 0;
        foreach (var (_, path) in GetTargets())
            total += GetDirectorySize(path);
        return total;
    }

    /// <summary>Deletes all cache targets and returns the number of bytes freed.</summary>
    public static long CleanAll()
    {
        long freed = 0;
        foreach (var (_, path) in GetTargets())
        {
            try
            {
                freed += GetDirectorySize(path);
                foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
                    RobloxInstallManager.TryDeleteDirectory(dir);
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly))
                {
                    try { File.Delete(file); } catch { }
                }
                Directory.CreateDirectory(Paths.DownloadsDir);
                Directory.CreateDirectory(Paths.LogsDir);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to clean {path}", ex);
            }
        }
        Logger.Info($"Cache clean freed {FormatSize(freed)}");
        return freed;
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1L * 1024 * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:F1} GB",
        >= 1L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B",
    };
}
