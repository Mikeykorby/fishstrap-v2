using System.IO;
using System.Text.Json;

namespace FishstrapV2.Core;

public static class ModManager
{
    public static string CursorDir => Path.Combine(Paths.ModsDir, "Cursor");
    public static string SoundsDir => Path.Combine(Paths.ModsDir, "Sounds");
    public static string FontsDir => Path.Combine(Paths.ModsDir, "Fonts");

    public static string CategoryDir(string category) => category switch
    {
        "Cursor" => CursorDir,
        "Sounds" => SoundsDir,
        "Fonts" => FontsDir,
        _ => throw new ArgumentException($"Unknown mod category: {category}"),
    };

    public static List<string> ListFiles(ModCategorySettings cat, string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return new List<string>();
            return Directory.GetFiles(dir)
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Cast<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void ImportFiles(ModCategorySettings cat, string destDir, IEnumerable<string> files)
    {
        Directory.CreateDirectory(destDir);
        foreach (var src in files)
        {
            var name = Path.GetFileName(src);
            File.Copy(src, Path.Combine(destDir, name), true);
            if (!cat.Files.Contains(name, StringComparer.OrdinalIgnoreCase))
                cat.Files.Add(name);
        }
        SettingsStore.AutoPersist();
    }

    public static void RemoveFile(ModCategorySettings cat, string dir, string fileName)
    {
        try { File.Delete(Path.Combine(dir, fileName)); } catch { }
        cat.Files.RemoveAll(f => string.Equals(f, fileName, StringComparison.OrdinalIgnoreCase));
        SettingsStore.AutoPersist();
    }

    public static void ClearCategory(ModCategorySettings cat, string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        cat.Files.Clear();
        cat.Enabled = false;
        SettingsStore.AutoPersist();
    }

    /// <summary>Copies enabled mods into the active version folder. Best-effort.</summary>
    public static void ApplyAll(string versionDir)
    {
        var s = SettingsStore.Settings;
        var applied = new List<string>();
        try
        {
            if (s.Mods.Cursor.Enabled && Directory.Exists(CursorDir))
                applied.AddRange(CopyInto(versionDir, CursorDir,
                    Path.Combine(versionDir, "content", "textures", "Cursors", "KeyboardMouse")));
            if (s.Mods.Sounds.Enabled && Directory.Exists(SoundsDir))
                applied.AddRange(CopyInto(versionDir, SoundsDir, Path.Combine(versionDir, "content", "sounds")));
            if (s.Mods.Fonts.Enabled && Directory.Exists(FontsDir))
                applied.AddRange(CopyInto(versionDir, FontsDir, Path.Combine(versionDir, "content", "fonts")));

            File.WriteAllText(Path.Combine(versionDir, ".fishstrap-mods.json"),
                JsonSerializer.Serialize(applied, new JsonSerializerOptions { WriteIndented = true }));

            if (applied.Count > 0)
                Logger.Info($"Applied {applied.Count} mod file(s) to {Path.GetFileName(versionDir)}");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to apply mods", ex);
        }
    }

    private static List<string> CopyInto(string versionDir, string srcDir, string dstDir)
    {
        var applied = new List<string>();
        Directory.CreateDirectory(dstDir);
        foreach (var file in Directory.GetFiles(srcDir))
        {
            var dst = Path.Combine(dstDir, Path.GetFileName(file));
            File.Copy(file, dst, true);
            applied.Add(dst);
        }
        return applied;
    }

    /// <summary>Deletes mod files that were previously applied to a version folder.</summary>
    public static void RemoveApplied(string versionDir)
    {
        try
        {
            var manifest = Path.Combine(versionDir, ".fishstrap-mods.json");
            if (!File.Exists(manifest)) return;

            var files = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(manifest)) ?? new List<string>();
            foreach (var f in files)
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
            File.Delete(manifest);
            Logger.Info("Removed applied mods from " + Path.GetFileName(versionDir));
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to remove mods", ex);
        }
    }
}
