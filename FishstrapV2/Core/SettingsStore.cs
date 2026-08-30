#nullable enable
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FishstrapV2.Core;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static AppSettings Settings { get; private set; } = new();

    /// <summary>
    /// When test mode is on, changes are held in memory and only written to disk
    /// when the user presses "Save".
    /// </summary>
    public static bool TestMode { get; set; }

    public static bool HasUnsavedChanges { get; internal set; }

    public static event Action? Changed;

    public static void Load()
    {
        try
        {
            Paths.EnsureDirectories();
            if (File.Exists(Paths.SettingsFile))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Paths.SettingsFile), JsonOpts);
                if (loaded is not null)
                    Settings = loaded;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load settings, using defaults", ex);
            Settings = new AppSettings();
        }
    }

    public static void Save()
    {
        try
        {
            Paths.EnsureDirectories();
            var tmp = Paths.SettingsFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Settings, JsonOpts));
            File.Move(tmp, Paths.SettingsFile, true);
            HasUnsavedChanges = false;
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save settings", ex);
        }
    }

    /// <summary>Persists changes immediately unless test mode is active.</summary>
    public static void AutoPersist()
    {
        if (TestMode)
            HasUnsavedChanges = true;
        else
            Save();
    }

    public static void Replace(AppSettings settings)
    {
        Settings = settings;
        Save();
    }
}
