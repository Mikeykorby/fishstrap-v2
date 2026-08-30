using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public partial class AboutPage : FishstrapPage
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnShown();
    }

    public override void OnShown()
    {
        LogoDark.Visibility = ThemeManager.IsLight ? Visibility.Collapsed : Visibility.Visible;
        LogoLight.Visibility = ThemeManager.IsLight ? Visibility.Visible : Visibility.Collapsed;
        VersionText.Text = $"Version {AppInfo.Version}";
        ChannelText.Text = SettingsStore.Settings.Deployment.Channel;
        UpdateStatus.Text = "";
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Could not open link: " + ex.Message, true);
        }
    }

    private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        BtnCheckUpdates.IsEnabled = false;
        UpdateStatus.Text = "Checking for updates…";
        var result = await UpdaterService.CheckAsync();
        BtnCheckUpdates.IsEnabled = true;

        if (result.Success && result.UpdateAvailable)
        {
            UpdateStatus.Text = $"Fishstrap V2 {result.LatestVersion} is available (you have {AppInfo.Version}).";
            OpenUrl(result.Url.Length > 0 ? result.Url : AppInfo.RepoUrl);
        }
        else if (result.Success)
        {
            UpdateStatus.Text = $"You're running the latest version ({AppInfo.Version}).";
        }
        else
        {
            UpdateStatus.Text = "Update check failed: " + (result.Message.Length > 0 ? result.Message : "unknown error");
        }
    }

    private void BtnGitHub_Click(object sender, RoutedEventArgs e) => OpenUrl(AppInfo.RepoUrl);
    private void BtnIssues_Click(object sender, RoutedEventArgs e) => OpenUrl(AppInfo.IssuesUrl);
    private void BtnWebsite_Click(object sender, RoutedEventArgs e) => OpenUrl(AppInfo.WebsiteUrl);

    private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Paths.EnsureDirectories();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{Paths.LogsDir}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Could not open logs: " + ex.Message, true);
        }
    }
}
