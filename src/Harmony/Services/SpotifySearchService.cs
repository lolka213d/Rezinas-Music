using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>
/// Searches the Spotify Web API using the Client Credentials flow.
/// NOTE (2024+): Spotify requires a Premium subscription on the developer
/// account for search endpoints. Without Premium the token is issued but search
/// returns HTTP 403.
/// </summary>
public sealed class SpotifySearchService : IMusicSearchService
{
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string ApiBase = "https://api.spotify.com/v1";

    private readonly ISettingsService _settings;
    private readonly HttpClient _http;

    private string? _token;
    private DateTime _tokenExpiresUtc;

    public SpotifySearchService(ISettingsService settings, HttpClient http)
    {
        _settings = settings;
        _http = http;
        _settings.SettingsChanged += (_, _) => { _token = null; _tokenExpiresUtc = DateTime.MinValue; };
    }

    public MusicSource Source => MusicSource.Spotify;
    public string DisplayName => "Spotify";
    public string? LastError { get; private set; }

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(_settings.Current.SpotifyClientId) &&
        !string.IsNullOrWhiteSpace(_settings.Current.SpotifyClientSecret);

    public async Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (!IsAvailable || string.IsNullOrWhiteSpace(query))
            return Array.Empty<Track>();

        var token = await GetTokenAsync(cancellationToken);
        if (token is null)
        {
            LastError ??= "Spotify: не удалось получить токен — проверьте Client ID и Secret.";
            return Array.Empty<Track>();
        }

        var url = $"{ApiBase}/search?type=track&limit=20&q={Uri.EscapeDataString(query)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await _http.SendAsync(request, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden &&
                body.Contains("premium", StringComparison.OrdinalIgnoreCase))
            {
                LastError = "Spotify: нужен Spotify Premium у владельца приложения (developer.spotify.com). Используйте Deezer — он работает без ключа.";
            }
            else
            {
                LastError = $"Spotify: HTTP {(int)resp.StatusCode}";
            }
            return Array.Empty<Track>();
        }

        var json = JsonDocument.Parse(body).RootElement;
        var tracks = new List<Track>();

        if (!json.TryGetProperty("tracks", out var tracksEl) ||
            !tracksEl.TryGetProperty("items", out var items))
            return tracks;

        foreach (var item in items.EnumerateArray())
        {
            var title = item.GetProperty("name").GetString() ?? "Unknown";
            var artist = item.TryGetProperty("artists", out var artistsEl) && artistsEl.GetArrayLength() > 0
                ? artistsEl[0].GetProperty("name").GetString() ?? ""
                : "";
            var album = item.TryGetProperty("album", out var albumEl)
                ? albumEl.GetProperty("name").GetString()
                : null;
            string? cover = null;
            if (item.TryGetProperty("album", out var al) &&
                al.TryGetProperty("images", out var imgs) && imgs.GetArrayLength() > 0)
            {
                cover = imgs[0].GetProperty("url").GetString();
            }
            var durationMs = item.TryGetProperty("duration_ms", out var d) ? d.GetInt32() : 0;
            var preview = item.TryGetProperty("preview_url", out var p) ? p.GetString() : null;

            tracks.Add(new Track
            {
                Title = title,
                ArtistName = artist,
                AlbumName = album,
                DurationSeconds = durationMs / 1000,
                Source = MusicSource.Spotify,
                SourceId = item.GetProperty("id").GetString() ?? string.Empty,
                ThumbnailUrl = cover,
                StreamUrl = preview
            });
        }

        return tracks;
    }

    private async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_token != null && DateTime.UtcNow < _tokenExpiresUtc)
            return _token;

        var id = _settings.Current.SpotifyClientId!;
        var secret = _settings.Current.SpotifyClientSecret!;
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var resp = await _http.SendAsync(request, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            LastError = "Spotify: неверный Client ID или Client Secret.";
            return null;
        }

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        _token = json.GetProperty("access_token").GetString();
        var expiresIn = json.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
        _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        return _token;
    }
}
