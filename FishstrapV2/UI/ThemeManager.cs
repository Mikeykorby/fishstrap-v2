using System.Windows;
using System.Windows.Media;

namespace FishstrapV2.UI;

public static class ThemeManager
{
    /// <summary>Whether the currently applied theme is light.</summary>
    public static bool IsLight { get; private set; }
    /// <summary>Applies the selected theme ("Dark", "Light" or "System").</summary>
    public static void ApplyTheme(string theme)
    {
        var app = Application.Current;
        if (app is null) return;

        var dicts = app.Resources.MergedDictionaries;
        var existing = dicts.FirstOrDefault(d =>
            d.Source is not null && d.Source.OriginalString.Contains("Light.xaml"));

        var useLight = theme == "Light" || (theme == "System" && IsSystemLight());
        IsLight = useLight;

        if (useLight && existing is null)
            dicts.Add(new ResourceDictionary { Source = new Uri("UI/Light.xaml", UriKind.Relative) });
        else if (!useLight && existing is not null)
            dicts.Remove(existing);
    }

    public static bool IsSystemLight()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") as int? == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Applies a hex accent color to the whole app.</summary>
    public static void ApplyAccent(string hex)
    {
        var app = Application.Current;
        if (app is null) return;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex.Trim());
            app.Resources["BrushAccent"] = Freeze(new SolidColorBrush(color));
            app.Resources["BrushAccentHover"] = Freeze(new SolidColorBrush(Brighten(color, 1.14)));
            app.Resources["BrushAccentSubtle"] = Freeze(new SolidColorBrush(Color.FromArgb(38, color.R, color.G, color.B)));
        }
        catch (Exception ex)
        {
            Core.Logger.Warn("Failed to apply accent: " + ex.Message);
        }
    }

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static Color Brighten(Color c, double factor)
    {
        byte Channel(int v) => (byte)Math.Clamp((int)Math.Round(v * factor), 0, 255);
        return Color.FromRgb(Channel(c.R), Channel(c.G), Channel(c.B));
    }
}
