using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>Quick connectivity checks for API keys configured in Settings.</summary>
public sealed class ApiTestService
{
    private readonly ISettingsService _settings;
    private readonly HttpClient _http;

    public ApiTestService(ISettingsService settings, HttpClient http)
    {
        _settings = settings;
        _http = http;
    }

    public async Task<string> TestSpotifyAsync(CancellationToken ct = default)
    {
        var id = _settings.Current.SpotifyClientId;
        var secret = _settings.Current.SpotifyClientSecret;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(secret))
            return "Spotify: укажите Client ID и Client Secret.";

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{id}:{secret}"));
        using var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
        };
        tokenReq.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var tokenResp = await _http.SendAsync(tokenReq, ct);
        var tokenBody = await tokenResp.Content.ReadAsStringAsync(ct);
        if (!tokenResp.IsSuccessStatusCode)
            return $"Spotify: неверные ключи (HTTP {(int)tokenResp.StatusCode}).";

        var token = JsonDocument.Parse(tokenBody).RootElement.GetProperty("access_token").GetString();
        using var searchReq = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/search?type=track&limit=1&q=test");
        searchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var searchResp = await _http.SendAsync(searchReq, ct);
        var searchBody = await searchResp.Content.ReadAsStringAsync(ct);

        if (searchResp.IsSuccessStatusCode)
            return "Spotify: OK — поиск работает.";

        if (searchBody.Contains("premium", StringComparison.OrdinalIgnoreCase))
            return "Spotify: ключи верные, но поиск заблокирован — нужен Spotify Premium у владельца приложения в developer.spotify.com.";

        return $"Spotify: поиск недоступен (HTTP {(int)searchResp.StatusCode}).";
    }

    public async Task<string> TestYouTubeAsync(CancellationToken ct = default)
    {
        var key = _settings.Current.YouTubeApiKey;
        if (string.IsNullOrWhiteSpace(key))
            return "YouTube: укажите API key (бесплатно в Google Cloud Console).";

        var url = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&maxResults=1&q=test&key={Uri.EscapeDataString(key)}";
        using var resp = await _http.GetAsync(url, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (resp.IsSuccessStatusCode)
            return "YouTube: OK — поиск работает.";

        if (body.Contains("API key not valid", StringComparison.OrdinalIgnoreCase))
            return "YouTube: неверный API key.";

        if (body.Contains("has not been used", StringComparison.OrdinalIgnoreCase) ||
            body.Contains("accessNotConfigured", StringComparison.OrdinalIgnoreCase))
            return "YouTube: включите YouTube Data API v3 в Google Cloud Console для этого проекта.";

        return $"YouTube: ошибка (HTTP {(int)resp.StatusCode}). Проверьте ключ и квоту.";
    }

    public async Task<string> TestSoundCloudAsync(CancellationToken ct = default)
    {
        var clientId = _settings.Current.SoundCloudClientId;
        if (string.IsNullOrWhiteSpace(clientId))
            return "SoundCloud: нужен Client ID, но для создания приложения требуется платный Artist Pro аккаунт. Используйте Deezer (без ключа).";

        var url = $"https://api-v2.soundcloud.com/search/tracks?q=test&limit=1&client_id={Uri.EscapeDataString(clientId)}";
        using var resp = await _http.GetAsync(url, ct);

        if (resp.IsSuccessStatusCode)
            return "SoundCloud: OK — поиск работает.";

        return $"SoundCloud: ошибка (HTTP {(int)resp.StatusCode}). Без Artist Pro создать приложение нельзя — используйте Deezer.";
    }

    public Task<string> TestDeezerAsync(CancellationToken ct = default)
        => Task.FromResult("Deezer: OK — работает без ключа (встроено).");
}
