using System.Net.Http;
using System.Text.Json;

namespace FishstrapV2.Core;

public class UpdateCheckResult
{
    public bool Success { get; set; }
    public string LatestVersion { get; set; } = "";
    public string CurrentVersion { get; set; } = "";
    public bool UpdateAvailable { get; set; }
    public string Url { get; set; } = "";
    public string Message { get; set; } = "";
}

public static class UpdaterService
{
    public static async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FishstrapV2-UpdateCheck");
            http.Timeout = TimeSpan.FromSeconds(15);

            using var resp = await http.GetAsync("https://api.github.com/repos/Mikeykorby/fishstrap-v2/releases/latest");
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new UpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = AppInfo.Version,
                    Message = "No releases have been published yet.",
                };
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var r = doc.RootElement;

            var tag = r.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var url = r.TryGetProperty("html_url", out var u) ? u.GetString() ?? AppInfo.RepoUrl : AppInfo.RepoUrl;

            var latest = tag.TrimStart('v', 'V');
            return new UpdateCheckResult
            {
                Success = true,
                LatestVersion = latest,
                CurrentVersion = AppInfo.Version,
                UpdateAvailable = IsNewer(latest, AppInfo.Version),
                Url = url,
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Update check failed", ex);
            return new UpdateCheckResult
            {
                Success = false,
                CurrentVersion = AppInfo.Version,
                Message = ex.Message,
            };
        }
    }

    private static bool IsNewer(string a, string b)
    {
        var pa = Parse(a);
        var pb = Parse(b);
        for (var i = 0; i < 4; i++)
        {
            if (pa[i] != pb[i])
                return pa[i] > pb[i];
        }
        return false;
    }

    private static int[] Parse(string version)
    {
        try
        {
            var parts = version.Split('.', '+', '-');
            var result = new int[4];
            for (var i = 0; i < Math.Min(4, parts.Length); i++)
                result[i] = int.TryParse(parts[i].Trim(), out var n) ? n : 0;
            return result;
        }
        catch
        {
            return new[] { 0, 0, 0, 0 };
        }
    }
}
