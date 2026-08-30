using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public class VersionRow
{
    public string Hash { get; set; } = "";
    public string Channel { get; set; } = "";
    public string InstalledText { get; set; } = "";
    public string SizeText { get; set; } = "";
    public RobloxVersionEntry Entry { get; set; } = new();
}

public partial class DeploymentPage : FishstrapPage
{
    private bool _suppress;

    public DeploymentPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnShown();
    }

    public override void OnShown()
    {
        _suppress = true;
        var s = SettingsStore.Settings.Deployment;

        CmbChannel.SelectedIndex = s.Channel == "production" ? 0 : s.Channel == "ZIntegration" ? 1 : 2;
        TxtCustomChannel.Text = s.Channel;
        TxtCustomChannel.Visibility = s.Channel is "production" or "ZIntegration" ? Visibility.Collapsed : Visibility.Visible;
        ChkWait.IsChecked = s.WaitForLatest;
        CmbServer.SelectedIndex = s.ServerLocation switch
        {
            "AWS US East (N. Virginia)" => 1,
            "AWS EU (Ireland)" => 2,
            "AWS Asia Pacific (Tokyo)" => 3,
            _ => 0,
        };
        CmbKeep.SelectedIndex = Math.Clamp(s.KeepOldVersions, 0, 5);
        _suppress = false;

        RefreshVersionList();
    }

    private void RefreshVersionList()
    {
        var versions = RobloxInstallManager.GetInstalledVersions();
        VersionList.ItemsSource = versions.Select(v => new VersionRow
        {
            Hash = v.Hash,
            Channel = v.Channel,
            InstalledText = v.Installed.ToString("MMM dd, yyyy HH:mm"),
            SizeText = CacheCleaner.FormatSize(v.SizeBytes),
            Entry = v,
        }).ToList();
        NoVersions.Visibility = versions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Channel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;

        if (CmbChannel.SelectedItem is not ComboBoxItem item) return;
        var value = item.Content as string ?? "production";

        if (value == "Custom…")
        {
            TxtCustomChannel.Visibility = Visibility.Visible;
            TxtCustomChannel.Focus();
            return;
        }

        TxtCustomChannel.Visibility = Visibility.Collapsed;
        SettingsStore.Settings.Deployment.Channel = value;
        Persist();
        _ = LoadLatestAsync();
    }

    private void CustomChannel_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        var value = TxtCustomChannel.Text.Trim();
        if (value.Length == 0) return;
        SettingsStore.Settings.Deployment.Channel = value;
        Persist();
        _ = LoadLatestAsync();
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        SettingsStore.Settings.Deployment.WaitForLatest = ChkWait.IsChecked == true;
        Persist();
    }

    private void Server_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress || CmbServer.SelectedItem is not ComboBoxItem item) return;
        SettingsStore.Settings.Deployment.ServerLocation = item.Content as string ?? "Automatic";
        Persist();
    }

    private void Keep_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress || CmbKeep.SelectedItem is not ComboBoxItem item) return;
        if (int.TryParse(item.Content as string, out var n))
        {
            SettingsStore.Settings.Deployment.KeepOldVersions = n;
            Persist();
        }
    }

    private void SetBusy(bool busy, string? message = null)
    {
        ProgressPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ProgressText.Text = message ?? "";
        BtnCheck.IsEnabled = BtnReinstall.IsEnabled = BtnStudio.IsEnabled = BtnUninstall.IsEnabled = !busy;
    }

    private async Task LoadLatestAsync()
    {
        SetBusy(true, "Checking latest version…");
        var info = await RobloxDeployClient.GetLatestVersionAsync(SettingsStore.Settings.Deployment.Channel, "WindowsPlayer");
        SetBusy(false);
        MainWindow.Current?.ShowToast(info is null
            ? "Could not reach the Roblox deployment API"
            : $"Latest version for '{SettingsStore.Settings.Deployment.Channel}': {info.Version}");
    }

    private async void BtnCheck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Checking for updates…");
            var info = await RobloxDeployClient.GetLatestVersionAsync(SettingsStore.Settings.Deployment.Channel, "WindowsPlayer");
            var active = RobloxInstallManager.GetActiveVersion("Player");

            if (info is null)
                throw new InvalidOperationException("Could not reach the Roblox deployment API.");

            if (active is not null && active.Hash == info.VersionHash)
            {
                SetBusy(false);
                MainWindow.Current?.ShowToast($"Roblox {info.Version} is already up to date");
                return;
            }

            SetBusy(false);
            await Bootstrapper.RunAsync<object?>($"Upgrading Roblox to {info.Version}…", (p, ct) =>
                RobloxInstallManager.InstallAsync(p, includeStudio: false, cancellationToken: ct)
                    .ContinueWith(t => (object?)t.Result, ct));
            RefreshVersionList();
            MainWindow.Current?.ShowToast($"Installed Roblox {info.Version}");
        }
        catch (OperationCanceledException)
        {
            SetBusy(false);
            MainWindow.Current?.ShowToast("Update cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error("Update check failed", ex);
            SetBusy(false);
            MainWindow.Current?.ShowToast("Update failed: " + ex.Message, true);
        }
    }

    private async void BtnReinstall_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(false);
            await Bootstrapper.RunAsync<object?>("Installing Roblox…", (p, ct) =>
                RobloxInstallManager.InstallAsync(p, includeStudio: false, forceReinstall: true, cancellationToken: ct)
                    .ContinueWith(t => (object?)t.Result, ct));
            RefreshVersionList();
            MainWindow.Current?.ShowToast("Reinstalled the current Roblox version");
        }
        catch (OperationCanceledException)
        {
            SetBusy(false);
            MainWindow.Current?.ShowToast("Reinstall cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error("Reinstall failed", ex);
            SetBusy(false);
            MainWindow.Current?.ShowToast("Reinstall failed: " + ex.Message, true);
        }
    }

    private async void BtnStudio_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SetBusy(true, "Downloading Roblox Studio…");
            var info = await RobloxDeployClient.GetLatestVersionAsync(SettingsStore.Settings.Deployment.Channel, "WindowsStudio64")
                       ?? throw new InvalidOperationException("Could not reach the Roblox deployment API.");
            await Bootstrapper.RunAsync("Downloading Roblox Studio…", (p, ct) =>
                RobloxDeployClient.DownloadVersionAsync(
                    info.VersionHash,
                    System.IO.Path.Combine(Paths.VersionsDir, info.VersionHash),
                    p, ct));
            RobloxInstallManager.WriteSidecar(
                System.IO.Path.Combine(Paths.VersionsDir, info.VersionHash), info.VersionHash);
            SetBusy(false);
            RefreshVersionList();
            MainWindow.Current?.ShowToast("Roblox Studio installed");
        }
        catch (OperationCanceledException)
        {
            SetBusy(false);
            MainWindow.Current?.ShowToast("Studio install cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error("Studio install failed", ex);
            SetBusy(false);
            MainWindow.Current?.ShowToast("Studio install failed: " + ex.Message, true);
        }
    }

    private void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Remove every installed Roblox version? You can reinstall at any time.",
                "Fishstrap V2", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        RobloxInstallManager.UninstallAll();
        RobloxInstallManager.UninstallAll();
        RefreshVersionList();
        MainWindow.Current?.ShowToast("All Roblox versions removed");
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not VersionRow row) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{row.Entry.DirectoryPath}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Could not open folder: " + ex.Message, true);
        }
    }

    private void BtnPin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not VersionRow row) return;
        row.Entry.Pinned = !row.Entry.Pinned;
        RobloxInstallManager.WriteSidecar(row.Entry.DirectoryPath, row.Entry.Hash);
        RefreshVersionList();
        MainWindow.Current?.ShowToast(row.Entry.Pinned
            ? "Version pinned — it will not be pruned"
            : "Version unpinned");
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not VersionRow row) return;
        RobloxInstallManager.TryDeleteDirectory(row.Entry.DirectoryPath);
        RefreshVersionList();
        MainWindow.Current?.ShowToast("Version removed");
    }
}
