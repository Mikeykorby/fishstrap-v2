#nullable enable
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace FishstrapV2.Core;

public class SessionRecord
{
    public DateTime Start { get; set; }
    public DateTime? End { get; set; }
    public string Binary { get; set; } = "";
}

public class StatisticsData
{
    public int TotalLaunches { get; set; }
    public long TotalPlaySeconds { get; set; }
    public Dictionary<string, int> PerDay { get; set; } = new();
    public List<SessionRecord> Sessions { get; set; } = new();
}

public static class StatisticsStore
{
    private static readonly object Lock = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static StatisticsData Data { get; private set; } = new();
    public static event Action? Changed;

    public static void Load()
    {
        try
        {
            if (File.Exists(Paths.StatisticsFile))
            {
                var loaded = JsonSerializer.Deserialize<StatisticsData>(File.ReadAllText(Paths.StatisticsFile), JsonOpts);
                if (loaded is not null)
                    Data = loaded;
            }

            // Close stale open sessions (app probably closed while a game was running).
            lock (Lock)
            {
                foreach (var s in Data.Sessions.Where(s => s.End is null && DateTime.Now - s.Start > TimeSpan.FromHours(24)))
                    s.End = s.Start;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to load statistics", ex);
            Data = new StatisticsData();
        }
    }

    public static void Save()
    {
        try
        {
            lock (Lock)
            {
                if (Data.Sessions.Count > 300)
                    Data.Sessions = Data.Sessions.OrderByDescending(s => s.Start).Take(300).ToList();

                Paths.EnsureDirectories();
                var tmp = Paths.StatisticsFile + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Data, JsonOpts));
                File.Move(tmp, Paths.StatisticsFile, true);
            }
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save statistics", ex);
        }
    }

    public static void RecordLaunch(string binary, string exePath)
    {
        var session = new SessionRecord { Start = DateTime.Now, Binary = binary };
        lock (Lock)
        {
            Data.TotalLaunches++;
            var key = DateTime.Now.ToString("yyyy-MM-dd");
            Data.PerDay[key] = Data.PerDay.TryGetValue(key, out var n) ? n + 1 : 1;
            Data.Sessions.Add(session);
        }
        Save();

        _ = Task.Run(() => WatchProcessExit(session, exePath));
    }

    private static async Task WatchProcessExit(SessionRecord session, string exePath)
    {
        try
        {
            var processName = Path.GetFileNameWithoutExtension(exePath);
            var started = DateTime.Now;

            while (true)
            {
                await Task.Delay(5000);
                var running = Process.GetProcessesByName(processName).Length > 0;
                if (!running || DateTime.Now - started > TimeSpan.FromHours(12))
                    break;
            }

            lock (Lock)
            {
                session.End = DateTime.Now;
                Data.TotalPlaySeconds += (long)(session.End.Value - session.Start).TotalSeconds;
            }
            Save();
        }
        catch (Exception ex)
        {
            Logger.Warn("Session watcher stopped: " + ex.Message);
        }
    }

    public static int GetLaunchesSince(int days)
    {
        lock (Lock)
        {
            int total = 0;
            for (var i = 0; i < days; i++)
            {
                var key = DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd");
                if (Data.PerDay.TryGetValue(key, out var n))
                    total += n;
            }
            return total;
        }
    }

    public static List<(DateTime Day, int Count)> GetLastDays(int days)
    {
        lock (Lock)
        {
            var list = new List<(DateTime, int)>();
            for (var i = days - 1; i >= 0; i--)
            {
                var day = DateTime.Now.Date.AddDays(-i);
                var key = day.ToString("yyyy-MM-dd");
                list.Add((day, Data.PerDay.TryGetValue(key, out var n) ? n : 0));
            }
            return list;
        }
    }

    public static string FormatDuration(long seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        if (seconds < 3600) return $"{seconds / 60}m {seconds % 60}s";
        if (seconds < 86400) return $"{seconds / 3600}h {(seconds % 3600) / 60}m";
        return $"{seconds / 86400}d {(seconds % 86400) / 3600}h";
    }
}
