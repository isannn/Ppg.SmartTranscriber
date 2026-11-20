namespace SmartTranscriber.Core.Services;

/// <summary>
/// Canonicalizes URLs for dedup: YouTube variants collapse to
/// <c>youtube.com/watch?v=&lt;id&gt;</c>; other hosts keep a lowercase,
/// tracking-stripped, param-sorted identity without scheme or fragment.
/// </summary>
public static class UrlNormalizer
{
    private static readonly HashSet<string> TrackingParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid", "gclid", "yclid", "igshid", "mc_cid", "mc_eid",
        "si", "feature", "pp"
    };

    public static string Normalize(string url)
    {
        var uri = new Uri(url.Trim());
        var host = uri.Host.ToLowerInvariant();
        var port = uri.IsDefaultPort ? "" : $":{uri.Port}";

        var ytId = ExtractYouTubeId(uri, host);
        if (ytId is not null)
        {
            return $"youtube.com/watch?v={ytId}";
        }

        var query = ParseQuery(uri.Query)
            .Where(kv => !IsTracking(kv.Key))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ThenBy(kv => kv.Value, StringComparer.Ordinal)
            .Select(kv => kv.Value is null ? kv.Key : $"{kv.Key}={kv.Value}");
        var qs = string.Join("&", query);

        var path = uri.AbsolutePath.TrimEnd('/');
        return $"{host}{port}{path}" + (qs.Length > 0 ? $"?{qs}" : "");
    }

    private static bool IsTracking(string key)
    {
        return key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) || TrackingParams.Contains(key);
    }

    private static string? ExtractYouTubeId(Uri uri, string host)
    {
        if (host is "youtu.be" or "www.youtu.be")
        {
            return FirstPathSegment(uri.AbsolutePath);
        }
        if (!host.EndsWith("youtube.com", StringComparison.Ordinal) &&
            !host.EndsWith("youtube-nocookie.com", StringComparison.Ordinal))
        {
            return null;
        }

        var path = uri.AbsolutePath;
        if (path.Equals("/watch", StringComparison.OrdinalIgnoreCase))
        {
            return ParseQuery(uri.Query).FirstOrDefault(kv => kv.Key == "v").Value;
        }
        var seg = FirstPathSegment(path);
        if (seg is "shorts" or "embed" or "live" or "v")
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[1] : null;
        }
        return null;
    }

    private static string? FirstPathSegment(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : null;
    }

    private static IEnumerable<(string Key, string? Value)> ParseQuery(string query)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                yield return (Uri.UnescapeDataString(pair), null);
            }
            else
            {
                yield return (Uri.UnescapeDataString(pair[..eq]), Uri.UnescapeDataString(pair[(eq + 1)..]));
            }
        }
    }
}
