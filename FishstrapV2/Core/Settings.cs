using System.Text.Json;

namespace FishstrapV2.Core;

public class AppSettings
{
    public LauncherSettings Launcher { get; set; } = new();
    public DeploymentSettings Deployment { get; set; } = new();
    public IntegrationsSettings Integrations { get; set; } = new();
    public ModsSettings Mods { get; set; } = new();
    public FastFlagSettings FastFlags { get; set; } = new();
    public EngineSettings Engine { get; set; } = new();
    public AppearanceSettings Appearance { get; set; } = new();
    public ShortcutsSettings Shortcuts { get; set; } = new();
    public MiscSettings Misc { get; set; } = new();
}

public class LauncherSettings
{
    public bool AutoCloseAfterLaunch { get; set; } = false;
    public bool CreateShortcutsOnInstall { get; set; } = true;
    public string LaunchArgs { get; set; } = "";
    /// <summary>Bootstrapper progress dialog: "Fishstrap" (default), "Disabled", a fishstrap original style ("Classic Fluent", "Terminal", "TwentyFive"), Bloxnified V2, or an imported theme name.</summary>
    public string BootstrapperStyle { get; set; } = "Fishstrap";
    public string BootstrapperTitle { get; set; } = "Fishstrap V2";
    /// <summary>Bootstrapper logo animation: "None" (static), "Spin", or "Custom".</summary>
    public string BootstrapperAnimation { get; set; } = "None";
    /// <summary>Image (PNG or animated GIF) shown when BootstrapperAnimation is "Custom".</summary>
    public string BootstrapperIconFile { get; set; } = "";
}

public class DeploymentSettings
{
    public string Channel { get; set; } = "production";
    public bool WaitForLatest { get; set; } = false;
    public string ServerLocation { get; set; } = "Automatic";
    public int KeepOldVersions { get; set; } = 1;
}

public class DiscordRpcSettings
{
    public bool Enabled { get; set; } = false;
    public bool ShowElapsedTime { get; set; } = true;
    public string Details { get; set; } = "Playing {game}";
    public string State { get; set; } = "via Fishstrap V2";
}

public class IntegrationsSettings
{
    public DiscordRpcSettings DiscordRpc { get; set; } = new();
    public bool ActivityTracking { get; set; } = true;
}

public class ModCategorySettings
{
    public bool Enabled { get; set; } = false;
    public List<string> Files { get; set; } = new();
}

public class ModsSettings
{
    public ModCategorySettings Cursor { get; set; } = new();
    public ModCategorySettings Sounds { get; set; } = new();
    public ModCategorySettings Fonts { get; set; } = new();
}

public class FastFlagSettings
{
    public bool Enabled { get; set; } = true;
    public bool EnforceAllowlist { get; set; } = true;
    public Dictionary<string, JsonElement> Flags { get; set; } = new();
}

public class EngineSettings
{
    public bool FpsCapEnabled { get; set; } = false;
    public int FpsCapValue { get; set; } = 240;
    public string Lighting { get; set; } = "Automatic";
    public string GraphicsMode { get; set; } = "Automatic";
    public bool DisablePostEffects { get; set; } = false;
    public bool DisablePlayerShadows { get; set; } = false;
    public bool BlockTelemetry { get; set; } = false;
}

public class AppearanceSettings
{
    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#7C6CF0";
    public string CustomIcon { get; set; } = "";
}

public class ShortcutsSettings
{
    public bool DesktopShortcut { get; set; } = true;
    public bool StartMenuShortcut { get; set; } = true;
    public bool CreateOnInstall { get; set; } = true;
    public bool SettingsShortcut { get; set; } = false;
    public bool JoinLastServerShortcut { get; set; } = false;
}

public class MiscSettings
{
    public bool AutoCheckUpdates { get; set; } = true;
    public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;
    public string LastPlaceId { get; set; } = "";
}
