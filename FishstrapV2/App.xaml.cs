using System.Threading;
using System.Windows;
using FishstrapV2.UI;

namespace FishstrapV2;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        // The multi-instance watcher is a headless process — it must not touch the GUI mutex.
        if (e.Args.Contains("-multiinstancewatcher"))
        {
            Core.MultiInstanceWatcher.RunWatcher();
            return;
        }

        // Protocol launches (website Play button, deep links) run in a transient windowless
        // process so they also work while the main window is already open.
        var url = e.Args.FirstOrDefault(a =>
            a.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith("roblox://", StringComparison.OrdinalIgnoreCase));
        if (url is not null)
        {
            // No main window in a protocol launch — the bootstrapper dialog is enough.
            StartProtocolLaunch(url);
            return;
        }

        bool createdNew;
        _singleInstanceMutex = new Mutex(true, @"Local\FishstrapV2-SingleInstance", out createdNew);

        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "Fishstrap V2 is already running.",
                "Fishstrap V2", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        Core.Logger.Info($"Fishstrap V2 {Core.AppInfo.Version} starting");
        Core.SettingsStore.Load();
        Core.StatisticsStore.Load();
        _ = System.Threading.Tasks.Task.Run(Core.CacheCleaner.RunScheduled);
        Core.SessionWatcher.Start();

        ThemeManager.ApplyAccent(Core.SettingsStore.Settings.Appearance.Accent);
        ThemeManager.ApplyTheme(Core.SettingsStore.Settings.Appearance.Theme);

        DispatcherUnhandledException += (_, args) =>
        {
            Core.Logger.Error("Unhandled UI exception", args.Exception);
            System.Windows.MessageBox.Show(
                "Something went wrong:\n\n" + args.Exception.Message,
                "Fishstrap V2", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Window creation is manual (not StartupUri) so protocol/watcher launches can skip it.
        new MainWindow().Show();

        base.OnStartup(e);
    }

    private void StartProtocolLaunch(string url)
    {
        // The bootstrapper dialog is the only window — without this the app would shut down
        // mid-launch the moment it closes.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Core.Logger.Info("Protocol launch received");
        Core.SettingsStore.Load();
        Core.StatisticsStore.Load();
        ThemeManager.ApplyAccent(Core.SettingsStore.Settings.Appearance.Accent);
        ThemeManager.ApplyTheme(Core.SettingsStore.Settings.Appearance.Theme);

        // The client expects everything after "roblox-player:" as its command line;
        // roblox:// deep links it parses itself.
        var arg = url.StartsWith("roblox-player:", StringComparison.OrdinalIgnoreCase)
            ? url["roblox-player:".Length..]
            : url;

        _ = Task.Run(async () =>
        {
            try
            {
                await Core.LaunchManager.LaunchPlayerAsync(arg);
            }
            catch (Exception ex)
            {
                Core.Logger.Error("Protocol launch failed", ex);
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(Shutdown);
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Core.Logger.Info("Fishstrap V2 shutting down");
        base.OnExit(e);
    }
}
