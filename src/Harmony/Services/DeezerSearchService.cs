using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>
/// Searches Deezer via the free public API (no key required).
/// Returns metadata, cover art and 30-second preview streams when available.
/// </summary>
public sealed class DeezerSearchService : IMusicSearchService
{
    private const string ApiBase = "https://api.deezer.com";
    private readonly HttpClient _http;

    public DeezerSearchService(HttpClient http) => _http = http;

    public MusicSource Source => MusicSource.Deezer;
    public string DisplayName => "Deezer";
    public bool IsAvailable => true;
    public string? LastError { get; private set; }

    public async Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<Track>();

        var url = $"{ApiBase}/search?q={Uri.EscapeDataString(query.Trim())}&limit=50";
        try
        {
            using var resp = await _http.GetAsync(url, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                LastError = $"Deezer: HTTP {(int)resp.StatusCode}";
                return Array.Empty<Track>();
            }

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!json.TryGetProperty("data", out var data))
                return Array.Empty<Track>();

            var tracks = new List<Track>();
            foreach (var item in data.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "Unknown" : "Unknown";
                var artist = item.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var an)
                    ? an.GetString() ?? ""
                    : "";
                var album = item.TryGetProperty("album", out var al) && al.TryGetProperty("title", out var aln)
                    ? aln.GetString()
                    : null;
                string? cover = null;
                if (item.TryGetProperty("album", out var alb) && alb.TryGetProperty("cover_medium", out var cov))
                    cover = cov.GetString();
                var duration = item.TryGetProperty("duration", out var d) ? d.GetInt32() : 0;
                var preview = item.TryGetProperty("preview", out var p) ? p.GetString() : null;
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : "";

                tracks.Add(new Track
                {
                    Title = title,
                    ArtistName = artist,
                    AlbumName = album,
                    DurationSeconds = duration,
                    Source = MusicSource.Deezer,
                    SourceId = id,
                    ThumbnailUrl = cover,
                    StreamUrl = string.IsNullOrWhiteSpace(preview) ? null : preview
                });
            }

            return tracks;
        }
        catch (Exception ex)
        {
            LastError = $"Deezer: {ex.Message}";
            return Array.Empty<Track>();
        }
    }
}
