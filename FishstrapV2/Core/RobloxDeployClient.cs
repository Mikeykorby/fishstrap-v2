using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace FishstrapV2.Core;

public record RobloxVersionInfo(string Version, string VersionHash);
public record DeployPackage(string Name, string Md5, long Size);

/// <summary>
/// Talks to the current Roblox deployment APIs:
///  - version info:  https://clientsettingscdn.roblox.com/v2/client-version/{binaryType}[/channel/{name}]
///    (no channel segment for the default channel; falls back to clientsettings.roblox.com)
///  - package list:  https://{mirror}/{hash}-rbxPkgManifest.txt  (plain text, "v0" header, 4 lines per package)
///  - packages:      https://{mirror}/{hash}-{packageName}
/// </summary>
public static class RobloxDeployClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>Roblox deployment mirrors, in preference order.</summary>
    private static readonly string[] Mirrors =
    {
        "https://setup.rbxcdn.com",
        "https://setup-aws.rbxcdn.com",
        "https://setup-ak.rbxcdn.com",
        "https://roblox-setup.cachefly.net",
        "https://s3.amazonaws.com/setup.roblox.com",
    };

    private static readonly string[] VersionApiHosts =
    {
        "https://clientsettingscdn.roblox.com",
        "https://clientsettings.roblox.com",
    };

    static RobloxDeployClient()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd($"FishstrapV2/{AppInfo.Version}");
    }

    /// <summary>
    /// Where each deployment package extracts to, relative to the version directory
    /// (mirrors Roblox's own installer layout — content packages are zipped flat and
    /// must land in their mapped subfolder).
    /// </summary>
    private static readonly Dictionary<string, string> PackageFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RobloxApp.zip"] = "",
        ["RobloxStudio.zip"] = "",
        ["Libraries.zip"] = "",
        ["LibrariesQt5.zip"] = "",
        ["redist.zip"] = "",
        ["WebView2.zip"] = "",
        ["WebView2RuntimeInstaller.zip"] = "WebView2RuntimeInstaller",
        ["shaders.zip"] = "shaders",
        ["ssl.zip"] = "ssl",
        ["content-avatar.zip"] = "content/avatar",
        ["content-configs.zip"] = "content/configs",
        ["content-fonts.zip"] = "content/fonts",
        ["content-models.zip"] = "content/models",
        ["content-sky.zip"] = "content/sky",
        ["content-sounds.zip"] = "content/sounds",
        ["content-textures2.zip"] = "content/textures",
        ["content-textures3.zip"] = "PlatformContent/pc/textures",
        ["content-terrain.zip"] = "PlatformContent/pc/terrain",
        ["content-platform-fonts.zip"] = "PlatformContent/pc/fonts",
        ["content-platform-dictionaries.zip"] = "PlatformContent/pc/shared_compression_dictionaries",
        ["content-studio_svg_textures.zip"] = "content/studio_svg_textures",
        ["content-qt_translations.zip"] = "content/qt_translations",
        ["content-api-docs.zip"] = "content/api_docs",
        ["extracontent-places.zip"] = "ExtraContent/places",
        ["extracontent-luapackages.zip"] = "ExtraContent/LuaPackages",
        ["extracontent-translations.zip"] = "ExtraContent/translations",
        ["extracontent-models.zip"] = "ExtraContent/models",
        ["extracontent-textures.zip"] = "ExtraContent/textures",
        ["extracontent-scripts.zip"] = "ExtraContent/scripts",
        ["studiocontent-models.zip"] = "StudioContent/models",
        ["studiocontent-textures.zip"] = "StudioContent/textures",
        ["BuiltInPlugins.zip"] = "BuiltInPlugins",
        ["BuiltInStandalonePlugins.zip"] = "BuiltInStandalonePlugins",
        ["ApplicationConfig.zip"] = "ApplicationConfig",
        ["Plugins.zip"] = "Plugins",
        ["Qml.zip"] = "Qml",
        ["StudioFonts.zip"] = "StudioFonts",
        ["RibbonConfig.zip"] = "RibbonConfig",
    };

    private static string GetPackageFolder(string name)
    {
        if (PackageFolders.TryGetValue(name, out var folder))
            return folder;

        // Derive a sane target for packages Roblox adds after this table was written.
        var stem = name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        foreach (var (prefix, root) in new[] { ("content-", "content"), ("extracontent-", "ExtraContent"), ("studiocontent-", "StudioContent") })
        {
            if (stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var suffix = stem[prefix.Length..].TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                Logger.Warn($"Unknown package {name} — deriving target folder {root}/{suffix}");
                return $"{root}/{suffix}";
            }
        }

        Logger.Warn($"Unknown package {name} — extracting to the version root");
        return "";
    }

    /// <summary>Picks the preferred setup mirror for the chosen download region.</summary>
    public static string GetHost(string serverLocation) => serverLocation switch
    {
        "AWS US East (N. Virginia)" => Mirrors[1],
        "AWS EU (Ireland)" or "AWS Asia Pacific (Tokyo)" => Mirrors[2],
        _ => Mirrors[0],
    };

    public static async Task<RobloxVersionInfo?> GetLatestVersionAsync(string channel, string binaryType)
    {
        bool isDefault = channel is "production" or "live";
        var path = $"v2/client-version/{binaryType}";
        if (!isDefault)
            path += $"/channel/{Uri.EscapeDataString(channel)}";

        foreach (var host in VersionApiHosts)
        {
            try
            {
                using var resp = await Http.GetAsync($"{host}/{path}");
                if (!resp.IsSuccessStatusCode)
                {
                    Logger.Warn($"Deploy API {host}/{path} returned {(int)resp.StatusCode}");
                    if (resp.StatusCode is HttpStatusCode.Unauthorized
                        or HttpStatusCode.Forbidden
                        or HttpStatusCode.NotFound)
                        return null; // channel is invalid or private — no point retrying the fallback host
                    continue;
                }

                await using var stream = await resp.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                var r = doc.RootElement;

                var hash = r.TryGetProperty("clientVersionUpload", out var cvu) ? cvu.GetString() : null;
                if (string.IsNullOrEmpty(hash))
                    continue;

                var version = r.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                return new RobloxVersionInfo(version, hash!);
            }
            catch (Exception ex)
            {
                Logger.Error($"Deploy API request failed ({host}, {channel}/{binaryType})", ex);
            }
        }

        return null;
    }

    /// <summary>
    /// Fetches the plain-text package manifest ({hash}-rbxPkgManifest.txt).
    /// Format: "v0", then 4 lines per package: name, md5, compressed size, uncompressed size.
    /// Tries the preferred mirror first, then rotates through the rest.
    /// </summary>
    public static async Task<List<DeployPackage>?> GetManifestAsync(string hash, string? preferredHost = null)
    {
        var hosts = new List<string>();
        if (!string.IsNullOrEmpty(preferredHost))
            hosts.Add(preferredHost);
        hosts.AddRange(Mirrors.Where(m => m != preferredHost));

        foreach (var host in hosts)
        {
            try
            {
                using var resp = await Http.GetAsync($"{host}/{hash}-rbxPkgManifest.txt");
                if (!resp.IsSuccessStatusCode)
                    continue;

                var lines = (await resp.Content.ReadAsStringAsync())
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (lines.Length < 5 || lines[0] != "v0")
                {
                    Logger.Warn($"Unexpected manifest format from {host}");
                    continue;
                }

                var packages = new List<DeployPackage>();
                for (int i = 1; i + 3 < lines.Length; i += 4)
                {
                    if (!lines[i].EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        continue; // skip standalone installer executables

                    packages.Add(new DeployPackage(
                        lines[i],
                        lines[i + 1],
                        long.Parse(lines[i + 2], CultureInfo.InvariantCulture)));
                }

                if (packages.Count > 0)
                {
                    Logger.Info($"Got manifest for {hash} from {host} ({packages.Count} packages)");
                    return packages;
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Manifest fetch failed on {host}: {ex.Message}");
            }
        }

        return null;
    }

    public static async Task DownloadVersionAsync(
        string hash, string targetDir, IProgress<string>? progress, CancellationToken ct = default)
    {
        var host = GetHost(SettingsStore.Settings.Deployment.ServerLocation);
        var packages = await GetManifestAsync(hash, host)
            ?? throw new InvalidOperationException("Could not fetch the package manifest for this Roblox version.");

        Directory.CreateDirectory(Paths.DownloadsDir);
        Directory.CreateDirectory(targetDir);

        long totalBytes = packages.Sum(p => p.Size);
        long doneBytes = 0;

        var status = progress as IProgressHook;
        status?.Dialog?.StartDeterminate(totalBytes);

        foreach (var pkg in packages)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Downloading {pkg.Name}…");

            var tmpZip = Path.Combine(Paths.DownloadsDir, $"{Guid.NewGuid():N}.{pkg.Name}");
            var md5 = await DownloadToFileAsync($"{host}/{hash}-{pkg.Name}", tmpZip, progress, ct, doneBytes, totalBytes, pkg.Name, status);

            if (!md5.Equals(pkg.Md5, StringComparison.OrdinalIgnoreCase))
            {
                var mirror = Mirrors.First(m => m != host);
                progress?.Report($"{pkg.Name} failed its checksum — retrying from {mirror}…");
                md5 = await DownloadToFileAsync($"{mirror}/{hash}-{pkg.Name}", tmpZip, progress, ct, doneBytes, totalBytes, pkg.Name);
                if (!md5.Equals(pkg.Md5, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Checksum mismatch for {pkg.Name} — the download may be corrupted.");
            }

            doneBytes += pkg.Size;

            progress?.Report($"Extracting {pkg.Name}…");
            ExtractZipSafely(tmpZip, Path.Combine(targetDir, GetPackageFolder(pkg.Name)));
            File.Delete(tmpZip);
        }

        await EnsureWebView2RuntimeAsync(hash, packages, targetDir, progress, ct);
    }

    /// <summary>
    /// Roblox's client needs the WebView2 runtime. If it is missing, bootstrap it from the
    /// WebView2RuntimeInstaller package that ships in the deployment manifest (like Fishstrap).
    /// </summary>
    private static async Task EnsureWebView2RuntimeAsync(
        string hash, List<DeployPackage> packages, string versionDir, IProgress<string>? progress, CancellationToken ct)
    {
        const string clientGuid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

        foreach (var root in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
        foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
        {
            using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(root, view);
            using var key = baseKey.OpenSubKey($@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{clientGuid}");
            if (key?.GetValue("pv") is string pv && pv.Length > 1)
                return; // runtime is already installed
        }

        var installer = packages.FirstOrDefault(p => p.Name.Equals("WebView2RuntimeInstaller.zip", StringComparison.OrdinalIgnoreCase));
        if (installer is null)
        {
            Logger.Warn("WebView2 runtime is missing and this Roblox version ships no WebView2RuntimeInstaller package");
            return;
        }

        progress?.Report("Installing the WebView2 runtime…");
        Logger.Info("WebView2 runtime not found — installing from the deployment package");

        var host = GetHost(SettingsStore.Settings.Deployment.ServerLocation);
        var pkgDir = Path.Combine(versionDir, "WebView2RuntimeInstaller");
        var tmpZip = Path.Combine(Paths.DownloadsDir, $"{Guid.NewGuid():N}.{installer.Name}");

        try
        {
            Directory.CreateDirectory(pkgDir);
            Directory.CreateDirectory(Paths.DownloadsDir);

            var md5 = await DownloadToFileAsync($"{host}/{hash}-{installer.Name}", tmpZip, null, ct);
            if (!md5.Equals(installer.Md5, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Checksum mismatch for WebView2RuntimeInstaller.zip");

            ExtractZipSafely(tmpZip, pkgDir);

            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(pkgDir, "MicrosoftEdgeWebview2Setup.exe"),
                Arguments = "/silent /install",
                UseShellExecute = true,
            });
            if (proc is not null)
                await proc.WaitForExitAsync(ct);

            Logger.Info("WebView2 runtime install finished");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Logger.Error("WebView2 runtime install failed", ex);
        }
        finally
        {
            try { File.Delete(tmpZip); } catch { }
            try { Directory.Delete(pkgDir, true); } catch { }
        }
    }

    /// <summary>
    /// Extracts a Roblox package zip, sanitizing entry names. Roblox zips contain a bare
    /// "/" directory entry and Windows-style separators that make the built-in
    /// ExtractToDirectory throw ("entry would have resulted in a file outside ...").
    /// </summary>
    private static void ExtractZipSafely(string zipPath, string targetDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace(Path.DirectorySeparatorChar, '/').TrimStart('/');
            var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue; // bare directory entries like "/"

            var clean = string.Join('/', parts.Where(p => p != "." && p != ".."));
            if (clean.Length == 0)
                continue;

            var dest = Path.Combine(targetDir, clean);

            if (name.EndsWith("/"))
            {
                Directory.CreateDirectory(dest);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, true);
        }
    }

    /// <summary>Lets a progress callback also drive the bootstrapper dialog's byte-level progress bar.</summary>
    public interface IProgressHook
    {
        UI.BootstrapperDialog? Dialog { get; }
    }

    private static async Task<string> DownloadToFileAsync(
        string url, string dest, IProgress<string>? progress, CancellationToken ct,
        long doneBytes = 0, long totalBytes = 0, string label = "",
        IProgressHook? status = null)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var source = await resp.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(dest);

        using var md5 = MD5.Create();
        var buffer = new byte[81920];
        long written = 0;
        int read;
        var lastReport = DateTime.MinValue;

        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            md5.TransformBlock(buffer, 0, read, null, 0);
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            written += read;

            if ((DateTime.Now - lastReport).TotalMilliseconds > 300)
            {
                lastReport = DateTime.Now;
                var done = doneBytes + written;
                var total = totalBytes > 0 ? totalBytes : doneBytes + (resp.Content.Headers.ContentLength ?? 0);
                progress?.Report(total > 0
                    ? $"Downloading {label}… {done / 1024 / 1024} / {total / 1024 / 1024} MB"
                    : $"Downloading {label}… {done / 1024 / 1024} MB");
                if (totalBytes > 0)
                    status?.Dialog?.ReportBytes(done, totalBytes);
            }
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(md5.Hash!);
    }
}
