using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>
/// Searches YouTube via the official YouTube Data API v3 (free tier: ~10 000
/// units/day in Google Cloud Console). Results are informational; playback
/// opens youtube.com (no direct stream extraction).
/// </summary>
public sealed class YouTubeSearchService : IMusicSearchService
{
    private const string ApiBase = "https://www.googleapis.com/youtube/v3";
    private readonly ISettingsService _settings;
    private readonly HttpClient _http;

    public YouTubeSearchService(ISettingsService settings, HttpClient http)
    {
        _settings = settings;
        _http = http;
    }

    public MusicSource Source => MusicSource.YouTube;
    public string DisplayName => "YouTube";
    public string? LastError { get; private set; }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_settings.Current.YouTubeApiKey);

    public async Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (!IsAvailable || string.IsNullOrWhiteSpace(query))
            return Array.Empty<Track>();

        var key = _settings.Current.YouTubeApiKey!;
        // No videoCategoryId filter — it excluded many music uploads.
        var searchUrl =
            $"{ApiBase}/search?part=snippet&type=video&maxResults=20" +
            $"&q={Uri.EscapeDataString(query)}&key={Uri.EscapeDataString(key)}";

        try
        {
            using var searchResp = await _http.GetAsync(searchUrl, cancellationToken);
            var raw = await searchResp.Content.ReadAsStringAsync(cancellationToken);

            if (!searchResp.IsSuccessStatusCode)
            {
                if (raw.Contains("API key not valid", StringComparison.OrdinalIgnoreCase))
                    LastError = "YouTube: неверный API key.";
                else if (raw.Contains("accessNotConfigured", StringComparison.OrdinalIgnoreCase))
                    LastError = "YouTube: включите YouTube Data API v3 в Google Cloud Console.";
                else
                    LastError = $"YouTube: HTTP {(int)searchResp.StatusCode}";
                return Array.Empty<Track>();
            }

            var searchJson = JsonDocument.Parse(raw).RootElement;
            var tracks = new List<Track>();

            if (!searchJson.TryGetProperty("items", out var items))
                return tracks;

            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var id) ||
                    !id.TryGetProperty("videoId", out var videoIdEl))
                    continue;

                var videoId = videoIdEl.GetString() ?? string.Empty;
                var snippet = item.GetProperty("snippet");
                var title = snippet.GetProperty("title").GetString() ?? "Unknown";
                var channel = snippet.TryGetProperty("channelTitle", out var ch) ? ch.GetString() ?? "" : "";
                var thumb = ReadThumbnail(snippet);

                tracks.Add(new Track
                {
                    Title = System.Net.WebUtility.HtmlDecode(title),
                    ArtistName = channel,
                    Source = MusicSource.YouTube,
                    SourceId = videoId,
                    ThumbnailUrl = thumb,
                    StreamUrl = null
                });
            }

            return tracks;
        }
        catch (Exception ex)
        {
            LastError = $"YouTube: {ex.Message}";
            return Array.Empty<Track>();
        }
    }

    private static string? ReadThumbnail(JsonElement snippet)
    {
        if (!snippet.TryGetProperty("thumbnails", out var thumbs))
            return null;
        foreach (var size in new[] { "high", "medium", "default" })
        {
            if (thumbs.TryGetProperty(size, out var el) && el.TryGetProperty("url", out var url))
                return url.GetString();
        }
        return null;
    }

    public static string WatchUrl(Track track) => $"https://www.youtube.com/watch?v={track.SourceId}";
}
