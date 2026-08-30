using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FishstrapV2.Core;

/// <summary>Named configuration profiles: save the full settings state and swap instantly.</summary>
public static class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static List<FileInfo> ListProfiles()
    {
        try
        {
            return new DirectoryInfo(Paths.ProfilesDir)
                .GetFiles("*.json")
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<FileInfo>();
        }
    }

    public static void Save(string name)
    {
        Paths.EnsureDirectories();
        var safe = Sanitize(name);
        File.WriteAllText(Path.Combine(Paths.ProfilesDir, safe + ".json"),
            JsonSerializer.Serialize(SettingsStore.Settings, JsonOpts));
        Logger.Info($"Saved profile '{safe}'");
    }

    public static void Apply(string name)
    {
        var path = Path.Combine(Paths.ProfilesDir, Sanitize(name) + ".json");
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOpts)
                       ?? throw new InvalidOperationException("Profile file is not valid.");
        SettingsStore.Replace(settings);
        Logger.Info($"Applied profile '{Sanitize(name)}'");
    }

    public static void Delete(string name)
    {
        try { File.Delete(Path.Combine(Paths.ProfilesDir, Sanitize(name) + ".json")); } catch { }
    }

    public static string Sanitize(string name)
    {
        var clean = string.Join("", name.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')).Trim();
        return clean.Length == 0 ? "profile" : clean;
    }
}
