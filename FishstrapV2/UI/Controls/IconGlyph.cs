using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace FishstrapV2.UI.Controls;

/// <summary>
/// Renders a named vector icon ("Icon.*" geometry resources, 24x24 stroke style) using the
/// inherited Foreground as the stroke. Replaces font-glyph icons so icons render identically
/// on every machine, regardless of installed fonts.
/// </summary>
public class IconGlyph : FrameworkElement
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(IconGlyph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(IconGlyph),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    // Re-own the inherited TextElement.Foreground so XAML can set Foreground directly on this element.
    public static readonly DependencyProperty ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner(typeof(IconGlyph),
            new FrameworkPropertyMetadata(Brushes.Gray,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private static readonly Dictionary<Brush, Pen> PenCache = new();

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    protected override void OnRender(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Icon))
            return;
        if (TryFindResource("Icon." + Icon) is not Geometry geometry)
            return;

        var brush = Foreground;
        if (!PenCache.TryGetValue(brush, out var pen))
        {
            pen = new Pen(brush, 2.0)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            pen.Freeze();
            PenCache[brush] = pen;
        }

        dc.PushTransform(new ScaleTransform(Size / 24.0, Size / 24.0));
        dc.DrawGeometry(null, pen, geometry);
        dc.Pop();
    }
}
