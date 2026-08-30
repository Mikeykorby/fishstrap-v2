using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FishstrapV2.Core;

public static class FastFlagManager
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] TelemetryFlags =
    {
        "FFlagDebugDisableTelemetryEphemeralCounter",
        "FFlagDebugDisableTelemetryEphemeralStatistic",
        "FFlagDebugDisableTelemetryEventIngest",
        "FFlagDebugDisableTelemetryPoint",
        "FFlagDebugDisableTelemetryV2Counter",
        "FFlagDebugDisableTelemetryV2Event",
        "FFlagDebugDisableTelemetryV2Stat",
    };

    /// <summary>Combines user flags (allowlist-gated) with engine settings into one flag map.</summary>
    public static Dictionary<string, JsonElement> BuildEffectiveFlags(AppSettings s)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (s.FastFlags.Enabled)
        {
            foreach (var (name, value) in s.FastFlags.Flags)
            {
                if (!s.FastFlags.EnforceAllowlist || FlagAllowlist.IsAllowed(name))
                    dict[name] = value;
            }
        }

        var e = s.Engine;

        if (e.FpsCapEnabled)
        {
            dict["FFlagTaskSchedulerLimitTargetFpsTo2402"] = Bool(true);
            // a cap below 1 breaks Roblox's renderer; treat it as unset so the default 240 cap applies
            if (e.FpsCapValue >= 1)
                dict["DFIntTaskSchedulerTargetFps"] = Num(e.FpsCapValue);
        }

        switch (e.Lighting)
        {
            case "Voxel": dict["DFFlagDebugRenderForceTechnologyVoxel"] = Bool(true); break;
            case "ShadowMap": dict["DFFlagDebugRenderForceTechnologyShadowMap"] = Bool(true); break;
            case "Future": dict["DFFlagDebugRenderForceTechnologyFuture"] = Bool(true); break;
        }

        switch (e.GraphicsMode)
        {
            case "Direct3D 11": dict["FFlagDebugGraphicsPreferD3D11"] = Bool(true); break;
            case "Direct3D 10": dict["FFlagDebugGraphicsPreferD3D11FL10"] = Bool(true); break;
            case "Vulkan": dict["FFlagDebugGraphicsPreferVulkan"] = Bool(true); break;
            case "OpenGL": dict["FFlagDebugGraphicsPreferOpenGL"] = Bool(true); break;
        }

        if (e.DisablePostEffects)
            dict["FFlagDisablePostFx"] = Bool(true);

        if (e.DisablePlayerShadows)
            dict["DFFlagDebugPauseVoxelizer"] = Bool(true);

        if (e.BlockTelemetry)
        {
            foreach (var flag in TelemetryFlags)
                dict[flag] = Bool(true);
        }

        return dict;
    }

    /// <summary>Writes the effective flag set into the version folder.</summary>
    public static void ApplyToVersion(string versionDir, AppSettings s)
    {
        try
        {
            var clientDir = Path.Combine(versionDir, "ClientSettings");
            Directory.CreateDirectory(clientDir);
            var path = Path.Combine(clientDir, "ClientAppSettings.json");
            File.WriteAllText(path, JsonSerializer.Serialize(BuildEffectiveFlags(s), JsonOpts));
            Logger.Info($"FastFlags written: {path} ({BuildEffectiveFlags(s).Count} flags)");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to write FastFlags", ex);
        }
    }

    /// <summary>Parses a raw string into a native JSON value (bool, number or string).</summary>
    public static JsonElement ParseValue(string raw)
    {
        raw = raw.Trim();
        if (bool.TryParse(raw, out var b))
            return Bool(b);
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return El(raw);
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return El(raw);
        return El(JsonSerializer.Serialize(raw));
    }

    public static string ValueToString(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? "",
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        _ => el.GetRawText(),
    };

    private static JsonElement El(string json) => JsonDocument.Parse(json).RootElement.Clone();
    private static JsonElement Bool(bool v) => El(v ? "true" : "false");
    private static JsonElement Num(int v) => El(v.ToString(CultureInfo.InvariantCulture));
}
