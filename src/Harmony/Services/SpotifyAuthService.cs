using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Harmony.Config;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>Spotify OAuth 2.0 Authorization Code with PKCE for desktop login.</summary>
public sealed class SpotifyAuthService
{
    public const string RedirectUri = "http://127.0.0.1:4567/callback";
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";
    private const string Scopes = "playlist-read-private playlist-read-collaborative user-library-read user-read-email";

    private readonly ISettingsService _settings;
    private readonly HttpClient _http;
    private readonly IAppLog _log;

    public SpotifyAuthService(ISettingsService settings, HttpClient http, IAppLog log)
    {
        _settings = settings;
        _http = http;
        _log = log;
    }

    public bool IsConnected =>
        !string.IsNullOrWhiteSpace(_settings.Current.SpotifyRefreshToken);

    public async Task<(bool Success, string Message)> ConnectAsync(CancellationToken ct = default)
    {
        var (clientId, clientSecret) = SpotifyCredentials.Resolve(_settings);
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return (false, "Spotify login is not configured in this build.");

        return await Task.Run(async () =>
            await ConnectCoreAsync(clientId, clientSecret, ct).ConfigureAwait(false), ct).ConfigureAwait(true);
    }

    private async Task<(bool Success, string Message)> ConnectCoreAsync(
        string clientId, string clientSecret, CancellationToken ct)
    {
        if (!await ValidateAppCredentialsAsync(clientId, clientSecret, ct).ConfigureAwait(false))
        {
            return (false,
                "Spotify отклонил Client ID / Secret (Invalid client). " +
                "Открой developer.spotify.com/dashboard → своё приложение → Settings, " +
                "скопируй Client ID и Client Secret в src/Harmony/Config/SpotifyAppCredentials.cs и перезапусти приложение.");
        }

        var verifier = GenerateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = Guid.NewGuid().ToString("N");

        using var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:4567/");
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            _log.Error("Spotify OAuth listener failed", ex);
            return (false, "Could not start local callback server on port 4567. Close other apps using this port and try again.");
        }

        try
        {
            var authUrl =
                $"{AuthorizeUrl}?client_id={Uri.EscapeDataString(clientId)}" +
                $"&response_type=code&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                $"&scope={Uri.EscapeDataString(Scopes)}" +
                $"&code_challenge={Uri.EscapeDataString(challenge)}&code_challenge_method=S256" +
                $"&state={Uri.EscapeDataString(state)}";

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(3));

            HttpListenerContext context;
            while (true)
            {
                context = await listener.GetContextAsync().WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                var path = context.Request.Url?.AbsolutePath ?? string.Empty;
                if (path.StartsWith("/callback", StringComparison.OrdinalIgnoreCase))
                    break;

                await WriteBrowserResponseAsync(context.Response, false).ConfigureAwait(false);
            }

            var req = context.Request;
            var code = req.QueryString["code"];
            var returnedState = req.QueryString["state"];
            var error = req.QueryString["error"];

            await WriteBrowserResponseAsync(context.Response, string.IsNullOrWhiteSpace(error)).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error))
                return (false, $"Spotify login cancelled: {error}");

            if (string.IsNullOrWhiteSpace(code) || returnedState != state)
                return (false, "Spotify login failed — invalid callback.");

            var tokens = await ExchangeCodeAsync(clientId, clientSecret, code, verifier, ct).ConfigureAwait(false);
            if (tokens == null)
                return (false, "Could not exchange Spotify authorization code.");

            var profile = await FetchProfileAsync(tokens.AccessToken, ct).ConfigureAwait(false);

            var s = _settings.Current;
            s.SpotifyRefreshToken = tokens.RefreshToken ?? s.SpotifyRefreshToken;
            s.SpotifyAccessToken = tokens.AccessToken;
            s.SpotifyTokenExpiresUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn - 60);
            s.SpotifyUserId = profile?.Id;
            s.SpotifyDisplayName = profile?.DisplayName ?? profile?.Id ?? "Spotify";
            s.SpotifyConnectedAt ??= DateTime.UtcNow;
            s.SpotifyAutoSyncEnabled = true;
            await _settings.SaveAsync(s).ConfigureAwait(false);

            _log.Info($"Spotify connected: {s.SpotifyDisplayName}");
            return (true, $"Connected as {s.SpotifyDisplayName}");
        }
        catch (OperationCanceledException)
        {
            return (false, "Spotify login timed out — complete login in the browser within 3 minutes.");
        }
        finally
        {
            listener.Stop();
        }
    }

    public async Task DisconnectAsync()
    {
        var s = _settings.Current;
        s.SpotifyRefreshToken = null;
        s.SpotifyAccessToken = null;
        s.SpotifyTokenExpiresUtc = null;
        s.SpotifyUserId = null;
        s.SpotifyDisplayName = null;
        s.SpotifyConnectedAt = null;
        s.SpotifyAutoSyncEnabled = false;
        await _settings.SaveAsync(s);
    }

    public async Task<string?> GetUserAccessTokenAsync(CancellationToken ct = default)
    {
        if (!IsConnected) return null;

        var s = _settings.Current;
        if (!string.IsNullOrWhiteSpace(s.SpotifyAccessToken)
            && s.SpotifyTokenExpiresUtc.HasValue
            && DateTime.UtcNow < s.SpotifyTokenExpiresUtc.Value)
            return s.SpotifyAccessToken;

        var refreshed = await RefreshTokenAsync(ct);
        return refreshed;
    }

    private async Task<bool> ValidateAppCredentialsAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        try
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials"
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            using var resp = await _http.SendAsync(request, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _log.Warning($"Spotify credential check failed: {ex.Message}");
            return false;
        }
    }

    private async Task<string?> RefreshTokenAsync(CancellationToken ct)
    {
        var s = _settings.Current;
        var (clientId, clientSecret) = SpotifyCredentials.Resolve(_settings);
        var refresh = s.SpotifyRefreshToken;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(refresh))
            return null;

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refresh
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var resp = await _http.SendAsync(request, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var access = json.GetProperty("access_token").GetString();
        var expiresIn = json.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
        if (json.TryGetProperty("refresh_token", out var rt))
        {
            var newRefresh = rt.GetString();
            if (!string.IsNullOrWhiteSpace(newRefresh))
                s.SpotifyRefreshToken = newRefresh;
        }

        s.SpotifyAccessToken = access;
        s.SpotifyTokenExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        await _settings.SaveAsync(s);
        return access;
    }

    private async Task<TokenResponse?> ExchangeCodeAsync(
        string clientId, string clientSecret, string code, string verifier, CancellationToken ct)
    {
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = verifier
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var resp = await _http.SendAsync(request, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return new TokenResponse
        {
            AccessToken = json.GetProperty("access_token").GetString() ?? "",
            RefreshToken = json.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
            ExpiresIn = json.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 3600
        };
    }

    private async Task<SpotifyProfile?> FetchProfileAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await _http.SendAsync(request, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return new SpotifyProfile
        {
            Id = json.TryGetProperty("id", out var id) ? id.GetString() : null,
            DisplayName = json.TryGetProperty("display_name", out var dn) ? dn.GetString() : null
        };
    }

    private static async Task WriteBrowserResponseAsync(HttpListenerResponse response, bool success)
    {
        var html = success
            ? "<html><body style='font-family:sans-serif;text-align:center;padding:40px;background:#121212;color:#fff'><h2>Rezinas Music</h2><p>Spotify connected. You can close this tab.</p></body></html>"
            : "<html><body style='font-family:sans-serif;text-align:center;padding:40px'><p>Login failed. Close this tab and try again.</p></body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.OutputStream.Close();
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TokenResponse
    {
        public required string AccessToken { get; init; }
        public string? RefreshToken { get; init; }
        public int ExpiresIn { get; init; }
    }

    private sealed class SpotifyProfile
    {
        public string? Id { get; init; }
        public string? DisplayName { get; init; }
    }
}
