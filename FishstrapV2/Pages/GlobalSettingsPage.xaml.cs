using System.Windows;
using System.Windows.Controls;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public class ProfileRow
{
    public string Name { get; set; } = "";
}

public partial class GlobalSettingsPage : FishstrapPage
{
    private bool _suppress;

    public GlobalSettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnShown();
    }

    public override void OnShown()
    {
        _suppress = true;
        var e = SettingsStore.Settings.Engine;

        ChkFps.IsChecked = e.FpsCapEnabled;
        SldFps.Value = Math.Clamp(e.FpsCapValue, 30, 999);
        FpsValue.Text = SldFps.Value.ToString("0");
        FpsPanel.Opacity = e.FpsCapEnabled ? 1 : 0.55;

        CmbLighting.SelectedIndex = e.Lighting switch
        {
            "Voxel" => 1,
            "ShadowMap" => 2,
            "Future" => 3,
            _ => 0,
        };
        CmbGraphics.SelectedIndex = e.GraphicsMode switch
        {
            "Direct3D 11" => 1,
            "Direct3D 10" => 2,
            "Vulkan" => 3,
            "OpenGL" => 4,
            _ => 0,
        };
        ChkPostFx.IsChecked = e.DisablePostEffects;
        ChkShadows.IsChecked = e.DisablePlayerShadows;
        ChkTelemetry.IsChecked = e.BlockTelemetry;
        _suppress = false;

        RefreshEffectiveCount();
        RefreshProfiles();
    }

    private void Fps_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        SettingsStore.Settings.Engine.FpsCapEnabled = ChkFps.IsChecked == true;
        FpsPanel.Opacity = ChkFps.IsChecked == true ? 1 : 0.55;
        Persist();
        RefreshEffectiveCount();
    }

    private void FpsSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || _suppress) return;
        FpsValue.Text = SldFps.Value.ToString("0");
        SettingsStore.Settings.Engine.FpsCapValue = (int)SldFps.Value;
        Persist();
    }

    private void Lighting_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress || CmbLighting.SelectedItem is not ComboBoxItem item) return;
        SettingsStore.Settings.Engine.Lighting = item.Content as string ?? "Automatic";
        Persist();
        RefreshEffectiveCount();
    }

    private void Graphics_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress || CmbGraphics.SelectedItem is not ComboBoxItem item) return;
        SettingsStore.Settings.Engine.GraphicsMode = item.Content as string ?? "Automatic";
        Persist();
        RefreshEffectiveCount();
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        var engine = SettingsStore.Settings.Engine;
        engine.DisablePostEffects = ChkPostFx.IsChecked == true;
        engine.DisablePlayerShadows = ChkShadows.IsChecked == true;
        engine.BlockTelemetry = ChkTelemetry.IsChecked == true;
        Persist();
        RefreshEffectiveCount();
    }

    private void RefreshEffectiveCount()
    {
        EffectiveCount.Text =
            $"{FastFlagManager.BuildEffectiveFlags(SettingsStore.Settings).Count} FastFlags will be applied on launch";
    }

    private void BtnApplyNow_Click(object sender, RoutedEventArgs e)
    {
        var active = RobloxInstallManager.GetActiveVersion("Player");
        if (active is null)
        {
            MainWindow.Current?.ShowToast("Roblox is not installed yet.", true);
            return;
        }
        FastFlagManager.ApplyToVersion(active.DirectoryPath, SettingsStore.Settings);
        MainWindow.Current?.ShowToast("FastFlags written to the active version");
    }

    private void RefreshProfiles()
    {
        var profiles = ProfileStore.ListProfiles();
        ProfileList.ItemsSource = profiles
            .Select(f => new ProfileRow { Name = System.IO.Path.GetFileNameWithoutExtension(f.Name) })
            .ToList();
        NoProfiles.Visibility = profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtProfileName.Text.Trim();
        if (name.Length == 0)
        {
            MainWindow.Current?.ShowToast("Enter a name for the profile first.", true);
            return;
        }
        ProfileStore.Save(name);
        TxtProfileName.Text = "";
        RefreshProfiles();
        MainWindow.Current?.ShowToast($"Profile '{name}' saved");
    }

    private void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        BackupManager.CreateBackup("manual");
        MainWindow.Current?.ShowToast("Backup created in Backups folder");
    }

    private void ProfileApply_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ProfileRow row) return;
        try
        {
            ProfileStore.Apply(row.Name);
            OnShown();
            MainWindow.Current?.ShowToast($"Profile '{row.Name}' applied");
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Failed to apply profile: " + ex.Message, true);
        }
    }

    private void ProfileDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ProfileRow row) return;
        ProfileStore.Delete(row.Name);
        RefreshProfiles();
        MainWindow.Current?.ShowToast($"Profile '{row.Name}' deleted");
    }
}
