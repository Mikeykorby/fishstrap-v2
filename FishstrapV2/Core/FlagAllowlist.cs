using System.Collections.Generic;

namespace FishstrapV2.Core;

/// <summary>
/// Fishstrap-style allowlist: when flag restriction is enabled, only flags
/// present in this set may be applied to the client.
/// </summary>
public static class FlagAllowlist
{
    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        // Graphics backends
        "FFlagDebugGraphicsPreferD3D11",
        "FFlagDebugGraphicsPreferD3D11FL10",
        "FFlagDebugGraphicsPreferOpenGL",
        "FFlagDebugGraphicsPreferVulkan",
        "FFlagDebugGraphicsDisableDirect3D9",
        "FFlagDebugGraphicsDisableDirect3D11",

        // Frame rate
        "FFlagTaskSchedulerLimitTargetFpsTo2402",
        "DFIntTaskSchedulerTargetFps",

        // Lighting technology
        "DFFlagDebugRenderForceTechnologyVoxel",
        "DFFlagDebugRenderForceTechnologyShadowMap",
        "DFFlagDebugRenderForceTechnologyFuture",
        "FFlagDebugForceFutureIsBrightPhase2",
        "FFlagDebugForceFutureIsBrightPhase3",
        "FFlagDebugForceFutureIsBrightPhase4",

        // Quality / effects
        "FFlagDisablePostFx",
        "DFFlagDebugPauseVoxelizer",
        "FFlagDebugSkyGray",
        "DFIntMaxFrameBufferSize",
        "FIntFRMMaxGrassDistance",
        "FIntFRMMinGrassDistance",
        "FIntTerrainArraySliceSize",
        "DFIntS2PhysicsSenderRate",

        // Telemetry
        "FFlagDebugDisableTelemetryEphemeralCounter",
        "FFlagDebugDisableTelemetryEphemeralStatistic",
        "FFlagDebugDisableTelemetryEventIngest",
        "FFlagDebugDisableTelemetryPoint",
        "FFlagDebugDisableTelemetryV2Counter",
        "FFlagDebugDisableTelemetryV2Event",
        "FFlagDebugDisableTelemetryV2Stat",

        // UI / misc
        "DFIntCanHideGuiGroupId",
        "FFlagDisableNewIGMinDUA",
        "FFlagEnableInGameMenuChrome",
        "FFlagEnableInGameMenuChromeABTest",
        "FFlagEnableInGameMenuControls",
        "FIntCameraMaxZoomDistance",
        "FFlagUserShowGuiHideToggles",
        "FFlagFixGraphicsQuality",
        "FFlagHandleAltitudeTrackingPivotChange",
        "DFIntTimestepArbiterThresholdCFLThousandth",
        "FFlagGraphicsGLTextureReduction",
        "FFlagNewLightAttenuation",
    };

    public static bool IsAllowed(string name) => Allowed.Contains(name);
}
