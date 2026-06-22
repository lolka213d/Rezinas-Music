using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>
/// Lyrics from LRCLIB (free, synced LRC) with lyrics.ovh plain-text fallback.
/// Results are cached in memory for the session.
/// </summary>
public sealed class LyricsService : ILyricsService
{
    private const string LrcLibBase = "https://lrclib.net/api";
    private const string OvhBase = "https://api.lyrics.ovh/v1";
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, LyricsData?> _cache = new();

    public LyricsService(HttpClient http) => _http = http;

    public async Task<LyricsData?> GetLyricsAsync(
        string artist, string title, double trackDurationSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var cleanTitle = title.Split('(', '[')[0].Trim();
        var cleanArtist = artist.Split(',', '&')[0].Trim();
        var key = $"{cleanArtist}|{cleanTitle}".ToLowerInvariant();

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(18));
        var ct = timeoutCts.Token;

        // Run direct get + search in parallel — first hit wins.
        var getTask = TryLrcLibGetAsync(cleanArtist, cleanTitle, ct);
        var searchTask = TryLrcLibSearchAsync(cleanArtist, cleanTitle, ct);
        try
        {
            await Task.WhenAll(getTask, searchTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _cache.TryAdd(key, null);
            return null;
        }

        var synced = getTask.IsCompletedSuccessfully ? getTask.Result : null;
        synced ??= searchTask.IsCompletedSuccessfully ? searchTask.Result : null;
        if (synced != null)
        {
            _cache[key] = synced;
            return synced;
        }

        var plain = await TryOvhAsync(cleanArtist, cleanTitle, ct).ConfigureAwait(false);
        if (plain == null)
        {
            _cache[key] = null;
            return null;
        }

        var duration = trackDurationSeconds > 0 ? trackDurationSeconds : 180;
        var lines = LrcParser.DistributePlain(plain, duration);
        var data = new LyricsData(lines, isSynced: false, plainText: plain);
        _cache[key] = data;
        return data;
    }

    private async Task<LyricsData?> TryLrcLibGetAsync(string artist, string title, CancellationToken ct)
    {
        try
        {
            var url = $"{LrcLibBase}/get?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artist)}";
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            return ParseLrcLibResponse(await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct));
        }
        catch
        {
            return null;
        }
    }

    private async Task<LyricsData?> TryLrcLibSearchAsync(string artist, string title, CancellationToken ct)
    {
        try
        {
            var q = $"{artist} {title}".Trim();
            var url = $"{LrcLibBase}/search?q={Uri.EscapeDataString(q)}";
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            var results = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (results.ValueKind != JsonValueKind.Array)
                return null;

            LyricsData? best = null;
            var bestScore = -1;
            foreach (var item in results.EnumerateArray())
            {
                var itemArtist = item.TryGetProperty("artistName", out var a) ? a.GetString() ?? "" : "";
                var itemTitle = item.TryGetProperty("trackName", out var t) ? t.GetString() ?? "" : "";
                var score = ScoreMatch(artist, title, itemArtist, itemTitle);
                var parsed = ParseLrcLibResponse(item);
                if (parsed != null && score > bestScore)
                {
                    bestScore = score;
                    best = parsed;
                }
            }
            return best;
        }
        catch
        {
            return null;
        }
    }

    private static LyricsData? ParseLrcLibResponse(JsonElement json)
    {
        if (json.TryGetProperty("syncedLyrics", out var syncedEl))
        {
            var lrc = syncedEl.GetString();
            if (!string.IsNullOrWhiteSpace(lrc))
            {
                var lines = LrcParser.Parse(lrc);
                if (lines.Count > 0)
                    return new LyricsData(lines, isSynced: true, plainText: lrc);
            }
        }

        if (json.TryGetProperty("plainLyrics", out var plainEl))
        {
            var plain = plainEl.GetString();
            if (!string.IsNullOrWhiteSpace(plain))
            {
                var parts = plain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 0)
                {
                    var lines = parts.Select(p => new LyricLine(p, 0)).ToList();
                    return new LyricsData(lines, isSynced: false, plainText: plain);
                }
            }
        }

        return null;
    }

    private static int ScoreMatch(string wantArtist, string wantTitle, string artist, string title)
    {
        var score = 0;
        if (string.Equals(Norm(wantTitle), Norm(title), StringComparison.Ordinal)) score += 4;
        if (string.Equals(Norm(wantArtist), Norm(artist), StringComparison.Ordinal)) score += 3;
        if (Norm(title).Contains(Norm(wantTitle), StringComparison.Ordinal)) score += 1;
        return score;
    }

    private static string Norm(string s) => s.Trim().ToLowerInvariant();

    private async Task<string?> TryOvhAsync(string artist, string title, CancellationToken ct)
    {
        try
        {
            var url = $"{OvhBase}/{Uri.EscapeDataString(artist)}/{Uri.EscapeDataString(title)}";
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound || !resp.IsSuccessStatusCode)
                return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var lyrics = json.TryGetProperty("lyrics", out var l) ? l.GetString() : null;
            return string.IsNullOrWhiteSpace(lyrics) ? null : lyrics.Replace("\r\n", "\n").Trim();
        }
        catch
        {
            return null;
        }
    }
}
