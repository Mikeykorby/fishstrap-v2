using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FishstrapV2.UI.Controls;

public partial class StatusCard : UserControl
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(StatusCard), new PropertyMetadata(""));

    public static readonly DependencyProperty IconTintProperty =
        DependencyProperty.Register(nameof(IconTint), typeof(Brush), typeof(StatusCard),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38))));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatusCard), new PropertyMetadata(""));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatusCard),
            new PropertyMetadata("", OnValueChanged));

    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(Brush), typeof(StatusCard),
            new PropertyMetadata(default(Brush)));

    public StatusCard()
    {
        InitializeComponent();
        ValueBrush = TryFindResource("BrushTextPrimary") as Brush;
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Brush IconTint
    {
        get => (Brush)GetValue(IconTintProperty);
        set => SetValue(IconTintProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Brush ValueBrush
    {
        get => (Brush)GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusCard card)
            card.ValueText.Text = e.NewValue as string ?? "";
    }
}
