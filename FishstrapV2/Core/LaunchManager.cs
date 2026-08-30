using System.Diagnostics;
using System.IO;

namespace FishstrapV2.Core;

public static class LaunchManager
{
    public static async Task<RobloxVersionEntry> LaunchPlayerAsync(string? extraArgs = null)
    {
        var progress = new Progress<string>(m => Logger.Info(m));
        var entry = await RobloxInstallManager.EnsurePlayerInstalledAsync(progress);

        Prepare(entry.DirectoryPath);

        var exe = Path.Combine(entry.DirectoryPath, "RobloxPlayerBeta.exe");
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = BuildArgs(extraArgs),
            WorkingDirectory = entry.DirectoryPath,
            UseShellExecute = true,
        };

        if (SettingsStore.Settings.Integrations.ActivityTracking)
            StatisticsStore.RecordLaunch("Player", exe);

        Process.Start(psi);
        Logger.Info($"Launched Roblox Player ({entry.Hash})");
        return entry;
    }

    public static RobloxVersionEntry LaunchStudio()
    {
        var entry = RobloxInstallManager.GetActiveVersion("Studio")
                    ?? throw new InvalidOperationException(
                        "Roblox Studio is not installed. Install it from the Deployment page first.");

        Prepare(entry.DirectoryPath);

        var exe = Path.Combine(entry.DirectoryPath, "RobloxStudioBeta.exe");

        if (SettingsStore.Settings.Integrations.ActivityTracking)
            StatisticsStore.RecordLaunch("Studio", exe);

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = entry.DirectoryPath,
            UseShellExecute = true,
        });

        Logger.Info($"Launched Roblox Studio ({entry.Hash})");
        return entry;
    }

    /// <summary>Opens the last played game page in the browser (join-last-server shortcut).</summary>
    public static void OpenLastGame()
    {
        var placeId = SettingsStore.Settings.Misc.LastPlaceId;
        if (string.IsNullOrWhiteSpace(placeId))
            throw new InvalidOperationException("No game has been played yet.");

        Process.Start(new ProcessStartInfo
        {
            FileName = $"https://www.roblox.com/games/{placeId}",
            UseShellExecute = true,
        });
    }

    private static void Prepare(string versionDir)
    {
        FastFlagManager.ApplyToVersion(versionDir, SettingsStore.Settings);
        ModManager.ApplyAll(versionDir);
    }

    private static string BuildArgs(string? extra)
    {
        var s = SettingsStore.Settings;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.Launcher.LaunchArgs))
            parts.Add(s.Launcher.LaunchArgs.Trim());
        if (!string.IsNullOrWhiteSpace(extra))
            parts.Add(extra.Trim());
        return string.Join(" ", parts.Where(p => p.Length > 0));
    }
}
