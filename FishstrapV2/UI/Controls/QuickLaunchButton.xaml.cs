using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace FishstrapV2.UI.Controls;

public partial class QuickLaunchButton : UserControl
{
    public static readonly RoutedEvent LaunchClickEvent = EventManager.RegisterRoutedEvent(
        nameof(LaunchClick), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(QuickLaunchButton));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(QuickLaunchButton), new PropertyMetadata(""));

    public static readonly DependencyProperty IconTintProperty =
        DependencyProperty.Register(nameof(IconTint), typeof(Brush), typeof(QuickLaunchButton),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4C, 0x3F, 0x8F))));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(QuickLaunchButton), new PropertyMetadata(""));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(QuickLaunchButton), new PropertyMetadata(""));

    public QuickLaunchButton()
    {
        InitializeComponent();
        MouseEnter += (_, _) => AnimateHover(true);
        MouseLeave += (_, _) => AnimateHover(false);
        MouseLeftButtonUp += OnMouseUp;
    }

    public event RoutedEventHandler LaunchClick
    {
        add => AddHandler(LaunchClickEvent, value);
        remove => RemoveHandler(LaunchClickEvent, value);
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

    private void AnimateHover(bool hover)
    {
        var ease = new CubicEase();
        var sb = new Storyboard();
        var ms = hover ? 120 : 180;
        var scale = hover ? 1.02 : 1.0;

        var fade = new DoubleAnimation(hover ? 1 : 0, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        Storyboard.SetTarget(fade, HoverLayer);
        Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));

        var scaleX = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        Storyboard.SetTarget(scaleX, Root);
        Storyboard.SetTargetProperty(scaleX, new PropertyPath("RenderTransform.(ScaleTransform.ScaleX)"));

        var scaleY = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };
        Storyboard.SetTarget(scaleY, Root);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath("RenderTransform.(ScaleTransform.ScaleY)"));

        sb.Children.Add(fade);
        sb.Children.Add(scaleX);
        sb.Children.Add(scaleY);
        sb.Begin(this, true);
    }

    private static void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is QuickLaunchButton btn)
            btn.RaiseEvent(new RoutedEventArgs(LaunchClickEvent, btn));
    }
}
