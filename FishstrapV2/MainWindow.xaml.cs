using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FishstrapV2.Core;
using FishstrapV2.UI;
using FishstrapV2.Pages;
using WS = System.Windows.Shell;

namespace FishstrapV2;

public partial class MainWindow : Window
{
    public static MainWindow? Current { get; private set; }

    private readonly List<NavDefinition> _nav = new();
    private Button? _activeNavButton;
    private DispatcherTimer? _toastTimer;

    public record NavDefinition(string Glyph, string Title, Func<FishstrapPage> CreatePage);

    public MainWindow()
    {
        InitializeComponent();
        Current = this;

        // Fit the window to the user's work area so nothing spawns off-screen.
        var work = SystemParameters.WorkArea;
        Width = Math.Min(1600, work.Width - 24);
        Height = Math.Min(880, work.Height - 24);
        MinWidth = Math.Min(1100, Width);
        MinHeight = Math.Min(640, Height);

        _nav.Add(new("\uE80F", "Dashboard", () => new DashboardPage()));
        _nav.Add(new("\uE710", "Integrations", () => new IntegrationsPage()));
        _nav.Add(new("\uE8EC", "Bootstrapper", () => new BootstrapperPage()));
        _nav.Add(new("\uE753", "Deployment", () => new DeploymentPage()));
        _nav.Add(new("\uE90F", "Mods", () => new ModsPage()));
        _nav.Add(new("\uE7C3", "FastFlags", () => new FastFlagsPage()));
        _nav.Add(new("\uE713", "Global Settings", () => new GlobalSettingsPage()));
        _nav.Add(new("\uE790", "Appearance", () => new AppearancePage()));
        _nav.Add(new("\uE71B", "Shortcuts", () => new ShortcutsPage()));
        _nav.Add(new("\uE787", "Statistics", () => new StatisticsPage()));
        _nav.Add(new("\uE946", "About", () => new AboutPage()));

        Loaded += (_, _) =>
        {
            BuildSidebar();
            Navigate(0);
        };

        StateChanged += (_, _) =>
        {
            MaximizeGlyph.Text =
                WindowState == WindowState.Maximized ? "\uE923" : "\uE922";

            // Borderless windows bleed the resize border off-screen when maximized;
            // pad the content so the command bar and edges stay fully visible.
            RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(8) : new Thickness(0);
        };

        if (SettingsStore.Settings.Integrations.DiscordRpc.Enabled)
            DiscordRpc.SetEnabled(true);
    }

    private void BuildSidebar()
    {
        NavList.Children.Clear();
        for (var i = 0; i < _nav.Count; i++)
        {
            var def = _nav[i];
            var index = i;

            var button = new Button();
            button.SetResourceReference(Button.StyleProperty, "NavButton");
            button.Tag = "";
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var glyph = new TextBlock
            {
                Text = def.Glyph,
                FontSize = 15,
                Width = 30,
                VerticalAlignment = VerticalAlignment.Center,
            };
            glyph.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            var label = new TextBlock
            {
                Text = def.Title,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            content.Children.Add(glyph);
            content.Children.Add(label);
            button.Content = content;

            button.Click += (_, _) => Navigate(index);
            NavList.Children.Add(button);
        }
    }

    public void Navigate(int index)
    {
        if (index < 0 || index >= _nav.Count) return;

        if (_activeNavButton is not null)
        {
            _activeNavButton.Tag = "";
            if (_activeNavButton.Content is StackPanel oldPanel && oldPanel.Children[0] is TextBlock oldGlyph)
                oldGlyph.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
            if (_activeNavButton.Content is StackPanel oldPanel2 && oldPanel2.Children[1] is TextBlock oldLabel)
                oldLabel.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextSecondary");
        }

        var button = (Button)NavList.Children[index];
        button.Tag = "active";
        if (button.Content is StackPanel panel)
        {
            if (panel.Children[0] is TextBlock glyph)
                glyph.SetResourceReference(TextBlock.ForegroundProperty, "BrushAccent");
            if (panel.Children[1] is TextBlock label)
                label.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
        }
        _activeNavButton = button;

        var page = _nav[index].CreatePage();
        PageHost.Content = page;
        AnimateIn(page);
        page.OnShown();
    }

    public void NavigateTo(string title)
    {
        var index = _nav.FindIndex(n => n.Title == title);
        if (index < 0)
            Logger.Warn($"NavigateTo: no page titled '{title}'");
        else
            Navigate(index);
    }

    // ======== Entrance animations ========
    private static void AnimateIn(UIElement element, double fromY = 12)
    {
        element.RenderTransform = new TranslateTransform();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var sb = new Storyboard { Duration = TimeSpan.FromMilliseconds(180) };

        var fade = new DoubleAnimation(0, 1, sb.Duration.TimeSpan) { EasingFunction = ease };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
        sb.Children.Add(fade);

        var slide = new DoubleAnimation(fromY, 0, sb.Duration.TimeSpan) { EasingFunction = ease };
        Storyboard.SetTarget(slide, element);
        Storyboard.SetTargetProperty(slide, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        sb.Children.Add(slide);

        sb.Begin((FrameworkElement)element, true);
    }

    // ======== Test mode ========
    private void TestMode_Changed(object sender, RoutedEventArgs e)
    {
        SettingsStore.TestMode = ChkTestMode.IsChecked == true;
        TestModeChip.Visibility = SettingsStore.TestMode ? Visibility.Visible : Visibility.Collapsed;
        RefreshCommandBar();
        if (SettingsStore.TestMode)
            ShowToast("Test mode enabled — changes are held until you press Save");
    }

    private void RefreshCommandBar()
    {
        var pending = SettingsStore.TestMode && SettingsStore.HasUnsavedChanges;
        PendingLabel.Visibility = pending || SettingsStore.TestMode ? Visibility.Visible : Visibility.Collapsed;
        PendingLabel.Text = pending
            ? "Test mode — press Save to apply changes"
            : "Test mode — changes hold until Save";
        SavedLabel.Visibility = pending || SettingsStore.TestMode ? Visibility.Collapsed : Visibility.Visible;
    }

    // ======== Save / launch / close ========
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SettingsStore.Save();
        ShowToast("Settings saved");
        RefreshCommandBar();
    }

    private async void BtnSaveLaunch_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnSaveLaunch.IsEnabled = false;
            SettingsStore.Save();
            ShowToast("Launching Roblox…");
            await LaunchManager.LaunchPlayerAsync();
            if (SettingsStore.Settings.Launcher.AutoCloseAfterLaunch)
                Close();
        }
        catch (Exception ex)
        {
            Logger.Error("Launch from command bar failed", ex);
            ShowToast("Launch failed: " + ex.Message, true);
        }
        finally
        {
            BtnSaveLaunch.IsEnabled = true;
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsStore.TestMode && SettingsStore.HasUnsavedChanges)
        {
            var result = System.Windows.MessageBox.Show(
                "Test mode has unsaved changes. Discard them and close?",
                "Fishstrap V2", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
        }
        Application.Current.Shutdown();
    }

    // ======== Toasts ========
    public void ShowToast(string message, bool isError = false)
    {
        ToastText.Text = message;
        if (isError)
            ToastText.SetResourceReference(TextBlock.ForegroundProperty, "BrushDanger");
        else
            ToastText.SetResourceReference(TextBlock.ForegroundProperty, "BrushTextPrimary");
        Toast.Visibility = Visibility.Visible;
        AnimateIn(Toast, 8);

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer!.Stop();
            Toast.Visibility = Visibility.Hidden;
        };
        _toastTimer.Start();
    }
}
