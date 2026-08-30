using System.Threading;
using System.Windows;
using FishstrapV2.UI;

namespace FishstrapV2;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
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

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Core.Logger.Info("Fishstrap V2 shutting down");
        base.OnExit(e);
    }
}
