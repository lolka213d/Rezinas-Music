using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Harmony.Helpers;
using Harmony.Models;
using Harmony.ViewModels;

namespace Harmony.Services;

/// <summary>Fetches chart/editorial content from the free Deezer API for the Home screen.</summary>
public sealed class DeezerHomeService
{
    private const string ApiBase = "https://api.deezer.com";
    private const int PageSize = 100;
    private const int DefaultChartTrackPages = 3;
    private const int DefaultChartAlbumPages = 2;
    private readonly HttpClient _http;

    public DeezerHomeService(HttpClient http) => _http = http;

    public Task<IReadOnlyList<Track>> GetChartTracksAsync(CancellationToken ct = default) =>
        GetChartTracksAsync(DefaultChartTrackPages, ct);

    public async Task<IReadOnlyList<Track>> GetChartTracksAsync(int maxPages, int pageSize, CancellationToken ct = default)
    {
        var all = new List<Track>();
        var size = Math.Clamp(pageSize, 1, PageSize);
        try
        {
            for (var page = 0; page < maxPages; page++)
            {
                var index = page * size;
                using var resp = await _http.GetAsync($"{ApiBase}/chart/0/tracks?limit={size}&index={index}", ct);
                if (!resp.IsSuccessStatusCode) break;
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                var batch = ParseTracks(json, "data");
                if (batch.Count == 0) break;
                all.AddRange(batch);
                if (batch.Count < size) break;
            }
            return all;
        }
        catch
        {
            return all;
        }
    }

    public Task<IReadOnlyList<Track>> GetChartTracksAsync(int maxPages, CancellationToken ct = default) =>
        GetChartTracksAsync(maxPages, PageSize, ct);

    public Task<IReadOnlyList<Track>> GetRegionalChartTracksAsync(
        string language, int maxPages = 1, int pageSize = 40, CancellationToken ct = default) =>
        GetPlaylistTracksAsync(ChartEditorialMap.GetPlaylistId(language), maxPages, pageSize, 0, ct);

    public Task<IReadOnlyList<HomeAlbumCard>> GetRegionalChartAlbumsAsync(
        string language, int maxPages = 1, int pageSize = 24, CancellationToken ct = default) =>
        GetPlaylistAlbumsAsync(
            ChartEditorialMap.GetPlaylistId(language),
            Math.Clamp(pageSize * maxPages, 1, 100),
            ct);

    /// <summary>Tracks for a curated radio station.</summary>
    public async Task<IReadOnlyList<Track>> GetRadioStationTracksAsync(
        RadioStation station, int limit = 40, int startIndex = 0, CancellationToken ct = default)
    {
        IReadOnlyList<Track> tracks = station.Kind switch
        {
            RadioStationKind.Playlist => await GetRegionalRadioTracksAsync(station.Id, limit, startIndex, ct),
            RadioStationKind.Genre => await GetGenreRadioTracksAsync((int)station.DeezerId, limit, ct),
            _ => await GetPlaylistTracksAsync(ChartEditorialMap.WorldwidePlaylistId, 2, 25, startIndex, ct)
        };
        return tracks.Take(limit).ToList();
    }

    private async Task<IReadOnlyList<Track>> GetRegionalRadioTracksAsync(
        string stationId, int limit, int startIndex, CancellationToken ct)
    {
        var merged = new List<Track>();
        foreach (var playlistId in ChartEditorialMap.GetPlaylistIds(stationId))
        {
            var batch = await GetPlaylistTracksAsync(playlistId, 2, 25, startIndex, ct, allowChartFallback: false);
            foreach (var track in batch)
            {
                if (merged.Any(t => t.Matches(track))) continue;
                merged.Add(track);
            }

            if (merged.Count >= limit * 2)
                break;
        }

        var filtered = RegionalTrackFilter.Apply(stationId, merged, limit * 2);
        if (filtered.Count >= limit / 2)
            return filtered;

        foreach (var playlistId in ChartEditorialMap.GetPlaylistIds(stationId))
        {
            var batch = await GetPlaylistTracksAsync(playlistId, 3, 40, startIndex, ct, allowChartFallback: false);
            foreach (var track in batch)
            {
                if (merged.Any(t => t.Matches(track))) continue;
                merged.Add(track);
            }
        }

        filtered = RegionalTrackFilter.Apply(stationId, merged, limit * 2);
        return filtered.Count > 0
            ? filtered
            : merged.Take(limit).ToList();
    }

    public async Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(
        long playlistId, int maxPages, int pageSize, int startIndex = 0, CancellationToken ct = default) =>
        await GetPlaylistTracksAsync(playlistId, maxPages, pageSize, startIndex, ct, allowChartFallback: true);

    public async Task<IReadOnlyList<Track>> GetPlaylistTracksAsync(
        long playlistId, int maxPages, int pageSize, int startIndex, CancellationToken ct, bool allowChartFallback)
    {
        var all = new List<Track>();
        var size = Math.Clamp(pageSize, 1, PageSize);
        try
        {
            for (var page = 0; page < maxPages; page++)
            {
                var index = startIndex + page * size;
                using var resp = await _http.GetAsync(
                    $"{ApiBase}/playlist/{playlistId}/tracks?limit={size}&index={index}", ct);
                if (!resp.IsSuccessStatusCode) break;
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                var batch = ParseTracks(json, "data");
                if (batch.Count == 0) break;
                all.AddRange(batch);
                if (batch.Count < size) break;
            }

            return all.Count > 0 || !allowChartFallback
                ? all
                : await GetChartTracksAsync(maxPages, pageSize, ct);
        }
        catch
        {
            return all.Count > 0 || !allowChartFallback
                ? all
                : await GetChartTracksAsync(maxPages, pageSize, ct);
        }
    }

    private async Task<IReadOnlyList<HomeAlbumCard>> GetPlaylistAlbumsAsync(
        long playlistId, int maxItems, CancellationToken ct)
    {
        try
        {
            var fetch = Math.Clamp(maxItems * 3, 1, 100);
            using var resp = await _http.GetAsync(
                $"{ApiBase}/playlist/{playlistId}/tracks?limit={fetch}", ct);
            if (!resp.IsSuccessStatusCode)
                return await GetChartAlbumsAsync(1, maxItems, ct);

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!json.TryGetProperty("data", out var data))
                return await GetChartAlbumsAsync(1, maxItems, ct);

            var seen = new HashSet<string>();
            var albums = new List<HomeAlbumCard>();
            foreach (var item in data.EnumerateArray())
            {
                if (!item.TryGetProperty("album", out var album)) continue;
                var id = album.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : "";
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;

                var title = album.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var artist = item.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var an)
                    ? an.GetString() ?? ""
                    : "";
                string? cover = null;
                if (album.TryGetProperty("cover_medium", out var c)) cover = c.GetString();

                albums.Add(new HomeAlbumCard
                {
                    Title = title,
                    ArtistName = artist,
                    ThumbnailUrl = cover,
                    SourceId = id
                });
                if (albums.Count >= maxItems) break;
            }

            return albums.Count > 0
                ? albums
                : await GetChartAlbumsAsync(1, maxItems, ct);
        }
        catch
        {
            return await GetChartAlbumsAsync(1, maxItems, ct);
        }
    }

    public Task<IReadOnlyList<HomeAlbumCard>> GetChartAlbumsAsync(CancellationToken ct = default) =>
        GetChartAlbumsAsync(DefaultChartAlbumPages, ct);

    public async Task<IReadOnlyList<HomeAlbumCard>> GetChartAlbumsAsync(int maxPages, int pageSize, CancellationToken ct = default)
    {
        var albums = new List<HomeAlbumCard>();
        var size = Math.Clamp(pageSize, 1, PageSize);
        try
        {
            for (var page = 0; page < maxPages; page++)
            {
                var index = page * size;
                using var resp = await _http.GetAsync($"{ApiBase}/chart/0/albums?limit={size}&index={index}", ct);
                if (!resp.IsSuccessStatusCode) break;
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                if (!json.TryGetProperty("data", out var data)) break;

                var count = 0;
                foreach (var item in data.EnumerateArray())
                {
                    count++;
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : "";
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var artist = item.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var an)
                        ? an.GetString() ?? ""
                        : "";
                    string? cover = null;
                    if (item.TryGetProperty("cover_medium", out var c)) cover = c.GetString();

                    albums.Add(new HomeAlbumCard
                    {
                        Title = title,
                        ArtistName = artist,
                        ThumbnailUrl = cover,
                        SourceId = id
                    });
                }

                if (count < size) break;
            }
            return albums;
        }
        catch
        {
            return albums;
        }
    }

    public Task<IReadOnlyList<HomeAlbumCard>> GetChartAlbumsAsync(int maxPages, CancellationToken ct = default) =>
        GetChartAlbumsAsync(maxPages, PageSize, ct);

    /// <summary>Search albums on Deezer for the Search page.</summary>
    public async Task<IReadOnlyList<HomeAlbumCard>> SearchAlbumsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<HomeAlbumCard>();

        try
        {
            var url = $"{ApiBase}/search/album?q={Uri.EscapeDataString(query.Trim())}&limit=50";
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<HomeAlbumCard>();
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!json.TryGetProperty("data", out var data)) return Array.Empty<HomeAlbumCard>();

            var albums = new List<HomeAlbumCard>();
            foreach (var item in data.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : "";
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var artist = item.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var an)
                    ? an.GetString() ?? ""
                    : "";
                string? cover = null;
                if (item.TryGetProperty("cover_medium", out var c)) cover = c.GetString();

                albums.Add(new HomeAlbumCard
                {
                    Title = title,
                    ArtistName = artist,
                    ThumbnailUrl = cover,
                    SourceId = id
                });
            }
            return albums;
        }
        catch
        {
            return Array.Empty<HomeAlbumCard>();
        }
    }

    /// <summary>Search artists on Deezer for the Search page.</summary>
    public async Task<IReadOnlyList<SearchArtistCard>> SearchArtistsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<SearchArtistCard>();

        try
        {
            var url = $"{ApiBase}/search/artist?q={Uri.EscapeDataString(query.Trim())}&limit=30";
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return Array.Empty<SearchArtistCard>();
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!json.TryGetProperty("data", out var data)) return Array.Empty<SearchArtistCard>();

            var artists = new List<SearchArtistCard>();
            foreach (var item in data.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : "";
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                string? picture = null;
                if (item.TryGetProperty("picture_medium", out var p)) picture = p.GetString();

                artists.Add(new SearchArtistCard
                {
                    Name = name,
                    ThumbnailUrl = picture,
                    SourceId = id
                });
            }
            return artists;
        }
        catch
        {
            return Array.Empty<SearchArtistCard>();
        }
    }

    /// <summary>All tracks for a Deezer album (for play-album from search).</summary>
    public async Task<IReadOnlyList<Track>> GetAlbumTracksAsync(string albumId, CancellationToken ct = default)
    {
        albumId = albumId.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(albumId)) return Array.Empty<Track>();

        var all = new List<Track>();
        var next = $"{ApiBase}/album/{albumId}/tracks?limit=50";
        while (!string.IsNullOrWhiteSpace(next))
        {
            try
            {
                using var resp = await _http.GetAsync(next, ct);
                if (!resp.IsSuccessStatusCode) break;
                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                all.AddRange(ParseTracks(json, "data"));
                next = json.TryGetProperty("next", out var n) ? n.GetString() : null;
            }
            catch
            {
                break;
            }
        }
        return all;
    }

    /// <summary>Find Deezer album id by title + artist (for favorites / history albums).</summary>
    public async Task<string?> FindAlbumIdAsync(string albumTitle, string artistName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(albumTitle)) return null;

        var query = $"{artistName} {albumTitle}".Trim();
        try
        {
            var url = $"{ApiBase}/search/album?q={Uri.EscapeDataString(query)}&limit=10";
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!json.TryGetProperty("data", out var data)) return null;

            string? bestId = null;
            var bestScore = -1;
            foreach (var item in data.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : "";
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var artist = item.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var an)
                    ? an.GetString() ?? "" : "";

                var score = ScoreAlbumMatch(albumTitle, artistName, title, artist);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = id;
                }
            }
            return bestScore >= 2 ? bestId : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Find Deezer artist id by name (for album / now-playing metadata).</summary>
    public async Task<string?> FindArtistIdAsync(string artistName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artistName)) return null;

        try
        {
            var url = $"{ApiBase}/search/artist?q={Uri.EscapeDataString(artistName.Trim())}&limit=10";
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!json.TryGetProperty("data", out var data)) return null;

            var target = Normalize(artistName);
            string? bestId = null;
            var bestScore = -1;
            foreach (var item in data.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : "";
                var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var score = Normalize(name) == target ? 4
                    : Normalize(name).Contains(target) || target.Contains(Normalize(name)) ? 2 : 0;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = id;
                }
            }

            return bestScore >= 2 ? bestId : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Album metadata from Deezer.</summary>
    public async Task<AlbumInfo?> GetAlbumInfoAsync(string albumId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(albumId)) return null;
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/album/{albumId}", ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            var title = json.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var artist = json.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var an)
                ? an.GetString() ?? "" : "";
            string? cover = null;
            if (json.TryGetProperty("cover_big", out var c)) cover = c.GetString();
            var release = json.TryGetProperty("release_date", out var rd) ? rd.GetString() : null;
            var year = int.TryParse(release?.Split('-')[0], out var y) ? y : (int?)null;
            var tracks = json.TryGetProperty("nb_tracks", out var nt) ? nt.GetInt32() : 0;
            var label = json.TryGetProperty("label", out var lb) ? lb.GetString() : null;
            var recordType = json.TryGetProperty("record_type", out var rt) ? rt.GetString() : null;
            var fans = json.TryGetProperty("fans", out var f) ? f.GetInt32() : 0;

            return new AlbumInfo(title, artist, cover, year, tracks, label, recordType, fans);
        }
        catch
        {
            return null;
        }
    }

    private static int ScoreAlbumMatch(string wantTitle, string wantArtist, string title, string artist)
    {
        var score = 0;
        if (Normalize(wantTitle) == Normalize(title)) score += 4;
        else if (Normalize(title).Contains(Normalize(wantTitle), StringComparison.Ordinal) ||
                 Normalize(wantTitle).Contains(Normalize(title), StringComparison.Ordinal))
            score += 2;

        if (!string.IsNullOrWhiteSpace(wantArtist))
        {
            if (Normalize(wantArtist) == Normalize(artist)) score += 3;
            else if (Normalize(artist).Contains(Normalize(wantArtist), StringComparison.Ordinal))
                score += 1;
        }
        return score;
    }

    private static string Normalize(string s) =>
        s.Trim().ToLowerInvariant().Replace("'", "").Replace("'", "");

    /// <summary>Top track for a Deezer artist (loaded on demand when user clicks play).</summary>
    public Task<Track?> GetArtistTopTrackAsync(
        string artistId, string artistName, string? picture, CancellationToken ct = default)
        => GetArtistTopTrackInternalAsync(artistId, artistName, picture, ct);

    /// <summary>Artist page: bio, top tracks, discography.</summary>
    public async Task<ArtistPageData?> GetArtistPageAsync(string artistId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(artistId)) return null;
        try
        {
            using var infoResp = await _http.GetAsync($"{ApiBase}/artist/{artistId}", ct);
            if (!infoResp.IsSuccessStatusCode) return null;
            var info = await infoResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            var name = info.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string? picture = null;
            if (info.TryGetProperty("picture_xl", out var p)) picture = p.GetString();
            var fans = info.TryGetProperty("nb_fan", out var f) ? f.GetInt32() : 0;

            using var topResp = await _http.GetAsync($"{ApiBase}/artist/{artistId}/top?limit=10", ct);
            var topTracks = topResp.IsSuccessStatusCode
                ? ParseTracks(await topResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct), "data")
                : Array.Empty<Track>();

            using var albResp = await _http.GetAsync($"{ApiBase}/artist/{artistId}/albums?limit=16", ct);
            var albums = new List<HomeAlbumCard>();
            if (albResp.IsSuccessStatusCode)
            {
                var albJson = await albResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                if (albJson.TryGetProperty("data", out var data))
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        var id = item.TryGetProperty("id", out var idEl) ? idEl.GetRawText() : "";
                        var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                        string? cover = null;
                        if (item.TryGetProperty("cover_medium", out var c)) cover = c.GetString();
                        albums.Add(new HomeAlbumCard
                        {
                            Title = title,
                            ArtistName = name,
                            ThumbnailUrl = cover,
                            SourceId = id
                        });
                    }
                }
            }

            foreach (var t in topTracks)
            {
                t.ArtistName = string.IsNullOrWhiteSpace(t.ArtistName) ? name : t.ArtistName;
                t.ThumbnailUrl ??= picture;
            }

            return new ArtistPageData(name, picture, null, fans, topTracks, albums);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Track?> GetArtistTopTrackInternalAsync(
        string artistId, string artistName, string? picture, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(artistId)) return null;
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/artist/{artistId}/top?limit=1", ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var tracks = ParseTracks(json, "data");
            var first = tracks.FirstOrDefault();
            if (first != null)
            {
                first.ArtistName = string.IsNullOrWhiteSpace(first.ArtistName) ? artistName : first.ArtistName;
                first.ThumbnailUrl ??= picture;
            }
            return first;
        }
        catch
        {
            return null;
        }
    }

    private async Task<Track?> GetAlbumFirstTrackAsync(
        string albumId, string albumTitle, string artist, string? cover, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(albumId)) return null;
        try
        {
            using var resp = await _http.GetAsync($"{ApiBase}/album/{albumId}/tracks?limit=1", ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var tracks = ParseTracks(json, "data");
            var first = tracks.FirstOrDefault();
            if (first != null)
            {
                first.AlbumName = albumTitle;
                first.ArtistName = string.IsNullOrWhiteSpace(first.ArtistName) ? artist : first.ArtistName;
                first.ThumbnailUrl ??= cover;
            }
            return first;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<Track> ParseTracks(JsonElement root, string arrayProperty)
    {
        if (!root.TryGetProperty(arrayProperty, out var data))
            return Array.Empty<Track>();

        var list = new List<Track>();
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

            list.Add(new Track
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
        return list;
    }

    /// <summary>Artist bio, credits and Spotify links for the now-playing panel.</summary>
    public async Task<TrackContextData> GetTrackContextAsync(Track track, CancellationToken ct = default)
    {
        var credits = new List<TrackCreditLine>();
        var primaryName = track.ArtistName.Split(',')[0].Trim();
        string? deezerArtistId = null;

        if (track.Source == MusicSource.Deezer && !string.IsNullOrWhiteSpace(track.SourceId))
        {
            try
            {
                using var resp = await _http.GetAsync($"{ApiBase}/track/{track.SourceId}", ct);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                    if (json.TryGetProperty("contributors", out var contribs))
                    {
                        foreach (var c in contribs.EnumerateArray())
                        {
                            var name = c.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : "";
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            var role = c.TryGetProperty("role", out var cr) ? cr.GetString() : null;
                            var isMain = string.Equals(role, "Main", StringComparison.OrdinalIgnoreCase);
                            credits.Add(new TrackCreditLine(
                                name,
                                MapCreditRole(role),
                                SpotifyLinks.Artist(name),
                                isMain));
                        }
                    }

                    if (json.TryGetProperty("artist", out var artistEl))
                    {
                        if (artistEl.TryGetProperty("id", out var aid))
                            deezerArtistId = aid.GetRawText();
                        if (artistEl.TryGetProperty("name", out var an))
                            primaryName = an.GetString() ?? primaryName;
                    }
                }
            }
            catch { /* best-effort */ }
        }

        if (credits.Count == 0)
        {
            foreach (var part in track.ArtistName.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                credits.Add(new TrackCreditLine(
                    part,
                    "Main Artist",
                    SpotifyLinks.Artist(part),
                    true));
            }
        }

        string? image = null;
        string fansLine = string.Empty;
        string? bio = null;

        deezerArtistId ??= await FindArtistIdAsync(primaryName, ct);
        if (!string.IsNullOrWhiteSpace(deezerArtistId))
        {
            var page = await GetArtistPageAsync(deezerArtistId, ct);
            if (page != null)
            {
                primaryName = page.Name;
                image = page.PictureUrl;
                if (page.Fans > 0)
                    fansLine = $"{page.Fans:N0} fans";
            }
        }

        return new TrackContextData
        {
            PrimaryArtistName = primaryName,
            DeezerArtistId = deezerArtistId,
            ArtistImageUrl = image ?? track.ThumbnailUrl,
            ArtistBio = bio,
            FansLine = fansLine,
            TrackSpotifyUrl = SpotifyLinks.Track(track),
            PrimaryArtistSpotifyUrl = SpotifyLinks.Artist(primaryName),
            Credits = credits
        };
    }

    private static string MapCreditRole(string? role) => role?.ToLowerInvariant() switch
    {
        "main" => "Main Artist",
        "featured" => "Featured Artist",
        "composer" => "Composer",
        "lyricist" => "Lyricist",
        "producer" => "Producer",
        _ => string.IsNullOrWhiteSpace(role) ? "Artist" : role
    };

    /// <summary>Tracks from a genre radio station.</summary>
    public async Task<IReadOnlyList<Track>> GetGenreRadioTracksAsync(int genreId, int limit = 24, CancellationToken ct = default)
    {
        try
        {
            using var radioListResp = await _http.GetAsync($"{ApiBase}/genre/{genreId}/radios?limit=1", ct);
            if (!radioListResp.IsSuccessStatusCode) return Array.Empty<Track>();

            var radioListJson = await radioListResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            if (!radioListJson.TryGetProperty("data", out var radios) || radios.GetArrayLength() == 0)
                return Array.Empty<Track>();

            var radioId = radios[0].GetProperty("id").GetInt64();
            using var tracksResp = await _http.GetAsync($"{ApiBase}/radio/{radioId}/tracks?limit={Math.Clamp(limit, 1, 50)}", ct);
            if (!tracksResp.IsSuccessStatusCode) return Array.Empty<Track>();

            var tracksJson = await tracksResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return ParseTracks(tracksJson, "data");
        }
        catch
        {
            return Array.Empty<Track>();
        }
    }
}
