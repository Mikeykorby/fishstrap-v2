using FishstrapV2.UI;

namespace FishstrapV2.Core;

/// <summary>
/// Runs long bootstrap operations (Roblox installs/updates) under the bootstrapper dialog,
/// honoring the user's chosen bootstrapper style: Fishstrap (default), a Bloxnified/imported
/// theme, or Disabled (runs silently with log-only progress).
/// </summary>
public static class Bootstrapper
{
    public static bool IsEnabled => SettingsStore.Settings.Launcher.BootstrapperStyle is not "Disabled";

    /// <summary>
    /// Runs <paramref name="work"/> with an <see cref="InstallProgress"/> reporter, showing the
    /// bootstrapper dialog when enabled. Progress always flows to the logger.
    /// </summary>
    public static Task<T> RunAsync<T>(string initialMessage, Func<IProgress<string>, CancellationToken, Task<T>> work)
    {
        if (!IsEnabled)
        {
            var progress = new InstallProgress();
            return work(progress, CancellationToken.None);
        }

        return BootstrapperDialog.ShowProgressAsync(initialMessage, (dialog, ct) =>
        {
            var progress = new InstallProgress { Dialog = dialog };
            return work(progress, ct);
        });
    }

    /// <summary>Convenience overload for operations that produce no result.</summary>
    public static Task RunAsync(string initialMessage, Func<IProgress<string>, CancellationToken, Task> work) =>
        RunAsync<object?>(initialMessage, async (p, ct) => { await work(p, ct); return null; });
}
