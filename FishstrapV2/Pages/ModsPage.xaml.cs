using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public class ModFileRow
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
}

public partial class ModsPage : FishstrapPage
{
    public ModsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnShown();
    }

    public override void OnShown()
    {
        ChkCursor.IsChecked = SettingsStore.Settings.Mods.Cursor.Enabled;
        ChkSounds.IsChecked = SettingsStore.Settings.Mods.Sounds.Enabled;
        ChkFonts.IsChecked = SettingsStore.Settings.Mods.Fonts.Enabled;
        RefreshLists();
    }

    private void RefreshLists()
    {
        FillList(ListCursor, ModManager.CursorDir, "Cursor", SettingsStore.Settings.Mods.Cursor);
        FillList(ListSounds, ModManager.SoundsDir, "Sounds", SettingsStore.Settings.Mods.Sounds);
        FillList(ListFonts, ModManager.FontsDir, "Fonts", SettingsStore.Settings.Mods.Fonts);
    }

    private void FillList(ItemsControl list, string dir, string category, ModCategorySettings cat)
    {
        var files = ModManager.ListFiles(cat, dir);
        list.ItemsSource = files.Select(name => new ModFileRow { Name = name, Category = category }).ToList();
        list.Visibility = files.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnModDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not ModFileRow row) return;
        var cat = row.Category switch
        {
            "Cursor" => SettingsStore.Settings.Mods.Cursor,
            "Sounds" => SettingsStore.Settings.Mods.Sounds,
            _ => SettingsStore.Settings.Mods.Fonts,
        };
        var dir = ModManager.CategoryDir(row.Category);
        ModManager.RemoveFile(cat, dir, row.Name);
        RefreshLists();
    }

    private void Cursor_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SettingsStore.Settings.Mods.Cursor.Enabled = ChkCursor.IsChecked == true;
        Persist();
    }

    private void Sounds_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SettingsStore.Settings.Mods.Sounds.Enabled = ChkSounds.IsChecked == true;
        Persist();
    }

    private void Fonts_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        SettingsStore.Settings.Mods.Fonts.Enabled = ChkFonts.IsChecked == true;
        Persist();
    }

    private void Import(string category, ModCategorySettings cat, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"Import {category} mods",
            Filter = filter,
            Multiselect = true,
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            ModManager.ImportFiles(cat, ModManager.CategoryDir(category), dialog.FileNames);
            RefreshLists();
            MainWindow.Current?.ShowToast($"Imported {dialog.FileNames.Length} file(s)");
        }
        catch (Exception ex)
        {
            MainWindow.Current?.ShowToast("Import failed: " + ex.Message, true);
        }
    }

    private void BtnImportCursor_Click(object sender, RoutedEventArgs e) =>
        Import("Cursor", SettingsStore.Settings.Mods.Cursor, "Cursor files|*.cur;*.ani;*.png;*.dds|All files|*.*");

    private void BtnImportSounds_Click(object sender, RoutedEventArgs e) =>
        Import("Sounds", SettingsStore.Settings.Mods.Sounds, "Sound files|*.wav;*.mp3;*.ogg|All files|*.*");

    private void BtnImportFonts_Click(object sender, RoutedEventArgs e) =>
        Import("Fonts", SettingsStore.Settings.Mods.Fonts, "Font files|*.ttf;*.otf|All files|*.*");

    private void Clear(string category, ModCategorySettings cat)
    {
        ModManager.ClearCategory(cat, ModManager.CategoryDir(category));
        RefreshLists();
        MainWindow.Current?.ShowToast($"{category} mods cleared");
    }

    private void BtnClearCursor_Click(object sender, RoutedEventArgs e) =>
        Clear("Cursor", SettingsStore.Settings.Mods.Cursor);

    private void BtnClearSounds_Click(object sender, RoutedEventArgs e) =>
        Clear("Sounds", SettingsStore.Settings.Mods.Sounds);

    private void BtnClearFonts_Click(object sender, RoutedEventArgs e) =>
        Clear("Fonts", SettingsStore.Settings.Mods.Fonts);

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        var active = RobloxInstallManager.GetActiveVersion("Player");
        if (active is null)
        {
            MainWindow.Current?.ShowToast("Roblox is not installed yet.", true);
            return;
        }
        ModManager.ApplyAll(active.DirectoryPath);
        MainWindow.Current?.ShowToast("Mods applied to the active Roblox version");
    }

    private void BtnClean_Click(object sender, RoutedEventArgs e)
    {
        var active = RobloxInstallManager.GetActiveVersion("Player");
        if (active is null)
        {
            MainWindow.Current?.ShowToast("Roblox is not installed yet.", true);
            return;
        }
        ModManager.RemoveApplied(active.DirectoryPath);
        MainWindow.Current?.ShowToast("Applied mods removed from the active version");
    }
}
