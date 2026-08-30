#nullable enable
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public class FlagRow : INotifyPropertyChanged
{
    private string _name = "";
    private string _value = "";

    public string Name
    {
        get => _name;
        set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class FastFlagsPage : FishstrapPage
{
    private bool _suppress;

    public FastFlagsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnShown();
    }

    public override void OnShown()
    {
        _suppress = true;
        ChkEnforce.IsChecked = SettingsStore.Settings.FastFlags.EnforceAllowlist;
        ChkFlagsEnabled.IsChecked = SettingsStore.Settings.FastFlags.Enabled;
        CmbPresets.SelectedIndex = 0;
        _suppress = false;
        Reload();
    }

    private void Reload()
    {
        var flags = SettingsStore.Settings.FastFlags.Flags;
        FlagsList.ItemsSource = flags
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new FlagRow { Name = kvp.Key, Value = FastFlagManager.ValueToString(kvp.Value) })
            .ToList();
        NoFlags.Visibility = flags.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FlagCount.Text = $"{flags.Count} flag(s) configured";
    }

    private void SyncFromRows()
    {
        if (FlagsList.ItemsSource is not IEnumerable<FlagRow> rows) return;
        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var name = row.Name.Trim();
            if (name.Length == 0) continue;
            dict[name] = FastFlagManager.ParseValue(row.Value);
        }
        SettingsStore.Settings.FastFlags.Flags = dict;
        Persist();
        NoFlags.Visibility = dict.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FlagCount.Text = $"{dict.Count} flag(s) configured";
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = TxtSearch.Text.Trim();
        if (query.Length == 0) { Reload(); return; }

        var flags = SettingsStore.Settings.FastFlags.Flags;
        FlagsList.ItemsSource = flags
            .Where(k => k.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new FlagRow { Name = kvp.Key, Value = FastFlagManager.ValueToString(kvp.Value) })
            .ToList();
    }

    private void FlagRow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SyncFromRows();
            e.Handled = true;
        }
    }

    private void FlagName_LostFocus(object sender, RoutedEventArgs e) => SyncFromRows();
    private void FlagValue_LostFocus(object sender, RoutedEventArgs e) => SyncFromRows();

    private void FlagDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not FlagRow row) return;
        SettingsStore.Settings.FastFlags.Flags.Remove(row.Name.Trim());
        Persist();
        Reload();
    }

    private void FlagsEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        SettingsStore.Settings.FastFlags.Enabled = ChkFlagsEnabled.IsChecked == true;
        Persist();
    }

    private void Enforce_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppress) return;
        SettingsStore.Settings.FastFlags.EnforceAllowlist = ChkEnforce.IsChecked == true;
        AllowlistBanner.Opacity = ChkEnforce.IsChecked == true ? 1 : 0.45;
        Persist();
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var flags = SettingsStore.Settings.FastFlags.Flags;
        if (flags.ContainsKey("NewFlag"))
        {
            var n = 2;
            while (flags.ContainsKey($"NewFlag{n}")) n++;
            flags[$"NewFlag{n}"] = FastFlagManager.ParseValue("True");
        }
        else
        {
            flags["NewFlag"] = FastFlagManager.ParseValue("True");
        }
        Persist();
        Reload();
        TxtSearch.Text = "";

        var last = FlagsList.ItemsSource?.Cast<FlagRow>().LastOrDefault();
        if (last is not null)
        {
            FlagsList.UpdateLayout();
            if (FlagsList.ItemContainerGenerator.ContainerFromItem(last) is ContentPresenter presenter)
            {
                var box = FindVisualChild<TextBox>(presenter);
                box?.Focus();
                box?.SelectAll();
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var deeper = FindVisualChild<T>(child);
            if (deeper is not null) return deeper;
        }
        return null;
    }

    private void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppress || CmbPresets.SelectedItem is not ComboBoxItem item) return;
        var preset = item.Content as string;

        var flags = SettingsStore.Settings.FastFlags.Flags;
        switch (preset)
        {
            case "Unlock FPS (240)":
                flags["FFlagTaskSchedulerLimitTargetFpsTo2402"] = FastFlagManager.ParseValue("True");
                flags["DFIntTaskSchedulerTargetFps"] = FastFlagManager.ParseValue("240");
                break;
            case "Reduce effects":
                flags["FFlagDisablePostFx"] = FastFlagManager.ParseValue("True");
                flags["DFFlagDebugPauseVoxelizer"] = FastFlagManager.ParseValue("True");
                break;
            case "Force Vulkan":
                flags["FFlagDebugGraphicsPreferVulkan"] = FastFlagManager.ParseValue("True");
                break;
            case "Force Direct3D 11":
                flags["FFlagDebugGraphicsPreferD3D11"] = FastFlagManager.ParseValue("True");
                break;
            case "Block telemetry":
                foreach (var f in new[]
                {
                    "FFlagDebugDisableTelemetryEphemeralCounter",
                    "FFlagDebugDisableTelemetryEphemeralStatistic",
                    "FFlagDebugDisableTelemetryEventIngest",
                    "FFlagDebugDisableTelemetryPoint",
                    "FFlagDebugDisableTelemetryV2Counter",
                    "FFlagDebugDisableTelemetryV2Event",
                    "FFlagDebugDisableTelemetryV2Stat",
                })
                    flags[f] = FastFlagManager.ParseValue("True");
                break;
            default:
                return; // "Presets…" placeholder
        }

        Persist();
        Reload();
        CmbPresets.SelectedIndex = 0;
        MainWindow.Current?.ShowToast($"Preset applied: {preset}");
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsStore.Settings.FastFlags.Flags.Count > 0 &&
            MessageBox.Show("Remove all FastFlags?", "Fishstrap V2",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        SettingsStore.Settings.FastFlags.Flags.Clear();
        Persist();
        Reload();
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "JSON files|*.json|All files|*.*", Title = "Import FastFlags" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            using var doc = JsonDocument.Parse(json);
            var flags = SettingsStore.Settings.FastFlags.Flags;
            var added = 0;
            var blocked = 0;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (SettingsStore.Settings.FastFlags.EnforceAllowlist && !FlagAllowlist.IsAllowed(prop.Name))
                {
                    blocked++;
                    continue;
                }
                flags[prop.Name] = prop.Value.Clone();
                added++;
            }

            Persist();
            Reload();
            MainWindow.Current?.ShowToast(blocked > 0
                ? $"Imported {added} flag(s); {blocked} blocked by the allowlist"
                : $"Imported {added} flag(s)");
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Import failed: " + ex.Message, true);
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON files|*.json",
            FileName = $"fishstrap-flags-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            Title = "Export FastFlags",
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var json = JsonSerializer.Serialize(
                FastFlagManager.BuildEffectiveFlags(SettingsStore.Settings),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
            MainWindow.Current?.ShowToast("FastFlags exported");
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Export failed: " + ex.Message, true);
        }
    }
}
