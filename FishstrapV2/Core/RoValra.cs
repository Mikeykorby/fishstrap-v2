using System.Net.Http;
using System.Text.Json;

namespace FishstrapV2.Core;

/// <summary>
/// RoValra server-information lookups (fishstrap parity). Every call fails
/// soft: any error or unknown IP returns null, never throws. Only the UI
/// queries this — never the launch path.
/// </summary>
public static class RoValra
{
    private static readonly Dictionary<string, string> Cache = new(); // ip -> "" when no info
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<string?> GetServerLocationAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.StartsWith("10."))
            return null; // private addresses have no location

        lock (Cache)
        {
            if (Cache.TryGetValue(address, out var hit))
                return hit.Length > 0 ? hit : null;
        }

        await Gate.WaitAsync();
        try
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("FishstrapV2");
                var json = await http.GetStringAsync($"https://apis.rovalra.com/v1/geolocation?ip={address}");
                using var doc = JsonDocument.Parse(json);

                string? result = null;
                if (doc.RootElement.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object)
                {
                    var city = GetString(loc, "city");
                    var region = GetString(loc, "region");
                    var country = GetString(loc, "country_name");

                    // Same collapsing rule fishstrap uses.
                    if (city == region && city == country)
                        result = country;
                    else if (city == region)
                        result = $"{region}, {country}";
                    else
                        result = $"{city}, {region}, {country}";
                }

                lock (Cache) Cache[address] = result ?? "";
                return result;
            }
            catch (Exception ex)
            {
                Logger.Warn($"RoValra location lookup failed for {address}: {ex.Message}");
                lock (Cache) Cache[address] = ""; // don't re-query a dead endpoint every poll
                return null;
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
