#nullable enable
using System.IO;

namespace FishstrapV2.Core;

public static class ShortcutManager
{
    public static string DesktopDir =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string StartMenuDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Windows", "Start Menu", "Programs");

    public static void CreateShortcut(string lnkPath, string target, string arguments, string description, string? iconPath = null)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell COM is unavailable.");

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(lnkPath);
            shortcut.TargetPath = target;
            shortcut.Arguments = arguments ?? "";
            shortcut.WorkingDirectory = Path.GetDirectoryName(target) ?? "";
            shortcut.Description = description ?? "";
            shortcut.IconLocation = (iconPath ?? target) + ",0";
            shortcut.Save();
            Logger.Info($"Created shortcut: {lnkPath}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to create shortcut {lnkPath}", ex);
            throw;
        }
    }

    public static void CreateAppShortcuts()
    {
        var s = SettingsStore.Settings.Shortcuts;
        var exe = AppInfo.ExePath;

        try
        {
            if (s.DesktopShortcut)
                CreateShortcut(Path.Combine(DesktopDir, "Fishstrap V2.lnk"), exe, "", "Fishstrap V2 launcher");
            if (s.StartMenuShortcut)
                CreateShortcut(Path.Combine(StartMenuDir, "Fishstrap V2.lnk"), exe, "", "Fishstrap V2 launcher");
            if (s.SettingsShortcut)
                CreateShortcut(Path.Combine(DesktopDir, "Fishstrap V2 Settings.lnk"), exe, "--settings",
                    "Open Fishstrap V2 settings");
        }
        catch (Exception ex)
        {
            Logger.Warn("Shortcut creation failed: " + ex.Message);
        }
    }

    public static void RemoveShortcuts()
    {
        TryDelete(Path.Combine(DesktopDir, "Fishstrap V2.lnk"));
        TryDelete(Path.Combine(StartMenuDir, "Fishstrap V2.lnk"));
        TryDelete(Path.Combine(DesktopDir, "Fishstrap V2 Settings.lnk"));
    }

    public static string[] ExistingShortcuts()
    {
        return new[]
        {
            Path.Combine(DesktopDir, "Fishstrap V2.lnk"),
            Path.Combine(StartMenuDir, "Fishstrap V2.lnk"),
            Path.Combine(DesktopDir, "Fishstrap V2 Settings.lnk"),
        }.Where(File.Exists).ToArray();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
