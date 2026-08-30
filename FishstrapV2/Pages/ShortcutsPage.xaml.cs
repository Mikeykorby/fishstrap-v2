using System.Windows;
using System.Windows.Controls;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public partial class ShortcutsPage : FishstrapPage
{
    private bool _suppress;

    public ShortcutsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnShown();
    }

    public override void OnShown()
    {
        _suppress = true;
        var s = SettingsStore.Settings.Shortcuts;
        ChkDesktop.IsChecked = s.DesktopShortcut;
        ChkStartMenu.IsChecked = s.StartMenuShortcut;
        ChkOnInstall.IsChecked = s.CreateOnInstall;
        ChkSettings.IsChecked = s.SettingsShortcut;
        ChkLastGame.IsChecked = s.JoinLastServerShortcut;
        _suppress = false;

        var existing = ShortcutManager.ExistingShortcuts();
        ExistingInfo.Text = existing.Length == 0
            ? "No Fishstrap V2 shortcuts exist right now."
            : $"{existing.Length} shortcut(s) currently exist on this system.";
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        var s = SettingsStore.Settings.Shortcuts;
        s.DesktopShortcut = ChkDesktop.IsChecked == true;
        s.StartMenuShortcut = ChkStartMenu.IsChecked == true;
        s.CreateOnInstall = ChkOnInstall.IsChecked == true;
        s.SettingsShortcut = ChkSettings.IsChecked == true;
        s.JoinLastServerShortcut = ChkLastGame.IsChecked == true;
        Persist();
    }

    private void BtnCreate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShortcutManager.CreateAppShortcuts();
            var count = ShortcutManager.ExistingShortcuts().Length;
            MainWindow.Current?.ShowToast($"Shortcuts created ({count} total)");
            OnShown();
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Shortcut creation failed: " + ex.Message, true);
        }
    }

    private void BtnRemove_Click(object sender, RoutedEventArgs e)
    {
        ShortcutManager.RemoveShortcuts();
        MainWindow.Current?.ShowToast("Fishstrap V2 shortcuts removed");
        OnShown();
    }
}
