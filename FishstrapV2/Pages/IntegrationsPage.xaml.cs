using System.Windows;
using System.Windows.Controls;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public partial class IntegrationsPage : FishstrapPage
{
    public IntegrationsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnShown();
    }

    public override void OnShown()
    {
        var s = SettingsStore.Settings;

        ChkRpc.IsChecked = s.Integrations.DiscordRpc.Enabled;
        ChkElapsed.IsChecked = s.Integrations.DiscordRpc.ShowElapsedTime;
        TxtDetails.Text = s.Integrations.DiscordRpc.Details;
        TxtState.Text = s.Integrations.DiscordRpc.State;
        ChkActivity.IsChecked = s.Integrations.ActivityTracking;
        RpcPanel.Opacity = s.Integrations.DiscordRpc.Enabled ? 1 : 0.55;
        UpdatePreview();
    }

    private void Rpc_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        var enabled = ChkRpc.IsChecked == true;
        SettingsStore.Settings.Integrations.DiscordRpc.Enabled = enabled;
        RpcPanel.Opacity = enabled ? 1 : 0.55;
        Persist();
        DiscordRpc.SetEnabled(enabled);
    }

    private void Elapsed_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SettingsStore.Settings.Integrations.DiscordRpc.ShowElapsedTime = ChkElapsed.IsChecked == true;
        Persist();
    }

    private void Presence_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        SettingsStore.Settings.Integrations.DiscordRpc.Details = TxtDetails.Text;
        SettingsStore.Settings.Integrations.DiscordRpc.State = TxtState.Text;
        Persist();
        UpdatePreview();
    }

    private void Activity_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SettingsStore.Settings.Integrations.ActivityTracking = ChkActivity.IsChecked == true;
        Persist();
    }

    private void UpdatePreview()
    {
        var rpc = SettingsStore.Settings.Integrations.DiscordRpc;
        PreviewDetails.Text = (rpc.Details.Length > 0 ? rpc.Details : "Playing {game}").Replace("{game}", "Crossroads");
        PreviewState.Text = (rpc.State.Length > 0 ? rpc.State : "via Fishstrap V2").Replace("{game}", "Crossroads");
        PreviewTimer.Visibility = rpc.ShowElapsedTime ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        var placeId = TxtPlaceId.Text.Trim();
        if (!long.TryParse(placeId, out _) || placeId.Length == 0)
        {
            InviteStatus.Text = "Enter a valid place ID (numbers only).";
            return;
        }

        var url = AppInfo.InviteBase + placeId;
        try
        {
            Clipboard.SetText(url);
            InviteStatus.Text = "Invite link copied to clipboard.";
        }
        catch
        {
            InviteStatus.Text = url;
        }
    }
}
