using System.Reflection;

namespace FishstrapV2.Core;

public static class AppInfo
{
    public const string ProductName = "Fishstrap V2";

    public static string Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "3.0.4" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public static string ExePath
    {
        get
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                       ?? Assembly.GetExecutingAssembly().Location;
            }
            catch
            {
                return Assembly.GetExecutingAssembly().Location;
            }
        }
    }

    public const string RepoUrl = "https://github.com/Mikeykorby/fishstrap-v2";
    public const string IssuesUrl = RepoUrl + "/issues";
    public const string WebsiteUrl = "https://www.fishstrap.app";
    public const string InviteBase = "https://www.fishstrap.app/v1/joingame?placeId=";
}
