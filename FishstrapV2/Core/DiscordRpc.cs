using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FishstrapV2.Core;

/// <summary>
/// Best-effort Discord Rich Presence: tails the newest Roblox client log,
/// extracts the current place, and publishes presence over the Discord IPC pipe.
/// Fails silently when Discord is not running.
/// </summary>
public static class DiscordRpc
{
    public const string ClientId = "1055003141484658718";

    private static System.Timers.Timer? _pollTimer;
    private static NamedPipeClientStream? _pipe;
    private static string _currentPlaceId = "";
    private static string _gameName = "";
    private static readonly Dictionary<string, string> NameCache = new();
    private static DateTime? _activityStart;
    private static long _logOffset;
    private static string _lastLogFile = "";
    private static readonly object PipeLock = new();

    private static readonly Regex PlaceRegex = new(@"placeId[""':=\s]+(\d+)", RegexOptions.Compiled);
    private static readonly Regex JobRegex = new(@"jobId[""':=\s]+([0-9a-fA-F-]{36})", RegexOptions.Compiled);

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
            Start();
        else
            Stop();
    }

    public static void Start()
    {
        if (_pollTimer is not null) return;
        _pollTimer = new System.Timers.Timer(3000);
        _pollTimer.Elapsed += (_, _) => Poll();
        _pollTimer.Start();
        Logger.Info("Discord RPC watcher started");
    }

    public static void Stop()
    {
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        _pollTimer = null;
        try { ClearPresence(); } catch { }
        lock (PipeLock)
        {
            try { _pipe?.Dispose(); } catch { }
            _pipe = null;
        }
        _currentPlaceId = "";
        _gameName = "";
        _activityStart = null;
        Logger.Info("Discord RPC watcher stopped");
    }

    private static void Poll()
    {
        if (!SettingsStore.Settings.Integrations.DiscordRpc.Enabled)
            return;

        try
        {
            var placeId = ReadLatestPlaceId();
            if (string.IsNullOrEmpty(placeId))
            {
                if (_currentPlaceId.Length > 0)
                {
                    // No longer in game.
                    _currentPlaceId = "";
                    _gameName = "";
                    _activityStart = null;
                    ClearPresence();
                }
                return;
            }

            if (placeId != _currentPlaceId)
            {
                _currentPlaceId = placeId;
                _activityStart = DateTime.UtcNow;
                _ = Task.Run(() => ResolveGameName(placeId));
            }

            EnsureConnected();
            PublishPresence();
        }
        catch (Exception ex)
        {
            Logger.Warn("Discord RPC poll failed: " + ex.Message);
        }
    }

    private static string? ReadLatestPlaceId()
    {
        try
        {
            var dir = Paths.RobloxLogsDir;
            if (!Directory.Exists(dir)) return null;

            var log = Directory.GetFiles(dir, "*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            if (log is null) return null;

            if (log.FullName != _lastLogFile)
            {
                _lastLogFile = log.FullName;
                _logOffset = 0;
            }

            if (log.Length <= _logOffset) return null;

            using var stream = new FileStream(log.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(_logOffset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            var newText = reader.ReadToEnd();
            _logOffset = stream.Length;

            var placeMatch = PlaceRegex.Match(newText);
            return placeMatch.Success ? placeMatch.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task ResolveGameName(string placeId)
    {
        if (NameCache.TryGetValue(placeId, out var cached))
        {
            _gameName = cached;
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FishstrapV2-RPC");

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
                    NameCache[placeId] = name!;
                    _gameName = name!;
                }
            }
        }
        catch
        {
            // Name resolution is optional.
        }
    }

    private static void EnsureConnected()
    {
        lock (PipeLock)
        {
            if (_pipe is { IsConnected: true }) return;

            for (var i = 0; i < 10; i++)
            {
                try
                {
                    var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut);
                    pipe.Connect(500);
                    if (pipe.IsConnected)
                    {
                        _pipe = pipe;
                        SendFrame(0, JsonSerializer.Serialize(new { v = 1, client_id = ClientId }));
                        Logger.Info("Connected to Discord IPC");
                        return;
                    }
                    pipe.Dispose();
                }
                catch
                {
                    // Discord pipe not found at this index.
                }
            }
        }
    }

    private static void PublishPresence()
    {
        var rpc = SettingsStore.Settings.Integrations.DiscordRpc;
        var game = string.IsNullOrEmpty(_gameName) ? "Roblox" : _gameName;

        var details = (rpc.Details.Length > 0 ? rpc.Details : "Playing {game}").Replace("{game}", game);
        var state = rpc.State.Replace("{game}", game);

        var activity = new Dictionary<string, object?>
        {
            ["details"] = details,
            ["state"] = state,
            ["assets"] = new Dictionary<string, object?> { ["large_image"] = "roblox", ["large_text"] = "Roblox" },
        };

        if (rpc.ShowElapsedTime && _activityStart is not null)
            activity["timestamps"] = new Dictionary<string, object?> { ["start"] = new DateTimeOffset(_activityStart.Value).ToUnixTimeSeconds() };

        var payload = JsonSerializer.Serialize(new
        {
            cmd = "SET_ACTIVITY",
            args = new { pid = Process.GetCurrentProcess().Id, activity },
            nonce = Guid.NewGuid().ToString(),
        });

        SendFrame(1, payload);
    }

    private static void ClearPresence()
    {
        try
        {
            if (_pipe is not { IsConnected: true }) return;
            var payload = JsonSerializer.Serialize(new
            {
                cmd = "SET_ACTIVITY",
                args = new { pid = Process.GetCurrentProcess().Id, activity = (object?)null },
                nonce = Guid.NewGuid().ToString(),
            });
            SendFrame(1, payload);
        }
        catch { }
    }

    private static void SendFrame(int op, string json)
    {
        lock (PipeLock)
        {
            if (_pipe is not { IsConnected: true }) return;
            var payload = Encoding.UTF8.GetBytes(json);
            var header = new byte[8];
            BitConverter.GetBytes(op).CopyTo(header, 0);
            BitConverter.GetBytes(payload.Length).CopyTo(header, 4);
            _pipe.Write(header, 0, 8);
            _pipe.Write(payload, 0, payload.Length);
            _pipe.Flush();
        }
    }
}
