using System.Windows;
using System.Windows.Controls;
using FishstrapV2.Core;
using FishstrapV2.UI;

namespace FishstrapV2.Pages;

public class ChartDay
{
    public double BarHeight { get; set; }
    public int Count { get; set; }
}

public class SessionRow
{
    public string StartText { get; set; } = "";
    public string Binary { get; set; } = "";
    public string DurationText { get; set; } = "";
}

public partial class StatisticsPage : FishstrapPage
{
    public StatisticsPage()
    {
        InitializeComponent();
        StatisticsStore.Changed += OnStatsChanged;
        Loaded += (_, _) => OnShown();
        Unloaded += (_, _) => StatisticsStore.Changed -= OnStatsChanged;
    }

    public override void OnShown() => Refresh();

    private void OnStatsChanged()
    {
        Dispatcher.Invoke(Refresh);
    }

    private void Refresh()
    {
        var data = StatisticsStore.Data;

        CardLaunches.Value = data.TotalLaunches.ToString("N0");
        CardPlaytime.Value = StatisticsStore.FormatDuration(data.TotalPlaySeconds);

        var finished = data.Sessions.Where(s => s.End is not null).ToList();
        var avg = finished.Count > 0
            ? (long)finished.Average(s => (s.End!.Value - s.Start).TotalSeconds)
            : 0;
        CardAverage.Value = StatisticsStore.FormatDuration(avg);

        var days = StatisticsStore.GetLastDays(14);
        var max = Math.Max(1, days.Max(d => d.Count));
        Chart.ItemsSource = days.Select(d => new ChartDay
        {
            Count = d.Count,
            BarHeight = d.Count == 0 ? 3 : Math.Max(6, 100.0 * d.Count / max),
        }).ToList();
        ChartLabels.ItemsSource = days.Select(d => d.Day.ToString("MM/dd")).ToList();

        SessionList.ItemsSource = data.Sessions
            .OrderByDescending(s => s.Start)
            .Take(100)
            .Select(s => new SessionRow
            {
                StartText = s.Start.ToString("MMM dd, yyyy HH:mm"),
                Binary = s.Binary,
                DurationText = s.End is null ? "in progress" : StatisticsStore.FormatDuration((long)(s.End.Value - s.Start).TotalSeconds),
            })
            .ToList();

        NoSessions.Visibility = data.Sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnClearStats_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Clear all recorded statistics? This cannot be undone.",
                "Fishstrap V2", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var data = StatisticsStore.Data;
        data.TotalLaunches = 0;
        data.TotalPlaySeconds = 0;
        data.PerDay.Clear();
        data.Sessions.Clear();
        StatisticsStore.Save();
        Refresh();
        MainWindow.Current?.ShowToast("Statistics cleared");
    }
}
