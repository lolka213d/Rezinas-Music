using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>
/// Searches SoundCloud via its official API v2. Requires a free client_id from
/// https://developers.soundcloud.com/ (Register app → Client ID).
/// </summary>
public sealed class SoundCloudSearchService : IMusicSearchService
{
    private const string ApiBase = "https://api-v2.soundcloud.com";

    private readonly ISettingsService _settings;
    private readonly HttpClient _http;

    public SoundCloudSearchService(ISettingsService settings, HttpClient http)
    {
        _settings = settings;
        _http = http;
    }

    public MusicSource Source => MusicSource.SoundCloud;
    public string DisplayName => "SoundCloud";
    public string? LastError { get; private set; }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_settings.Current.SoundCloudClientId);

    public async Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (!IsAvailable || string.IsNullOrWhiteSpace(query))
            return Array.Empty<Track>();

        var clientId = _settings.Current.SoundCloudClientId!;
        var url = $"{ApiBase}/search/tracks?q={Uri.EscapeDataString(query)}&limit=20&client_id={Uri.EscapeDataString(clientId)}";

        try
        {
            using var resp = await _http.GetAsync(url, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                LastError = resp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "SoundCloud: неверный Client ID. Получите бесплатный на developers.soundcloud.com"
                    : $"SoundCloud: HTTP {(int)resp.StatusCode}";
                return Array.Empty<Track>();
            }

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!json.TryGetProperty("collection", out var items))
                return Array.Empty<Track>();

            var tracks = new List<Track>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("title", out var titleEl)) continue;

                var title = titleEl.GetString() ?? "Unknown";
                var artist = item.TryGetProperty("user", out var user) && user.TryGetProperty("username", out var un)
                    ? un.GetString() ?? ""
                    : "";
                var durationMs = item.TryGetProperty("duration", out var d) ? d.GetInt32() : 0;
                var artwork = item.TryGetProperty("artwork_url", out var aw) ? aw.GetString() : null;
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : "";

                var progressiveUrl = FindProgressiveUrl(item);
                string? streamUrl = null;
                if (progressiveUrl != null)
                    streamUrl = await ResolveStreamAsync(progressiveUrl, clientId, cancellationToken);

                tracks.Add(new Track
                {
                    Title = title,
                    ArtistName = artist,
                    DurationSeconds = durationMs / 1000,
                    Source = MusicSource.SoundCloud,
                    SourceId = id,
                    ThumbnailUrl = artwork,
                    StreamUrl = streamUrl
                });
            }

            return tracks;
        }
        catch (Exception ex)
        {
            LastError = $"SoundCloud: {ex.Message}";
            return Array.Empty<Track>();
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

    private async Task<string?> ResolveStreamAsync(string transcodingUrl, string clientId, CancellationToken ct)
    {
        try
        {
            var url = $"{transcodingUrl}?client_id={Uri.EscapeDataString(clientId)}";
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return json.TryGetProperty("url", out var u) ? u.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
