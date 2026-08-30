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
        var s = SettingsStore.Settings;

        CmbTheme.SelectedIndex = s.Appearance.Theme switch
        {
            "Light" => 1,
            "System" => 2,
            _ => 0,
        };
        TxtAccentHex.Text = s.Appearance.Accent;

        LoadBootstrapperStyles(s.Launcher.BootstrapperStyle);
        CmbBootstrapperAnimation.SelectedItem = CmbBootstrapperAnimation.Items
            .Cast<ComboBoxItem>()
            .FirstOrDefault(i => i.Content as string == s.Launcher.BootstrapperAnimation)
            ?? CmbBootstrapperAnimation.Items[0];
        UpdateLogoPickerVisibility();
        TxtBootstrapperTitle.Text = s.Launcher.BootstrapperTitle;

        _suppress = false;

        LoadIconPreview();
    }

    private void LoadBootstrapperStyles(string selected)
    {
        CmbBootstrapperStyle.Items.Clear();
        foreach (var name in new[] { "Fishstrap", "Disabled" }
                     .Concat(Bloxnified.All.Select(t => t.Name))
                     .Concat(Bloxnified.UserThemeNames()))
        {
            CmbBootstrapperStyle.Items.Add(new ComboBoxItem { Content = name });
        }
        CmbBootstrapperStyle.SelectedItem = CmbBootstrapperStyle.Items
            .Cast<ComboBoxItem>()
            .FirstOrDefault(i => i.Content as string == selected)
            ?? CmbBootstrapperStyle.Items[0];
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

    private void BootstrapperStyle_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress || CmbBootstrapperStyle.SelectedItem is not ComboBoxItem item) return;
        SettingsStore.Settings.Launcher.BootstrapperStyle = item.Content as string ?? "Fishstrap";
        Persist();
    }

    private void BootstrapperAnimation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress || CmbBootstrapperAnimation.SelectedItem is not ComboBoxItem item) return;
        SettingsStore.Settings.Launcher.BootstrapperAnimation = item.Content as string ?? "None";
        UpdateLogoPickerVisibility();
        Persist();
    }

    private void UpdateLogoPickerVisibility() =>
        BtnPickLogo.Visibility =
            (CmbBootstrapperAnimation.SelectedItem as ComboBoxItem)?.Content as string == "Custom"
                ? Visibility.Visible
                : Visibility.Collapsed;

    private void BtnPickLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files|*.*",
            Title = "Choose bootstrapper logo",
        };
        if (dialog.ShowDialog() != true) return;

        SettingsStore.Settings.Launcher.BootstrapperIconFile = dialog.FileName;
        Persist();
        MainWindow.Current?.ShowToast("Custom bootstrapper logo set");
    }

    private void BtnImportTheme_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Bootstrapper theme|Theme.xml",
            Title = "Import a Bloxstrap-style theme",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var sourceDir = Path.GetDirectoryName(dialog.FileName)!;
            var target = Path.Combine(Paths.BootstrappersDir, Path.GetFileName(sourceDir)!);
            Paths.EnsureDirectories();
            if (Directory.Exists(target))
                target += "-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            CopyDirectory(sourceDir, target);

            LoadBootstrapperStyles(SettingsStore.Settings.Launcher.BootstrapperStyle);
            MainWindow.Current?.ShowToast("Theme imported: " + Path.GetFileName(target));
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Import failed: " + ex.Message, true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)!));
    }

    private void BootstrapperTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        SettingsStore.Settings.Launcher.BootstrapperTitle = TxtBootstrapperTitle.Text.Trim();
        Persist();
    }

    private async void BtnPreviewBootstrapper_Click(object sender, RoutedEventArgs e)
    {
        BtnPreviewBootstrapper.IsEnabled = false;
        try
        {
            await Bootstrapper.RunAsync("Upgrading Roblox...", async (p, ct) =>
            {
                for (var i = 0; i <= 100 && !ct.IsCancellationRequested; i += 10)
                {
                    p.Report(i >= 100 ? "Applying modifications..." : $"Upgrading Roblox... {i}%");
                    await Task.Delay(140, ct);
                }
                p.Report("Starting Roblox...");
                await Task.Delay(500, ct);
                return (object?)null;
            });
        }
        catch (OperationCanceledException)
        {
            // preview cancelled — nothing to clean up
        }
        catch (Exception ex)
        {
            Logger.Warn("Bootstrapper preview failed: " + ex.Message);
        }
        finally
        {
            BtnPreviewBootstrapper.IsEnabled = true;
        }
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
