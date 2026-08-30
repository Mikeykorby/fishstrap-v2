using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FishstrapV2.UI.Controls;

/// <summary>
/// A settings row: icon, title, subtitle and arbitrary right-side content.
/// A templated control (no own namescope) so named children inside RowContent
/// register in the hosting page's scope.
/// </summary>
public class SettingRow : Control
{
    static SettingRow()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingRow), new FrameworkPropertyMetadata(typeof(SettingRow)));
    }

    public static readonly RoutedEvent RowClickEvent = EventManager.RegisterRoutedEvent(
        nameof(RowClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SettingRow));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public static readonly DependencyProperty IconTintProperty =
        DependencyProperty.Register(nameof(IconTint), typeof(Brush), typeof(SettingRow),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38))));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public static readonly DependencyProperty RowContentProperty =
        DependencyProperty.Register(nameof(RowContent), typeof(object), typeof(SettingRow), new PropertyMetadata(null));

    public static readonly DependencyProperty IsClickableProperty =
        DependencyProperty.Register(nameof(IsClickable), typeof(bool), typeof(SettingRow),
            new PropertyMetadata(false, OnIsClickableChanged));

    public event RoutedEventHandler RowClick
    {
        add => AddHandler(RowClickEvent, value);
        remove => RemoveHandler(RowClickEvent, value);
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

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object RowContent
    {
        get => GetValue(RowContentProperty);
        set => SetValue(RowContentProperty, value);
    }

    public bool IsClickable
    {
        get => (bool)GetValue(IsClickableProperty);
        set => SetValue(IsClickableProperty, value);
    }

    private static void OnIsClickableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingRow row)
        {
            row.Cursor = (bool)e.NewValue ? Cursors.Hand : Cursors.Arrow;
            row.MouseLeftButtonUp -= OnMouseUp;
            if ((bool)e.NewValue)
                row.MouseLeftButtonUp += OnMouseUp;
        }
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is SettingRow row)
            row.RaiseEvent(new RoutedEventArgs(RowClickEvent, row));
    }
}
