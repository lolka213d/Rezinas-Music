using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>
/// Finds and resolves full-length progressive streams on SoundCloud by artist + title search.
/// </summary>
public sealed class SoundCloudStreamResolver
{
    private const string ApiBase = "https://api-v2.soundcloud.com";
    private const int MaxCandidates = 15;

    private readonly HttpClient _http;
    private readonly ISettingsService _settings;
    private readonly IAppLog _log;

    public SoundCloudStreamResolver(HttpClient http, ISettingsService settings, IAppLog log)
    {
        _http = http;
        _settings = settings;
        _log = log;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_settings.Current.SoundCloudClientId);

    public async Task<string?> TryResolveStreamAsync(
        string artist, string title, int durationSeconds, CancellationToken cancellationToken = default)
    {
        var clientId = _settings.Current.SoundCloudClientId;
        if (string.IsNullOrWhiteSpace(clientId)) return null;

        var queries = new List<string>();
        var trimmedArtist = artist.Trim();
        var trimmedTitle = title.Trim();
        if (trimmedArtist.Length > 0 && trimmedTitle.Length > 0)
        {
            queries.Add($"{trimmedArtist} {trimmedTitle}");
            queries.Add($"{trimmedArtist} - {trimmedTitle}");
        }
        if (trimmedTitle.Length > 0)
            queries.Add(trimmedTitle);

        string? bestUrl = null;
        var bestScore = -1;

        foreach (var query in queries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var items = await SearchTracksAsync(query, clientId, cancellationToken);
            foreach (var item in items)
            {
                var score = ScoreMatch(item.Title, item.Artist, item.DurationSeconds, trimmedArtist, trimmedTitle, durationSeconds);
                if (score <= 0 || score <= bestScore) continue;

                var streamUrl = await ResolveProgressiveStreamAsync(item.Json, clientId, cancellationToken);
                if (string.IsNullOrWhiteSpace(streamUrl)) continue;

                bestScore = score;
                bestUrl = streamUrl;
                _log.Info($"SoundCloud candidate (score {score}): {item.Artist} — {item.Title}");
            }

            if (bestUrl != null) break;
        }

        return bestUrl;
    }

    private async Task<List<ScTrack>> SearchTracksAsync(string query, string clientId, CancellationToken ct)
    {
        var list = new List<ScTrack>();
        try
        {
            var url = $"{ApiBase}/search/tracks?q={Uri.EscapeDataString(query)}&limit={MaxCandidates}&client_id={Uri.EscapeDataString(clientId)}";
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return list;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!json.TryGetProperty("collection", out var items)) return list;

            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("title", out var titleEl)) continue;

                var title = titleEl.GetString() ?? "";
                var artist = item.TryGetProperty("user", out var user) && user.TryGetProperty("username", out var un)
                    ? un.GetString() ?? ""
                    : "";
                var durationMs = item.TryGetProperty("duration", out var d) ? d.GetInt32() : 0;

                list.Add(new ScTrack(title, artist, durationMs / 1000, item));
            }
        }
        catch (Exception ex)
        {
            _log.Warning($"SoundCloud search failed for '{query}': {ex.Message}");
        }

        return list;
    }

    private async Task<string?> ResolveProgressiveStreamAsync(JsonElement track, string clientId, CancellationToken ct)
    {
        var progressiveUrl = FindProgressiveUrl(track);
        if (progressiveUrl == null) return null;

        try
        {
            var resolveUrl = $"{progressiveUrl}?client_id={Uri.EscapeDataString(clientId)}";
            using var resp = await _http.GetAsync(resolveUrl, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return json.TryGetProperty("url", out var u) ? u.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindProgressiveUrl(JsonElement track)
    {
        if (!track.TryGetProperty("media", out var media) ||
            !media.TryGetProperty("transcodings", out var transcodings))
            return null;

        foreach (var t in transcodings.EnumerateArray())
        {
            var protocol = t.TryGetProperty("format", out var fmt) && fmt.TryGetProperty("protocol", out var p)
                ? p.GetString()
                : null;
            if (protocol == "progressive" && t.TryGetProperty("url", out var u))
                return u.GetString();
        }

        return null;
    }

    private static int ScoreMatch(
        string videoTitle, string videoArtist, int videoDuration,
        string artist, string title, int durationSeconds)
    {
        var haystack = $"{Normalize(videoTitle)} {Normalize(videoArtist)}";
        if (haystack.Length == 0) return 0;

        var normTitle = Normalize(title);
        var normArtist = Normalize(artist);
        if (normTitle.Length == 0) return 0;

        var hasArtist = normArtist.Length == 0
            || haystack.Contains(normArtist, StringComparison.OrdinalIgnoreCase);
        var fullTitle = haystack.Contains(normTitle, StringComparison.OrdinalIgnoreCase);

        var tokens = normTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2).ToList();
        var titleHits = tokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
        var titleRatio = tokens.Count == 0 ? (fullTitle ? 1.0 : 0.0) : (double)titleHits / tokens.Count;

        if (!hasArtist && !fullTitle) return 0;
        if (titleRatio < 0.5) return 0;

        if (durationSeconds > 0 && videoDuration > 0)
        {
            var delta = Math.Abs(videoDuration - durationSeconds);
            var tolerance = Math.Clamp((int)Math.Round(durationSeconds * 0.25), 25, 60);
            if (delta > tolerance) return 0;
        }

        var score = (int)Math.Round(titleRatio * 40);
        if (fullTitle) score += 25;
        if (hasArtist) score += 20;

        if (durationSeconds > 0 && videoDuration > 0)
        {
            var delta = Math.Abs(videoDuration - durationSeconds);
            score += Math.Max(0, 30 - delta);
        }

        return score;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray();
        return new string(chars).ToLowerInvariant().Trim();
    }

    private sealed record ScTrack(string Title, string Artist, int DurationSeconds, JsonElement Json);
}
