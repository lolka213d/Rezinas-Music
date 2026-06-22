using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

public sealed record SpotifySyncResult(
    int PlaylistsSynced,
    int TracksImported,
    int LikedImported,
    string? Error = null);

/// <summary>Imports Spotify playlists and liked songs into the local library.</summary>
public sealed class SpotifyLibrarySyncService
{
    private const string ApiBase = "https://api.spotify.com/v1";
    private const string LikedPlaylistExternalId = "__spotify_liked__";
    private const string MarketQuery = "market=from_token";

    private readonly SpotifyAuthService _auth;
    private readonly ISettingsService _settings;
    private readonly IPlaylistService _playlists;
    private readonly IFavoritesService _favorites;
    private readonly HttpClient _http;
    private readonly IAppLog _log;

    public SpotifyLibrarySyncService(
        SpotifyAuthService auth,
        ISettingsService settings,
        IPlaylistService playlists,
        IFavoritesService favorites,
        HttpClient http,
        IAppLog log)
    {
        _auth = auth;
        _settings = settings;
        _playlists = playlists;
        _favorites = favorites;
        _http = http;
        _log = log;
    }

    public async Task<SpotifySyncResult> SyncAsync(CancellationToken ct = default)
    {
        if (!_auth.IsConnected)
            return new SpotifySyncResult(0, 0, 0, "Not connected to Spotify.");

        var token = await _auth.GetUserAccessTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token))
            return new SpotifySyncResult(0, 0, 0, "Spotify session expired — connect again.");

        var playlistsSynced = 0;
        var tracksImported = 0;
        var likedImported = 0;

        try
        {
            var spotifyPlaylists = await FetchAllPlaylistsAsync(token, ct);
            _log.Info($"Spotify: {spotifyPlaylists.Count} playlists in account");

            foreach (var sp in spotifyPlaylists)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var tracks = await FetchPlaylistTracksAsync(token, sp.Id, ct);
                    var local = await _playlists.GetOrCreateExternalAsync(
                        MusicSource.Spotify, sp.Id, $"Spotify · {sp.Name}");
                    await _playlists.ReplaceTracksAsync(local.Id, tracks);
                    playlistsSynced++;
                    tracksImported += tracks.Count;
                    _log.Info($"Spotify playlist «{sp.Name}»: {tracks.Count} tracks");
                }
                catch (Exception ex)
                {
                    _log.Warning($"Spotify playlist «{sp.Name}» failed: {ex.Message}");
                }

                await Task.Delay(120, ct);
            }

            var liked = await FetchLikedTracksAsync(token, ct);
            foreach (var track in liked)
            {
                ct.ThrowIfCancellationRequested();
                if (await _favorites.EnsureFavoriteAsync(track))
                    likedImported++;
            }

            var likedPlaylist = await _playlists.GetOrCreateExternalAsync(
                MusicSource.Spotify, LikedPlaylistExternalId, "Spotify · Liked Songs");
            await _playlists.ReplaceTracksAsync(likedPlaylist.Id, liked);
            if (liked.Count > 0)
            {
                playlistsSynced++;
                tracksImported += liked.Count;
            }

            var s = _settings.Current;
            s.SpotifyLastSyncUtc = DateTime.UtcNow;
            await _settings.SaveAsync(s);

            _log.Info($"Spotify sync done: {playlistsSynced} playlists, {tracksImported} tracks, {likedImported} new likes");
            return new SpotifySyncResult(playlistsSynced, tracksImported, likedImported);
        }
        catch (Exception ex)
        {
            _log.Error("Spotify sync failed", ex);
            return new SpotifySyncResult(playlistsSynced, tracksImported, likedImported, ex.Message);
        }
    }

    private async Task<List<SpotifyPlaylistRef>> FetchAllPlaylistsAsync(string token, CancellationToken ct)
    {
        var list = new List<SpotifyPlaylistRef>();
        var next = $"{ApiBase}/me/playlists?limit=50";

        while (!string.IsNullOrWhiteSpace(next))
        {
            var page = await GetJsonAsync(next, token, ct);
            if (page == null) break;

            if (page.Value.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty("id", out var idEl)) continue;
                    var id = idEl.GetString();
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "Playlist" : "Playlist";
                    list.Add(new SpotifyPlaylistRef(id, name));
                }
            }

            next = ReadNextUrl(page.Value) ?? string.Empty;
        }

        return list;
    }

    private async Task<List<Track>> FetchPlaylistTracksAsync(string token, string playlistId, CancellationToken ct)
    {
        var expectedTotal = await GetPlaylistTrackTotalAsync(token, playlistId, ct);

        var tracks = await FetchPlaylistTracksFromUrlAsync(
            token,
            $"{ApiBase}/playlists/{playlistId}/items?limit=100&{MarketQuery}&additional_types=track",
            ct);

        if (tracks.Count == 0)
        {
            tracks = await FetchPlaylistTracksFromUrlAsync(
                token,
                $"{ApiBase}/playlists/{playlistId}/tracks?limit=100&{MarketQuery}&additional_types=track",
                ct);
        }

        if (tracks.Count == 0 && expectedTotal is > 0)
            _log.Warning($"Spotify playlist {playlistId}: API reports {expectedTotal} tracks but none could be imported.");

        return tracks;
    }

    private async Task<int?> GetPlaylistTrackTotalAsync(string token, string playlistId, CancellationToken ct)
    {
        var meta = await GetJsonAsync($"{ApiBase}/playlists/{playlistId}?fields=name,tracks.total", token, ct);
        if (meta is { } json
            && json.TryGetProperty("tracks", out var tracks)
            && tracks.TryGetProperty("total", out var totalEl))
            return totalEl.GetInt32();
        return null;
    }

    private async Task<List<Track>> FetchPlaylistTracksFromUrlAsync(string token, string startUrl, CancellationToken ct)
    {
        var tracks = new List<Track>();
        var next = startUrl;

        while (!string.IsNullOrWhiteSpace(next))
        {
            var page = await GetJsonAsync(next, token, ct);
            if (page == null) break;

            if (page.Value.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var track = SpotifyTrackParser.FromItem(item);
                    if (track != null)
                        tracks.Add(track);
                }
            }

            next = ReadNextUrl(page.Value);
        }

        return tracks;
    }

    private async Task<List<Track>> FetchLikedTracksAsync(string token, CancellationToken ct)
    {
        var tracks = new List<Track>();
        var next = $"{ApiBase}/me/tracks?limit=50&{MarketQuery}";

        while (!string.IsNullOrWhiteSpace(next))
        {
            var page = await GetJsonAsync(next, token, ct);
            if (page == null) break;

            if (page.Value.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var track = SpotifyTrackParser.FromItem(item);
                    if (track != null)
                        tracks.Add(track);
                }
            }

            next = ReadNextUrl(page.Value);
        }

        return tracks;
    }

    private static string? ReadNextUrl(JsonElement page)
    {
        if (!page.TryGetProperty("next", out var nxt) || nxt.ValueKind != JsonValueKind.String)
            return null;
        return nxt.GetString();
    }

    private async Task<JsonElement?> GetJsonAsync(string url, string token, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            if (resp.StatusCode == HttpStatusCode.TooManyRequests && attempt < 3)
            {
                var delaySec = 2;
                if (resp.Headers.RetryAfter?.Delta is { } delta)
                    delaySec = Math.Max(1, (int)delta.TotalSeconds);
                _log.Info($"Spotify rate limit, waiting {delaySec}s…");
                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                continue;
            }

            var body = await resp.Content.ReadAsStringAsync(ct);
            _log.Warning($"Spotify API {resp.StatusCode} for {url}: {body[..Math.Min(body.Length, 200)]}");
            return null;
        }

        return null;
    }

    private sealed record SpotifyPlaylistRef(string Id, string Name);
}
