using System.IO;

namespace FishstrapV2.Core;

public static class Logger
{
    private static readonly object Lock = new();

    public static void Info(string message) => Write("INFO ", message, null);
    public static void Warn(string message) => Write("WARN ", message, null);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Paths.LogsDir);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                if (ex is not null)
                    line += Environment.NewLine + ex;
                File.AppendAllText(
                    Path.Combine(Paths.LogsDir, $"fishstrap-{DateTime.Now:yyyyMMdd}.log"),
                    line + Environment.NewLine);
            }
        }
        catch
        {
            // never let logging break the app
        }

        System.Diagnostics.Debug.WriteLine($"[{level.Trim()}] {message}");
    }
}
