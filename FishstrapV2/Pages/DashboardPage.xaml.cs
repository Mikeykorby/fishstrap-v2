using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FishstrapV2.Core;
using FishstrapV2.UI;
using FishstrapV2.UI.Controls;

namespace FishstrapV2.Pages;

public partial class DashboardPage : FishstrapPage
{
    private static readonly Brush SuccessBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
    private static readonly Brush DangerBrush = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));

    public DashboardPage()
    {
        InitializeComponent();
        SettingsStore.Changed += OnSettingsChanged;
        Loaded += (_, _) => OnShown();
        Unloaded += (_, _) => SettingsStore.Changed -= OnSettingsChanged;

        LaunchPlayer.LaunchClick += (_, _) => LaunchPlayerGame();
        LaunchStudio.LaunchClick += (_, _) => LaunchStudioApp();

        LinkIntegrations.RowClick += (_, _) => MainWindow.Current?.NavigateTo("Integrations");
        LinkMods.RowClick += (_, _) => MainWindow.Current?.NavigateTo("Mods");
        LinkFlags.RowClick += (_, _) => MainWindow.Current?.NavigateTo("FastFlags");
        LinkAppearance.RowClick += (_, _) => MainWindow.Current?.NavigateTo("Appearance");
        LinkDeployment.RowClick += (_, _) => MainWindow.Current?.NavigateTo("Deployment");
    }

    public override void OnShown()
    {
        RefreshStatic();
        _ = LoadRobloxInfoAsync();
    }

    private void OnSettingsChanged()
    {
        Dispatcher.Invoke(RefreshStatic);
    }

    private void RefreshStatic()
    {
        var s = SettingsStore.Settings;

        VersionText.Text = $"Version {AppInfo.Version}";
        ChannelLink.Text = s.Deployment.Channel;
        CardChannel.Value = s.Deployment.Channel;

        ChkActivity.IsChecked = s.Integrations.ActivityTracking;
        ChkRpc.IsChecked = s.Integrations.DiscordRpc.Enabled;
        ChkFlags.IsChecked = s.FastFlags.Enabled;

        var stats = StatisticsStore.Data;
        StatLaunches.Value = stats.TotalLaunches.ToString("N0");
        StatPlaytime.Value = StatisticsStore.FormatDuration(stats.TotalPlaySeconds);
        StatWeek.Value = $"{StatisticsStore.GetLaunchesSince(7)} launches";

        var entry = RobloxInstallManager.GetActiveVersion("Player");
        CardInstalled.Value = entry is null ? "Not installed" : entry.Installed.ToString("MMM dd, yyyy");
        CardStatus.Value = entry is null ? "Not installed" : "Ready";
        CardStatus.ValueBrush = entry is null ? DangerBrush : SuccessBrush;
    }

    private async Task LoadRobloxInfoAsync()
    {
        CardVersion.Value = "Checking…";
        var channel = SettingsStore.Settings.Deployment.Channel;
        var info = await RobloxDeployClient.GetLatestVersionAsync(channel, "WindowsPlayer");
        CardVersion.Value = info is null ? "Unavailable" : info.Version;
        if (info is not null)
            CardVersion.ValueBrush = TryFindResource("BrushTextPrimary") as Brush;
    }

    private void QuickSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var s = SettingsStore.Settings;
        s.Integrations.ActivityTracking = ChkActivity.IsChecked == true;
        var rpcWas = s.Integrations.DiscordRpc.Enabled;
        s.Integrations.DiscordRpc.Enabled = ChkRpc.IsChecked == true;
        s.FastFlags.Enabled = ChkFlags.IsChecked == true;
        Persist();

        if (rpcWas != s.Integrations.DiscordRpc.Enabled)
            DiscordRpc.SetEnabled(s.Integrations.DiscordRpc.Enabled);
    }

    private async void LaunchPlayerGame()
    {
        try
        {
            MainWindow.Current?.ShowToast("Launching Roblox Player…");
            await LaunchManager.LaunchPlayerAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Quick launch failed", ex);
            MainWindow.Current?.ShowToast("Launch failed: " + ex.Message, true);
        }
    }

    private void LaunchStudioApp()
    {
        try
        {
            LaunchManager.LaunchStudio();
            MainWindow.Current?.ShowToast("Launching Roblox Studio…");
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Launch failed: " + ex.Message, true);
        }
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshStatic();
        _ = LoadRobloxInfoAsync();
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        BtnUpdate.IsEnabled = false;
        var result = await UpdaterService.CheckAsync();
        BtnUpdate.IsEnabled = true;

        if (MainWindow.Current is null) return;

        if (result.Success && result.UpdateAvailable)
            MainWindow.Current.ShowToast($"Update available: Fishstrap V2 {result.LatestVersion}");
        else if (result.Success)
            MainWindow.Current.ShowToast($"You're up to date ({AppInfo.Version})");
        else
            MainWindow.Current.ShowToast("Update check failed: " + result.Message, true);
    }
}
