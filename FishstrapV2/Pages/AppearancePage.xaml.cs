using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public partial class AppearancePage : FishstrapPage
{
    private bool _suppress;

    public AppearancePage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnShown();
    }

    public override void OnShown()
    {
        _suppress = true;
        var s = SettingsStore.Settings.Appearance;

        CmbTheme.SelectedIndex = s.Theme switch
        {
            "Light" => 1,
            "System" => 2,
            _ => 0,
        };
        TxtAccentHex.Text = s.Accent;
        _suppress = false;

        LoadIconPreview();
    }

    private void LoadIconPreview()
    {
        var iconPath = SettingsStore.Settings.Appearance.CustomIcon;
        if (File.Exists(iconPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(iconPath);
                bmp.EndInit();
                IconImage.Source = bmp;
                IconPreview.Background = System.Windows.Media.Brushes.Transparent;
            }
            catch
            {
                IconImage.Source = null;
                IconPreview.Background = (System.Windows.Media.Brush)Application.Current.Resources["BrushAccent"];
            }
        }
        else
        {
            IconImage.Source = null;
            IconPreview.Background = (System.Windows.Media.Brush)Application.Current.Resources["BrushAccent"];
        }
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress || CmbTheme.SelectedItem is not ComboBoxItem item) return;
        var theme = item.Content as string ?? "Dark";
        SettingsStore.Settings.Appearance.Theme = theme;
        Persist();
        ThemeManager.ApplyTheme(theme);
    }

    private void Swatch_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string hex) return;
        ApplyAccent(hex);
    }

    private void BtnAccentApply_Click(object sender, RoutedEventArgs e)
    {
        ApplyAccent(TxtAccentHex.Text.Trim());
    }

    private void ApplyAccent(string hex)
    {
        try
        {
            var converted = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            SettingsStore.Settings.Appearance.Accent = hex;
            Persist();
            ThemeManager.ApplyAccent(hex);
            TxtAccentHex.Text = hex;
            MainWindow.Current?.ShowToast("Accent color updated");
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Invalid color: " + ex.Message, true);
        }
    }

    private void BtnIconPick_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg;*.ico|All files|*.*",
            Title = "Choose an app icon",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            Paths.EnsureDirectories();
            File.Copy(dialog.FileName, Paths.CustomIconFile, true);
            SettingsStore.Settings.Appearance.CustomIcon = Paths.CustomIconFile;
            Persist();
            LoadIconPreview();
            MainWindow.Current?.ShowToast("Custom icon applied");
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Could not copy image: " + ex.Message, true);
        }
    }

    private void BtnIconReset_Click(object sender, RoutedEventArgs e)
    {
        try { if (File.Exists(Paths.CustomIconFile)) File.Delete(Paths.CustomIconFile); } catch { }
        SettingsStore.Settings.Appearance.CustomIcon = "";
        Persist();
        LoadIconPreview();
    }
}
