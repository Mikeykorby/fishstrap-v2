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
            FastFlagManager.ApplyToVersion(dir, SettingsStore.Settings);
            ModManager.ApplyAll(dir);
            EnsureWebView2Runtime(dir);
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

    /// <summary>Roblox needs the WebView2 runtime; bootstrap it from the offline installer if absent.</summary>
    private static void EnsureWebView2Runtime(string dir)
    {
        const string clientGuid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

        foreach (var root in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
        foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
        {
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(root, view);
            using var key = baseKey.OpenSubKey($@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{clientGuid}");
            if (key?.GetValue("pv") is string pv && pv.Length > 1)
                return;
        }

        var setup = Path.Combine(dir, "MicrosoftEdgeWebview2Setup.exe");
        if (!File.Exists(setup))
        {
            Logger.Warn("WebView2 runtime not found and no offline installer present");
            return;
        }

        Logger.Info("Installing the WebView2 runtime…");
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = setup,
                Arguments = "/silent /install",
                UseShellExecute = true,
            });
            proc?.WaitForExit(120_000);
            Logger.Info("WebView2 runtime install finished");
        }
        catch (Exception ex)
        {
            Logger.Error("WebView2 runtime install failed", ex);
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
