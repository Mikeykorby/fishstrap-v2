using System.Diagnostics;
using System.Threading;

namespace FishstrapV2.Core;

/// <summary>
/// Multi-instance launching: pre-creates Roblox's singleton kernel objects so more than one
/// client can run at once. The launcher ensures the named mutex/event exist and starts a
/// detached watcher process that holds the mutex until every Roblox client has exited.
/// Mechanism ported from Bloxstrap/Froststrap's MultiInstanceWatcher.
/// </summary>
public static class MultiInstanceWatcher
{
    private const string SingletonMutexName = "ROBLOX_singletonMutex";
    private const string SingletonEventName = "ROBLOX_singletonEvent";
    private const string InitEventName = "Bloxstrap-MultiInstanceWatcherInitialisationFinished";

    // Held for the app's lifetime so the kernel objects outlive the launch that created them.
    private static Mutex? _singletonMutex;
    private static Mutex? _singletonEvent;

    /// <summary>
    /// Called before starting the player: ensures Roblox's singleton objects exist and spawns
    /// the watcher that keeps them alive while clients run. Best-effort — a failure only means
    /// the next launch behaves single-instance; it never blocks the launch.
    /// </summary>
    public static void PrepareForLaunch()
    {
        try
        {
            // Already exists (another client/watcher owns it) — nothing to create or hold.
            _singletonMutex = EnsureNamedMutex(SingletonMutexName);
            _singletonEvent = EnsureNamedMutex(SingletonEventName);

            using var initEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InitEventName);

            Process.Start(new ProcessStartInfo
            {
                FileName = AppInfo.ExePath,
                Arguments = "-multiinstancewatcher",
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            initEvent.WaitOne(TimeSpan.FromSeconds(2));
            Logger.Info("Multi-instance watcher started");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Could not start multi-instance watcher: {ex.Message}");
        }
    }

    /// <summary>Watcher-process entry point: holds the singleton mutex until all clients exit.</summary>
    public static void RunWatcher()
    {
        try
        {
            using var mutex = new Mutex(false, SingletonMutexName);
            bool acquired;
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // Previous owner died without releasing — the mutex is ours now.
                acquired = true;
            }

            using var initEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InitEventName);

            if (!acquired)
            {
                Logger.Info("Roblox singleton mutex already owned — no watcher needed");
                initEvent.Set();
                return;
            }

            Logger.Info("Holding Roblox singleton mutex for multi-instance launching");
            initEvent.Set();

            // Stay alive while any Roblox client is running, then release the singleton.
            int count;
            do
            {
                Thread.Sleep(2500);
                count = GetClientCount();
            } while (count == -1 || count > 0);

            Logger.Info("All Roblox clients closed — releasing singleton mutex");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Multi-instance watcher stopped: {ex.Message}");
        }
        finally
        {
            Environment.Exit(0);
        }
    }

    private static Mutex? EnsureNamedMutex(string name)
    {
        if (Mutex.TryOpenExisting(name, out _))
            return null;
        return new Mutex(false, name);
    }

    private static int GetClientCount()
    {
        try
        {
            return Process.GetProcessesByName("RobloxPlayerBeta").Length;
        }
        catch (Exception ex)
        {
            // Process APIs can fail at any time; -1 makes the poll loop retry.
            Logger.Warn($"Could not count Roblox processes: {ex.Message}");
            return -1;
        }
    }
}
