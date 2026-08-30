using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FishstrapV2.Core;
namespace FishstrapV2.UI;

/// <summary>
/// Bloxstrap-style progress dialog shown while Fishstrap installs or upgrades Roblox.
/// Status message, progress bar and a Cancel button that aborts the install. When the
/// bootstrapper style is a custom theme, the theme's own layout is rendered instead of
/// the default Fishstrap panel; the logo can spin or use a custom (GIF) image.
/// </summary>
public partial class BootstrapperDialog : Window, RobloxDeployClient.IProgressHook
{
    public static BootstrapperDialog? Current { get; private set; }

    /// <summary>Dialog reference for install progress objects (RobloxDeployClient.IProgressHook).</summary>
    BootstrapperDialog? RobloxDeployClient.IProgressHook.Dialog => this;

    private CancellationTokenSource? _cts;
    private bool _isClosing;
    private bool _themed;
    private bool _squareCorners;
    private TextBlock _status;
    private ProgressBar? _progress;

    public BootstrapperDialog()
    {
        InitializeComponent();
        Title = string.IsNullOrWhiteSpace(SettingsStore.Settings.Launcher.BootstrapperTitle)
            ? AppInfo.ProductName
            : SettingsStore.Settings.Launcher.BootstrapperTitle;

        _status = Message;
        _progress = Progress;

        ApplyBloxnifiedTheme();
        if (!_themed)
            ApplyFishstrapStyle();
        ApplyLogoAnimation();
    }

    /// <summary>Fishstrap's original dialog styles, re-created natively.</summary>
    private void ApplyFishstrapStyle()
    {
        var light = ThemeManager.IsLight;
        switch (SettingsStore.Settings.Launcher.BootstrapperStyle)
        {
            case "Classic Fluent":
                Width = 420;
                Height = 190;
                DefaultPanel.Visibility = Visibility.Collapsed;
                ClassicFluentPanel.Visibility = Visibility.Visible;
                _status = ClassicFluentStatus;
                _progress = ClassicFluentProgress;
                break;

            case "Terminal":
                Width = 800;
                Height = 520;
                Background = new SolidColorBrush(Color.FromRgb(0x0C, 0x0C, 0x0C));
                DefaultPanel.Visibility = Visibility.Collapsed;
                TerminalPanel.Visibility = Visibility.Visible;
                TerminalLaunchArgs.Text = "roblox-player:";
                _status = TerminalStatus;
                _progress = null; // the console reports progress as messages only
                _squareCorners = true;
                break;

            case "TwentyFive":
                Width = 576;
                Height = 482;
                Background = Brushes.Black;
                DefaultPanel.Visibility = Visibility.Collapsed;
                TwentyFivePanel.Visibility = Visibility.Visible;
                _status = TwentyFiveStatus; // the 2025 launcher shows progress only
                _progress = TwentyFiveProgress;
                _squareCorners = true;
                break;

            default:
                // Fishstrap fluent dialog (the DefaultPanel above).
                if (light)
                    Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF4));
                break;
        }
    }

    private void ApplyBloxnifiedTheme()
    {
        var theme = Bloxnified.Find(SettingsStore.Settings.Launcher.BootstrapperStyle);
        if (theme is null)
        {
            Logger.Info($"Bootstrapper: no theme for style '{SettingsStore.Settings.Launcher.BootstrapperStyle}'");
            return;
        }

        var layout = Bloxnified.TryLoad(theme);
        if (layout is null)
        {
            Logger.Info($"Bootstrapper: theme '{theme.Name}' failed to load");
            return;
        }

        var added = new List<UIElement>();
        try
        {
            RootPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RootPanel.RowDefinitions.Add(new RowDefinition());

            if (layout.ShowTitleBar)
            {
                // Bloxstrap renders a real window title bar for these themes; synthesize one.
                var strip = new Border
                {
                    Height = 30,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(layout.TitleBarColor)),
                };
                strip.MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { /* not draggable yet */ } };

                var row = new Grid();
                var title = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(SettingsStore.Settings.Launcher.BootstrapperTitle)
                        ? AppInfo.ProductName
                        : SettingsStore.Settings.Launcher.BootstrapperTitle,
                    FontSize = 11.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0xA6)),
                };
                row.Children.Add(title);

                if (layout.ShowCloseButton)
                {
                    var close = new Button
                    {
                        Content = "\u2715",
                        Width = 40,
                        Background = System.Windows.Media.Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0xA6)),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        FontSize = 12,
                    };
                    close.Click += (_, _) => _cts?.Cancel();
                    row.Children.Add(close);
                }

                strip.Child = row;
                Grid.SetRow(strip, 0);
                RootPanel.Children.Add(strip);
                added.Add(strip);
            }

            Grid.SetRow(layout.Root, 1);
            RootPanel.Children.Add(layout.Root);
            added.Add(layout.Root);

            DefaultPanel.Visibility = Visibility.Collapsed;
            if (layout.Root.Width > 0) Width = layout.Root.Width;
            if (layout.Root.Height > 0) Height = layout.Root.Height + (layout.ShowTitleBar ? 30 : 0);

            // Some themes (V3) have no StatusText; status updates then go to the hidden
            // default label, matching how Bloxstrap renders these themes.
            _status = layout.Status ?? Message;
            _progress = layout.Progress;
            _themed = true;
            _squareCorners = layout.SquareCorners;

            if (layout.Cancel is not null)
            {
                layout.Cancel.Style = Application.Current.TryFindResource("CmdButton") as Style;
                layout.Cancel.Click += BtnCancel_Click;
            }
        }
        catch (Exception ex)
        {
            // Never let a broken theme take an install down with it.
            foreach (var element in added) RootPanel.Children.Remove(element);
            RootPanel.RowDefinitions.Clear();
            DefaultPanel.Visibility = Visibility.Visible;
            _status = Message;
            _progress = Progress;
            _themed = false;
            Logger.Warn($"Themed bootstrapper '{theme.Name}' fell back to the default dialog: {ex.GetBaseException().Message}");
        }
    }

    private void ApplyLogoAnimation()
    {
        if (_themed || DefaultPanel.Visibility != Visibility.Visible)
            return; // themes and other styles bring their own visuals

        var s = SettingsStore.Settings.Launcher;
        if (s.BootstrapperAnimation == "Spin")
        {
            var rotate = new RotateTransform();
            Logo.RenderTransform = rotate;
            Logo.RenderTransformOrigin = new Point(0.5, 0.5);
            rotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, new Duration(TimeSpan.FromSeconds(1.8)))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                });
        }
        else if (s.BootstrapperAnimation == "Custom" && File.Exists(s.BootstrapperIconFile))
        {
            Bloxnified.AnimateGif(Logo, s.BootstrapperIconFile);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (_squareCorners)
        {
            // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_DONOTROUND = 1 (ignored on Windows 10)
            var preference = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref preference, 4);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public CancellationToken CancellationToken => (_cts ??= new CancellationTokenSource()).Token;

    /// <summary>
    /// Runs <paramref name="work"/> while the dialog is on screen. The work callback receives the
    /// dialog (status/byte reporter) and its cancellation token; cancelling closes the dialog.
    /// </summary>
    public static async Task<T> ShowProgressAsync<T>(
        string initialMessage, Func<BootstrapperDialog, CancellationToken, Task<T>> work)
    {
        var dialog = await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var d = new BootstrapperDialog();
            d.SetMessage(initialMessage);
            Current = d;
            d.Show();
            return d;
        });

        try
        {
            var result = await work(dialog, dialog.CancellationToken);
            await dialog.CloseForShutdownAsync();
            return result;
        }
        catch (OperationCanceledException)
        {
            await dialog.CloseForShutdownAsync();
            Logger.Info("Bootstrapper: operation cancelled by user");
            throw;
        }
        catch
        {
            await dialog.CloseForShutdownAsync();
            throw;
        }
        finally
        {
            Current = null;
        }
    }

    private async Task CloseForShutdownAsync()
    {
        await Dispatcher.InvokeAsync(() =>
        {
            _isClosing = true;
            Close();
        });
    }

    /// <summary>Switches the progress bar from indeterminate to byte-based.</summary>
    public void StartDeterminate(long totalBytes) =>
        Dispatcher.Invoke(() =>
        {
            if (_progress is null) return;
            _progress.IsIndeterminate = false;
            _progress.Maximum = Math.Max(1, totalBytes);
        });

    /// <summary>Reports byte-level download progress; no-op while indeterminate.</summary>
    public void ReportBytes(long done, long total)
    {
        if (_progress is null || _progress.IsIndeterminate) return;
        Dispatcher.Invoke(() => _progress.Value = Math.Min(done, total));
    }

    public void SetMessage(string message) => Dispatcher.Invoke(() => _status.Text = message);

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        if (sender is Button button) button.IsEnabled = false;
        else BtnCancel.IsEnabled = false;
        _status.Text = "Cancelling...";
        Logger.Info("Bootstrapper: install cancelled by user");
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_isClosing)
        {
            _cts?.Cancel();
            e.Cancel = true;
        }
    }
}
