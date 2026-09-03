using System.Windows;
using System.Windows.Controls;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public partial class BootstrapperPage : FishstrapPage
{
    private bool _suppress;

    public BootstrapperPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnShown();
    }

    public override void OnShown()
    {
        _suppress = true;
        var s = SettingsStore.Settings;

        ChkAutoClose.IsChecked = s.Launcher.AutoCloseAfterLaunch;
        ChkShortcuts.IsChecked = s.Launcher.CreateShortcutsOnInstall;
        ChkRegister.IsChecked = IsRegisteredAsLauncher();
        ChkCrashHandler.IsChecked = s.Launcher.AutoCloseCrashHandler;
        CmbPriority.SelectedIndex = Math.Max(0, PriorityIndex(s.Launcher.ProcessPriority));
        TxtLaunchArgs.Text = s.Launcher.LaunchArgs;
        _suppress = false;
    }

    private static int PriorityIndex(string value) => value.ToLowerInvariant() switch
    {
        "below normal" => 1,
        "low" => 2,
        "above normal" => 3,
        "high" => 4,
        _ => 0,
    };

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        var s = SettingsStore.Settings.Launcher;
        s.AutoCloseAfterLaunch = ChkAutoClose.IsChecked == true;
        s.CreateShortcutsOnInstall = ChkShortcuts.IsChecked == true;
        s.AutoCloseCrashHandler = ChkCrashHandler.IsChecked == true;
        Persist();
    }

    private void Priority_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress || CmbPriority.SelectedItem is not ComboBoxItem item) return;
        SettingsStore.Settings.Launcher.ProcessPriority = (string)item.Content;
        Persist();
        MainWindow.Current?.ShowToast($"Roblox priority set to {item.Content}");
    }

    private void Register_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        ApplyRegistration(ChkRegister.IsChecked == true);
    }

    private static bool IsRegisteredAsLauncher()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Roblox\RobloxPlayerLauncherBeta.exe");
        return key is not null;
    }

    private void Args_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        SettingsStore.Settings.Launcher.LaunchArgs = TxtLaunchArgs.Text;
        Persist();
    }

    private void ApplyRegistration(bool enable)
    {
        try
        {
            using var root = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Roblox");
            if (enable)
            {
                using var player = root.CreateSubKey("RobloxPlayerLauncherBeta.exe");
                player.SetValue("Path", AppInfo.ExePath);
                player.SetValue("Channel", SettingsStore.Settings.Deployment.Channel);
                using var studio = root.CreateSubKey("RobloxStudioLauncherBeta.exe");
                studio.SetValue("Path", AppInfo.ExePath);
                studio.SetValue("Channel", SettingsStore.Settings.Deployment.Channel);
                Logger.Info("Registered Fishstrap V2 as the system Roblox launcher");
                MainWindow.Current?.ShowToast("Fishstrap V2 is now the default Roblox launcher");
            }
            else
            {
                root.DeleteSubKeyTree("RobloxPlayerLauncherBeta.exe", false);
                root.DeleteSubKeyTree("RobloxStudioLauncherBeta.exe", false);
                Logger.Info("Unregistered Fishstrap V2 as the system Roblox launcher");
                MainWindow.Current?.ShowToast("Fishstrap V2 is no longer the default launcher");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Launcher registration failed", ex);
            MainWindow.Current?.ShowToast("Registration failed: " + ex.Message, true);
        }
    }

    private async void BtnSetup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnSetup.IsEnabled = false;
            await Bootstrapper.RunAsync("Installing Roblox…", (p, ct) =>
                RobloxInstallManager.InstallAsync(p, includeStudio: true, forceReinstall: false, ct));
            ProgressText.Text = "Roblox is ready.";
            MainWindow.Current?.ShowToast("Roblox is up to date");
        }
        catch (OperationCanceledException)
        {
            ProgressText.Text = "Setup cancelled.";
            MainWindow.Current?.ShowToast("Setup cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error("Initial setup failed", ex);
            ProgressText.Text = "Setup failed: " + ex.Message;
            MainWindow.Current?.ShowToast("Setup failed: " + ex.Message, true);
        }
        finally
        {
            BtnSetup.IsEnabled = true;
        }
    }
}
