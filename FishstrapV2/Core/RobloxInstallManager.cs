using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace FishstrapV2.Core;

public class RobloxVersionEntry
{
    public string Hash { get; set; } = "";
    public string Channel { get; set; } = "production";
    public DateTime Installed { get; set; } = DateTime.Now;
    public bool Pinned { get; set; }
    public long SizeBytes { get; set; }
    public bool HasPlayer { get; set; }
    public bool HasStudio { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string DirectoryPath => Path.Combine(Paths.VersionsDir, Hash);
}

public static class RobloxInstallManager
{
    public static List<RobloxVersionEntry> GetInstalledVersions()
    {
        var list = new List<RobloxVersionEntry>();
        try
        {
            if (!Directory.Exists(Paths.VersionsDir))
                return list;

            foreach (var dir in Directory.GetDirectories(Paths.VersionsDir, "version-*"))
            {
                var sidecar = Path.Combine(dir, ".fishstrap.json");

                // Only versions whose sidecar exists (install ran to completion) are managed.
                if (!File.Exists(sidecar))
                    continue;

                var entry = new RobloxVersionEntry { Hash = Path.GetFileName(dir) };

                try
                {
                    var loaded = JsonSerializer.Deserialize<RobloxVersionEntry>(File.ReadAllText(sidecar));
                    if (loaded is not null)
                    {
                        entry.Channel = loaded.Channel;
                        entry.Installed = loaded.Installed;
                        entry.Pinned = loaded.Pinned;
                    }
                }
                catch { /* ignore corrupt sidecar */ }

                entry.HasPlayer = File.Exists(Path.Combine(dir, "RobloxPlayerBeta.exe"));
                entry.HasStudio = File.Exists(Path.Combine(dir, "RobloxStudioBeta.exe"));
                entry.SizeBytes = GetDirectorySize(dir);
                list.Add(entry);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to scan installed Roblox versions", ex);
        }

        return list
            .OrderByDescending(v => v.Pinned)
            .ThenByDescending(v => v.Installed)
            .ThenByDescending(v => v.Hash, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static RobloxVersionEntry? GetActiveVersion(string binaryType = "Player")
    {
        var versions = GetInstalledVersions();
        return binaryType == "Player"
            ? versions.FirstOrDefault(v => v.HasPlayer)
            : versions.FirstOrDefault(v => v.HasStudio);
    }

    public static async Task<RobloxVersionEntry> EnsurePlayerInstalledAsync(IProgress<string>? progress = null)
    {
        var active = GetActiveVersion("Player");
        if (active is not null)
            return active;
        return await InstallAsync(progress, includeStudio: false, forceReinstall: false);
    }

    public static async Task<RobloxVersionEntry> InstallAsync(
        IProgress<string>? progress = null, bool includeStudio = true, bool forceReinstall = false)
    {
        progress?.Report("Checking latest Roblox version…");

        var info = await RobloxDeployClient.GetLatestVersionAsync(SettingsStore.Settings.Deployment.Channel, "WindowsPlayer")
                   ?? throw new InvalidOperationException(
                       "Could not reach the Roblox deployment API. Check your internet connection and channel name.");

        var dir = Path.Combine(Paths.VersionsDir, info.VersionHash);
        var exe = Path.Combine(dir, "RobloxPlayerBeta.exe");

        if (Directory.Exists(dir) && (forceReinstall || !IsComplete(dir, "RobloxPlayerBeta.exe")))
        {
            progress?.Report(forceReinstall ? "Removing existing installation…" : "Finishing an interrupted installation…");
            TryDeleteDirectory(dir);
        }

        if (!File.Exists(exe))
        {
            progress?.Report($"Downloading Roblox {info.Version}…");
            await RobloxDeployClient.DownloadVersionAsync(info.VersionHash, dir, progress);
            progress?.Report("Applying settings…");
            EnsureAppSettings(dir);
            FastFlagManager.ApplyToVersion(dir, SettingsStore.Settings);
            ModManager.ApplyAll(dir);
            if (SettingsStore.Settings.Launcher.CreateShortcutsOnInstall)
                ShortcutManager.CreateAppShortcuts();
        }

        WriteSidecar(dir, info.VersionHash);

        if (includeStudio)
        {
            var sinfo = await RobloxDeployClient.GetLatestVersionAsync(SettingsStore.Settings.Deployment.Channel, "WindowsStudio64");
            if (sinfo is not null)
            {
                var sdir = Path.Combine(Paths.VersionsDir, sinfo.VersionHash);
                if (Directory.Exists(sdir) && !IsComplete(sdir, "RobloxStudioBeta.exe"))
                    TryDeleteDirectory(sdir);
                if (!File.Exists(Path.Combine(sdir, "RobloxStudioBeta.exe")))
                {
                    progress?.Report("Downloading Roblox Studio…");
                    await RobloxDeployClient.DownloadVersionAsync(sinfo.VersionHash, sdir, progress);
                    WriteSidecar(sdir, sinfo.VersionHash);
                }
            }
        }

        PruneOldVersions();
        progress?.Report("Done");

        return new RobloxVersionEntry
        {
            Hash = info.VersionHash,
            Channel = SettingsStore.Settings.Deployment.Channel,
            Installed = DateTime.Now,
            HasPlayer = File.Exists(exe),
        };
    }

    public static void PruneOldVersions()
    {
        try
        {
            int keep = Math.Max(0, SettingsStore.Settings.Deployment.KeepOldVersions);
            var versions = GetInstalledVersions();
            var toDelete = versions.Skip(1 + keep).Where(v => !v.Pinned).ToList();

            foreach (var v in toDelete)
            {
                TryDeleteDirectory(v.DirectoryPath);
                Logger.Info($"Pruned old version {v.Hash}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to prune old versions", ex);
        }
    }

    public static void UninstallAll()
    {
        foreach (var v in GetInstalledVersions())
            TryDeleteDirectory(v.DirectoryPath);
    }

    /// <summary>An install counts as complete only when the sidecar written at the end exists.</summary>
    private static bool IsComplete(string dir, string exeName) =>
        File.Exists(Path.Combine(dir, ".fishstrap.json")) && File.Exists(Path.Combine(dir, exeName));

    /// <summary>Roblox's client requires AppSettings.xml to find its content folder; it ships in no package, so write it.</summary>
    public static void EnsureAppSettings(string dir)
    {
        try
        {
            var path = Path.Combine(dir, "AppSettings.xml");
            if (File.Exists(path))
                return;

            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Settings>\r\n\t<ContentFolder>content</ContentFolder>\r\n\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n</Settings>\r\n");
        }
        catch (Exception ex)
        {
            Logger.Warn("Failed to write AppSettings.xml: " + ex.Message);
        }
    }

    public static void WriteSidecar(string dir, string hash)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var entry = new RobloxVersionEntry
            {
                Hash = hash,
                Channel = SettingsStore.Settings.Deployment.Channel,
                Installed = DateTime.Now,
            };
            File.WriteAllText(Path.Combine(dir, ".fishstrap.json"),
                JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Logger.Warn("Failed to write version sidecar: " + ex.Message);
        }
    }

    public static long GetDirectorySize(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
        }
        catch
        {
            return 0;
        }
    }

    internal static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to delete {path}", ex);
        }
    }
}
