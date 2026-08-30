using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace FishstrapV2.Core;

/// <summary>
/// Best-effort Discord Rich Presence: publishes SessionWatcher's live session
/// over the Discord IPC pipe. Fails silently when Discord is not running.
/// </summary>
public static class DiscordRpc
{
    public const string ClientId = "1055003141484658718";

    private static System.Timers.Timer? _pollTimer;
    private static NamedPipeClientStream? _pipe;
    private static bool _hasPresence;
    private static readonly object PipeLock = new();

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
        _hasPresence = false;
        Logger.Info("Discord RPC watcher stopped");
    }

    private static void Poll()
    {
        if (!SettingsStore.Settings.Integrations.DiscordRpc.Enabled)
            return;

        try
        {
            var s = SessionWatcher.Current;
            if (s is null)
            {
                if (_hasPresence)
                {
                    // No longer in game.
                    ClearPresence();
                    _hasPresence = false;
                }
                return;
            }

            EnsureConnected();
            PublishPresence(s);
            _hasPresence = true;
        }
        catch (Exception ex)
        {
            Logger.Warn("Discord RPC poll failed: " + ex.Message);
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

    private static void PublishPresence(SessionWatcher.Session s)
    {
        var rpc = SettingsStore.Settings.Integrations.DiscordRpc;
        var game = string.IsNullOrEmpty(s.GameName) ? "Roblox" : s.GameName;

        var details = (rpc.Details.Length > 0 ? rpc.Details : "Playing {game}").Replace("{game}", game);
        var state = rpc.State.Replace("{game}", game);

        var activity = new Dictionary<string, object?>
        {
            ["details"] = details,
            ["state"] = state,
            ["assets"] = new Dictionary<string, object?> { ["large_image"] = "roblox", ["large_text"] = "Roblox" },
        };

        if (rpc.ShowElapsedTime)
            activity["timestamps"] = new Dictionary<string, object?> { ["start"] = new DateTimeOffset(s.JoinedAt).ToUnixTimeSeconds() };

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
