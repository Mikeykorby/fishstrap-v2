using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FishstrapV2.Core;

namespace FishstrapV2.UI;

/// <summary>
/// Bloxstrap-style custom bootstrapper themes imported by the user; the folders live in
/// %LOCALAPPDATA%\FishstrapV2\Bootstrappers. Theme.xml is parsed with XamlReader after a
/// minimal adaptation: the root element is renamed to a Grid, Bloxstrap-only attributes
/// are stripped, and theme:// resources resolve to the folder.
/// </summary>
public static class Bloxnified
{
    public record Theme(string Name, string Dir);

    public static Theme? Find(string style)
    {
        var dir = Path.Combine(Paths.BootstrappersDir, style);
        return File.Exists(Path.Combine(dir, "Theme.xml")) ? new Theme(style, dir) : null;
    }

    public static string[] UserThemeNames()
    {
        try
        {
            return Directory.GetDirectories(Paths.BootstrappersDir)
                .Where(d => File.Exists(Path.Combine(d, "Theme.xml")))
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    public sealed class Layout
    {
        public required FrameworkElement Root;
        public TextBlock? Status;
        public ProgressBar? Progress;
        public Button? Cancel;
        public bool SquareCorners;
        public bool ShowTitleBar;
        public bool ShowCloseButton;
        public string TitleBarColor = "#1B1B1F";
    }

    /// <summary>Parses the theme's Theme.xml; returns null (with a log entry) if unusable.</summary>
    public static Layout? TryLoad(Theme theme)
    {
        for (var attempt = 0; ; attempt++)
        {
        try
        {
            var dir = theme.Dir;
            var xml = File.ReadAllText(Path.Combine(dir, "Theme.xml"));

            var rootTag = Regex.Match(xml, "<BloxstrapCustomBootstrapper(?<attrs>\\s[^>]*)>");
            if (!rootTag.Success) return null;
            var attrs = rootTag.Groups["attrs"].Value;

            var kept = Regex.Replace(attrs,
                "(Version|AllowTransparency|WindowBackdropType|WindowCornerPreference|IgnoreTitleBarInset|Theme|Margin)\\s*=\\s*\"[^\"]*\"", "");
            var squareCorners = Regex.IsMatch(attrs, "WindowCornerPreference\\s*=\\s*\"DoNotRound\"");

            // TitleBar metadata (the element itself is dropped below).
            var titleBar = Regex.Match(xml, "<TitleBar\\b([^>]*)/?>");
            var tbAttrs = titleBar.Success ? titleBar.Groups[1].Value : "";
            var showTitleBar = titleBar.Success && !Regex.IsMatch(tbAttrs, "Visibility\\s*=\\s*\"Collapsed\"");
            var showClose = Regex.IsMatch(tbAttrs, "ShowClose\\s*=\\s*\"True\"");
            var tbColorMatch = Regex.Match(attrs, "Background\\s*=\\s*\"(#[0-9A-Fa-f]{6,8})\"");
            var titleBarColor = tbColorMatch.Success ? tbColorMatch.Groups[1].Value : "#1B1B1F";

            // Rename property elements and the root element to a WPF Grid.
            xml = Regex.Replace(xml, "<BloxstrapCustomBootstrapper\\.([A-Za-z0-9.]+)>", "<Grid.$1>");
            xml = Regex.Replace(xml, "</BloxstrapCustomBootstrapper\\.([A-Za-z0-9.]+)>", "</Grid.$1>");
            xml = xml.Replace("</BloxstrapCustomBootstrapper>", "</Grid>");
            xml = Regex.Replace(xml, "<BloxstrapCustomBootstrapper(?<attrs>\\s[^>]*)>",
                "<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"" + kept + ">");

            xml = Regex.Replace(xml, "<TitleBar[^>]*/>", "");
            xml = Regex.Replace(xml, "\\bZIndex=", "Panel.ZIndex=");
            xml = Regex.Replace(xml, "\\sIsAnimated=\"[^\"]*\"", "");
            xml = Regex.Replace(xml, "(?<=<ProgressBar[^>]*)\\s(CornerRadius|IndicatorCornerRadius)=\"[^\"]*\"", "");

            xml = xml.Replace("{Icon}", "pack://application:,,,/Assets/Bloxstrap.png")
                     .Replace("{TextFillColorPrimaryBrush}", "#FFFFFF");

            // theme:// resources point at the theme folder on disk.
            xml = Regex.Replace(xml, "theme://(?<file>[^\"]+)", m =>
                new Uri(Path.Combine(dir, m.Groups["file"].Value.Replace('/', Path.DirectorySeparatorChar))).AbsoluteUri);

            // Tag GIF images with their path so they can be animated after parsing.
            xml = Regex.Replace(xml, "Source=\"([^\"]*\\.gif)\"", "Source=\"$1\" Tag=\"$1\"");

            var root = (FrameworkElement)XamlReader.Parse(xml);
            AnimateThemeGifs(root);

            return new Layout
            {
                Root = root,
                SquareCorners = squareCorners,
                ShowTitleBar = showTitleBar,
                ShowCloseButton = showClose,
                TitleBarColor = titleBarColor,
                Status = root.FindName("StatusText") as TextBlock,
                Progress = root.FindName("PrimaryProgressBar") as ProgressBar,
                Cancel = root.FindName("CancelButton") as Button,
            };
        }
        catch (Exception ex)
        {
            // Transient image locks (e.g. antivirus scans) can fail the first parse; retry once.
            if (attempt == 0) continue;
            Logger.Warn($"Bloxnified theme '{theme.Name}' could not be loaded: {ex.GetBaseException().Message}");
            return null;
        }
        }
    }

    /// <summary>Plays an animated GIF frame by frame; a still image is shown otherwise.</summary>
    public static void AnimateGif(Image image, string path)
    {
        try
        {
            var decoder = new GifBitmapDecoder(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count < 2)
            {
                image.Source = decoder.Frames[0];
                return;
            }
            image.Source = decoder.Frames[0];
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            var frame = 0;
            timer.Tick += (_, _) =>
            {
                frame = (frame + 1) % decoder.Frames.Count;
                image.Source = decoder.Frames[frame];
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not load image '" + path + "': " + ex.Message);
        }
    }

    private static void AnimateThemeGifs(DependencyObject node)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is Image { Tag: string tag } && tag.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                AnimateGif((Image)child, new Uri(tag).LocalPath);
            }
            AnimateThemeGifs(child);
        }
    }
}
