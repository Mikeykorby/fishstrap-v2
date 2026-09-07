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
        ChkMultiInstance.IsChecked = s.Launcher.MultiInstanceLaunching;
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
        s.MultiInstanceLaunching = ChkMultiInstance.IsChecked == true;
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
        using var delegation = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Roblox\RobloxPlayerLauncherBeta.exe");
        if (delegation is not null) return true;
        using var proto = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Classes\roblox-player\shell\open\command");
        return proto?.GetValue(null) is string cmd && cmd.Contains("FishstrapV2", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetProtocolHandler(Microsoft.Win32.RegistryKey classes, string scheme)
    {
        using var command = classes.CreateSubKey($@"{scheme}\shell\open\command");
        var previous = command.GetValue(null) as string;
        if (string.IsNullOrEmpty(previous) || !previous.Contains("FishstrapV2", StringComparison.OrdinalIgnoreCase))
        {
            using var backup = classes.CreateSubKey(@"FishstrapV2\PrevProtocol");
            backup.SetValue(scheme, previous ?? "");
        }
        command.SetValue(null, $"\"{AppInfo.ExePath}\" \"%1\"");
    }

    private static void RestoreProtocolHandler(Microsoft.Win32.RegistryKey classes, string scheme)
    {
        string? previous;
        using (var backup = classes.OpenSubKey(@"FishstrapV2\PrevProtocol"))
            previous = backup?.GetValue(scheme) as string;

        if (string.IsNullOrEmpty(previous))
        {
            classes.DeleteSubKeyTree(scheme, false); // no prior handler — remove ours entirely
        }
        else
        {
            using var command = classes.CreateSubKey($@"{scheme}\shell\open\command");
            command.SetValue(null, previous);
        }
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
            using var classes = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes");
            if (enable)
            {
                using var player = root.CreateSubKey("RobloxPlayerLauncherBeta.exe");
                player.SetValue("Path", AppInfo.ExePath);
                player.SetValue("Channel", SettingsStore.Settings.Deployment.Channel);
                using var studio = root.CreateSubKey("RobloxStudioLauncherBeta.exe");
                studio.SetValue("Path", AppInfo.ExePath);
                studio.SetValue("Channel", SettingsStore.Settings.Deployment.Channel);

                // Take over the website Play-button and deep-link protocols (the previous
                // handler is backed up so unregistering restores it).
                SetProtocolHandler(classes, "roblox-player");
                SetProtocolHandler(classes, "roblox");
                Logger.Info("Registered Fishstrap V2 as the system Roblox launcher");
                MainWindow.Current?.ShowToast("Fishstrap V2 is now the default Roblox launcher");
            }
            else
            {
                root.DeleteSubKeyTree("RobloxPlayerLauncherBeta.exe", false);
                root.DeleteSubKeyTree("RobloxStudioLauncherBeta.exe", false);
                RestoreProtocolHandler(classes, "roblox-player");
                RestoreProtocolHandler(classes, "roblox");
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
