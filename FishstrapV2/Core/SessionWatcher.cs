using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FishstrapV2.Core;

/// <summary>
/// Tails the newest Roblox client log (the same entries Bloxstrap/Fishstrap
/// parse) and exposes the live session. One watcher serves Discord RPC and
/// the server-information card; it never touches the network itself.
/// </summary>
public static class SessionWatcher
{
    public sealed class Session
    {
        public string PlaceId { get; set; } = "";
        public string JobId { get; set; } = "";
        public string ServerAddress { get; set; } = "";
        public string GameName { get; set; } = "";
        public DateTime JoinedAt { get; set; }        // when we joined (UTC)
        public DateTime? ServerBoot { get; set; }     // server's own start time
        public string LogFile { get; set; } = "";     // log the join came from
    }

    private static readonly Regex JoinRegex =
        new(@"! Joining game '([0-9a-f\-]{36})' place ([0-9]+) at ([0-9\.]+)", RegexOptions.Compiled);
    private static readonly Regex UdmuxRegex =
        new(@"UDMUX Address = ([0-9\.]+), Port = [0-9]+", RegexOptions.Compiled);
    private static readonly Regex ServerPrefixRegex =
        new(@"Server Prefix:.+_([0-9]{8}T[0-9]{6}Z)_RCC_[0-9a-z]+", RegexOptions.Compiled);

    private static System.Timers.Timer? _pollTimer;
    private static long _logOffset;
    private static string _lastLogFile = "";
    private static string _currentLogFile = "";
    private static readonly object Gate = new();

    public static Session? Current { get; private set; }
    public static event Action? Changed;

    public static void Start()
    {
        if (_pollTimer is not null) return;
        _pollTimer = new System.Timers.Timer(3000);
        _pollTimer.Elapsed += (_, _) => Poll();
        _pollTimer.Start();
        Logger.Info("Session watcher started");
    }

    private static void Poll()
    {
        try
        {
            var text = ReadLogDelta();
            if (string.IsNullOrEmpty(text)) return;

            lock (Gate)
            {
                foreach (var line in text.Split('\n'))
                {
                    var join = JoinRegex.Match(line);
                    if (join.Success)
                    {
                        Current = new Session
                        {
                            JobId = join.Groups[1].Value,
                            PlaceId = join.Groups[2].Value,
                            ServerAddress = join.Groups[3].Value,
                            JoinedAt = DateTime.UtcNow,
                            LogFile = _currentLogFile,
                        };
                        Changed?.Invoke();
                        _ = Task.Run(() => ResolveGameName(Current!.PlaceId));
                        continue;
                    }

                    if (Current is null) continue;

                    var udmux = UdmuxRegex.Match(line);
                    if (udmux.Success)
                    {
                        Current.ServerAddress = udmux.Groups[1].Value;
                        Changed?.Invoke();
                        continue;
                    }

                    var prefix = ServerPrefixRegex.Match(line);
                    if (prefix.Success && Current.ServerBoot is null)
                    {
                        Current.ServerBoot = DateTime.ParseExact(
                            prefix.Groups[1].Value, "yyyyMMdd'T'HHmmss'Z'",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AdjustToUniversal);
                        Changed?.Invoke();
                        continue;
                    }

                    if (line.Contains("Time to disconnect replication data"))
                    {
                        Current = null;
                        Changed?.Invoke();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Session watcher poll failed: " + ex.Message);
        }
    }

    private static string? ReadLogDelta()
    {
        try
        {
            var dir = Paths.RobloxLogsDir;
            if (!Directory.Exists(dir)) return null;

            var log = new DirectoryInfo(dir).GetFiles("*.log")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            if (log is null) return null;

            lock (Gate)
            {
                if (log.FullName != _lastLogFile)
                {
                    // A new client instance started; the old session is dead.
                    _lastLogFile = log.FullName;
                    _currentLogFile = log.FullName;
                    _logOffset = 0;
                    if (Current is not null)
                    {
                        Current = null;
                        Changed?.Invoke();
                    }
                }

                // A session only survives while its client lives and its own log keeps
                // growing — covers kills/crashes that never emit the disconnect entry,
                // and stale joins found in an old log at startup.
                if (Current is not null && SessionDead(Current))
                {
                    Current = null;
                    Changed?.Invoke();
                }

                if (log.Length <= _logOffset) return null;

                using var stream = new FileStream(log.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stream.Seek(_logOffset, SeekOrigin.Begin);
                using var reader = new StreamReader(stream);
                var newText = reader.ReadToEnd();
                _logOffset = stream.Length;
                return newText;
            }
        }
        catch
        {
            return null;
        }
    }

    private static bool SessionDead(Session s)
    {
        if (!IsRobloxRunning()) return true;
        return File.GetLastWriteTimeUtc(s.LogFile) < DateTime.UtcNow - TimeSpan.FromMinutes(2);
    }

    private static bool IsRobloxRunning() =>
        Process.GetProcessesByName("RobloxPlayerBeta").Length > 0 ||
        Process.GetProcessesByName("RobloxStudioBeta").Length > 0;

    private static async Task ResolveGameName(string placeId)
    {
        if (Cache.TryGetValue(placeId, out var cached))
        {
            ApplyGameName(placeId, cached);
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FishstrapV2");

            var universeResp = await http.GetStringAsync($"https://apis.roblox.com/universes/v1/places/{placeId}/universe");
            using var uDoc = JsonDocument.Parse(universeResp);
            var universeId = uDoc.RootElement.TryGetProperty("universeId", out var uid) ? uid.GetInt64().ToString() : "";

            if (universeId.Length > 0)
            {
                var gamesResp = await http.GetStringAsync($"https://games.roblox.com/v1/games?universeIds={universeId}");
                using var gDoc = JsonDocument.Parse(gamesResp);
                var name = gDoc.RootElement.GetProperty("data")[0].TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!string.IsNullOrEmpty(name))
                {
                    Cache[placeId] = name!;
                    ApplyGameName(placeId, name!);
                }
            }
        }
        catch
        {
            // Name resolution is optional.
        }
    }

    private static void ApplyGameName(string placeId, string name)
    {
        var applied = false;
        lock (Gate)
        {
            if (Current is { PlaceId: var pid } && pid == placeId)
            {
                Current.GameName = name;
                applied = true;
            }
        }
        if (applied) Changed?.Invoke();
    }

    private static readonly Dictionary<string, string> Cache = new();
}
