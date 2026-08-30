using System.IO;

namespace FishstrapV2.Core;

public static class CacheCleaner
{
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
