using System.Diagnostics;
using System.IO;

namespace FishstrapV2.Core;

public static class LaunchManager
{
    // One launch at a time: repeat clicks reuse a running launch, and a launch that finished less
    // than 5 seconds ago is not repeated (stops duplicate Roblox processes from rapid clicks).
    private static Task<RobloxVersionEntry>? _playerLaunch;
    private static DateTime _playerLastLaunch = DateTime.MinValue;
    private static Task<RobloxVersionEntry>? _studioLaunch;
    private static DateTime _studioLastLaunch = DateTime.MinValue;

    public static Task<RobloxVersionEntry> LaunchPlayerAsync(string? extraArgs = null)
    {
        if (_playerLaunch is { IsCompleted: false })
        {
            Logger.Info("Player launch already in progress — reusing it");
            return _playerLaunch;
        }
        if (_playerLaunch is { IsCompletedSuccessfully: true } && DateTime.UtcNow - _playerLastLaunch < TimeSpan.FromSeconds(5))
        {
            Logger.Info("Player was just launched — ignoring repeat request");
            return _playerLaunch;
        }

        _playerLaunch = LaunchPlayerCoreAsync(extraArgs);
        return _playerLaunch;
    }

    private static async Task<RobloxVersionEntry> LaunchPlayerCoreAsync(string? extraArgs)
    {
        // When Roblox still needs installing, show the bootstrapper dialog for the download.
        var entry = await Bootstrapper.RunAsync("Installing Roblox…", (p, ct) =>
            RobloxInstallManager.EnsurePlayerInstalledAsync(p, ct));

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
        ApplyPostLaunchSettings();
        Logger.Info($"Launched Roblox Player ({entry.Hash})");
        _playerLastLaunch = DateTime.UtcNow;
        return entry;
    }

    public static Task<RobloxVersionEntry> LaunchStudio()
    {
        if (_studioLaunch is { IsCompleted: false })
        {
            Logger.Info("Studio launch already in progress — reusing it");
            return _studioLaunch;
        }
        if (_studioLaunch is { IsCompletedSuccessfully: true } && DateTime.UtcNow - _studioLastLaunch < TimeSpan.FromSeconds(5))
        {
            Logger.Info("Studio was just launched — ignoring repeat request");
            return _studioLaunch;
        }

        _studioLaunch = LaunchStudioCoreAsync();
        return _studioLaunch;
    }

    private static Task<RobloxVersionEntry> LaunchStudioCoreAsync()
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
        _studioLastLaunch = DateTime.UtcNow;
        return Task.FromResult(entry);
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
        RobloxInstallManager.EnsureAppSettings(versionDir);
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

    /// <summary>
    /// Applies the post-launch behaviour settings: the Roblox client's process priority
    /// and killing any lingering RobloxCrashHandler processes. Every step is best-effort.
    /// </summary>
    private static void ApplyPostLaunchSettings()
    {
        var launcher = SettingsStore.Settings.Launcher;

        if (!launcher.ProcessPriority.Equals("Normal", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName("RobloxPlayerBeta"))
                {
                    if (proc.HasExited) continue;
                    proc.PriorityClass = launcher.ProcessPriority.ToLowerInvariant() switch
                    {
                        "low" => ProcessPriorityClass.Idle,
                        "below normal" => ProcessPriorityClass.BelowNormal,
                        "above normal" => ProcessPriorityClass.AboveNormal,
                        "high" => ProcessPriorityClass.High,
                        _ => ProcessPriorityClass.Normal,
                    };
                    Logger.Info($"Set Roblox process priority to {launcher.ProcessPriority}");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not set Roblox process priority: {ex.Message}");
            }
        }

        if (launcher.AutoCloseCrashHandler)
        {
            try
            {
                var killed = 0;
                foreach (var crashProc in Process.GetProcessesByName("RobloxCrashHandler"))
                {
                    try { crashProc.Kill(); killed++; } catch { }
                }
                if (killed > 0)
                    Logger.Info($"Closed {killed} lingering Roblox Crash Handler process(es)");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not close Roblox Crash Handler: {ex.Message}");
            }
        }
    }
}
