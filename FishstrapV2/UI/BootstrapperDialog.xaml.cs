using System.ComponentModel;
using System.IO;
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
        Title = AppInfo.ProductName;

        _status = Message;
        _progress = Progress;

        ApplyBloxnifiedTheme();
        ApplyLogoAnimation();

        if (ThemeManager.IsLight)
            Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF4));
    }

    private void ApplyBloxnifiedTheme()
    {
        var theme = Bloxnified.Find(SettingsStore.Settings.Launcher.BootstrapperStyle);
        if (theme is null) return;

        var layout = Bloxnified.TryLoad(theme);
        if (layout is null) return;

        _themed = true;
        _squareCorners = layout.SquareCorners;
        RootPanel.Children.Add(layout.Root);
        DefaultPanel.Visibility = Visibility.Collapsed;

        if (layout.Root.Width > 0) Width = layout.Root.Width;
        if (layout.Root.Height > 0) Height = layout.Root.Height;

        // Some themes (V3) have no StatusText; status updates then go to the hidden
        // default label, matching how Bloxstrap renders these themes.
        _status = layout.Status ?? Message;
        _progress = layout.Progress;

        if (layout.Cancel is not null)
        {
            layout.Cancel.Style = Application.Current.TryFindResource("CmdButton") as Style;
            layout.Cancel.Click += BtnCancel_Click;
        }
    }

    private void ApplyLogoAnimation()
    {
        if (_themed) return; // themes bring their own assets (GIFs animate via the loader)

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
