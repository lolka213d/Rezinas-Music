using System.Collections.Concurrent;
using System.IO;

using System.Net.Http;

using System.Net.Http.Json;

using System.Text.Json;

using Harmony.Data;

using Harmony.Models;

using Harmony.Services.Interfaces;

using YoutubeExplode;

using YoutubeExplode.Videos.Streams;



namespace Harmony.Services;



/// <summary>

/// Resolves full-length audio via YouTube search + direct stream URL (online playback).

/// Falls back to Piped API when YoutubeExplode manifest returns 400/403.

/// </summary>

public sealed class YouTubeStreamResolver : IStreamResolverService

{

    private static readonly string[] PipedInstances =

    [

        "https://pipedapi.kavin.rocks",

        "https://pipedapi.adminforge.de",

        "https://api.piped.yt",

    ];



    private readonly YoutubeClient _youtube = new();
    private readonly HttpClient _http;
    private readonly IAppLog _log;
    private readonly ISettingsService _settings;
    private readonly ConcurrentDictionary<string, string> _urlCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _videoIdCache = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxSearchCandidates = 12;
    private const int MinMatchScore = 55;

    private sealed record DeezerTrackMeta(string Title, string Artist, int DurationSeconds, string? Preview);



    public YouTubeStreamResolver(IAppLog log, HttpClient http, ISettingsService settings)

    {

        _log = log;

        _http = http;

        _settings = settings;

    }



    public async Task<string?> ResolveFullStreamAsync(Track track, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(track);
        string? deezerPreview = null;
        var searchArtist = track.ArtistName;
        var searchTitle = track.Title;
        var expectedDuration = track.DurationSeconds;

        // Refresh expiring provider URLs before deciding we already have a full stream.
        if (track.Source == MusicSource.SoundCloud)
        {
            var scUrl = await RefreshSoundCloudStreamAsync(track, cancellationToken);
            if (!string.IsNullOrWhiteSpace(scUrl))
            {
                track.StreamUrl = scUrl;
                return scUrl;
            }
        }

        if (track.Source == MusicSource.Deezer && !string.IsNullOrWhiteSpace(track.SourceId))
        {
            var meta = await RefreshDeezerTrackAsync(track.SourceId, cancellationToken);
            if (meta != null)
            {
                deezerPreview = meta.Preview;
                if (!string.IsNullOrWhiteSpace(meta.Title))
                    searchTitle = meta.Title;
                if (!string.IsNullOrWhiteSpace(meta.Artist))
                    searchArtist = meta.Artist;
                if (meta.DurationSeconds > 0)
                {
                    expectedDuration = meta.DurationSeconds;
                    track.DurationSeconds = meta.DurationSeconds;
                }
            }
        }

        if (!NeedsFullStream(track))
        {
            _log.Info($"Stream OK without YouTube: {track.ArtistName} — {track.Title}");
            return track.StreamUrl;
        }

        if (_urlCache.TryGetValue(cacheKey, out var cached))
        {
            _log.Info($"Stream cache hit: {track.Title}");
            return cached;
        }

        if (_videoIdCache.TryGetValue(cacheKey, out var knownVideoId))
        {
            var cachedUrl = await TryResolveVideoIdAsync(knownVideoId, track, cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cachedUrl))
                return cachedUrl;
            _videoIdCache.TryRemove(cacheKey, out _);
        }

        var albumPhrase = PrimaryAlbum(track.AlbumName);
        var titlePhrase = PrimaryTitle(searchTitle);
        var baseQuery = $"{searchArtist} {titlePhrase}".Trim();
        if (string.IsNullOrWhiteSpace(baseQuery))
        {
            _log.Warning($"Empty search query for track '{track.Title}', using preview.");
            return deezerPreview ?? track.StreamUrl;
        }

        var queryList = new List<string>();
        if (!string.IsNullOrWhiteSpace(albumPhrase))
            queryList.Add($"{searchArtist} {titlePhrase} {albumPhrase}");
        queryList.Add($"{searchArtist} - {titlePhrase}");
        queryList.Add($"{searchArtist} {titlePhrase} official audio");
        queryList.Add($"{searchArtist} {titlePhrase}");
        queryList.Add(baseQuery);
        var queries = queryList.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var probeTrack = CloneForSearch(track, searchArtist, searchTitle, expectedDuration);

        foreach (var query in queries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _log.Info($"Resolving online stream via YouTube: {query}");
            var streamUrl = await TryYouTubeSearchAsync(probeTrack, query, cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(streamUrl))
                return streamUrl;
        }

        _log.Warning($"YouTube + Piped: no confident match for '{baseQuery}'");

        if (!string.IsNullOrWhiteSpace(deezerPreview))
        {
            _log.Info($"Using Deezer preview for '{track.Title}'");
            return deezerPreview;
        }

        if (!string.IsNullOrWhiteSpace(track.StreamUrl))
            _log.Warning($"Falling back to preview URL for '{track.Title}'");

        return track.StreamUrl;
    }

    private static string CacheKey(Track track) =>
        string.IsNullOrWhiteSpace(track.SourceId)
            ? $"v3:{track.Source}:{track.ArtistName}:{track.Title}:{track.DurationSeconds}"
            : $"v3:{track.Source}:{track.SourceId}";

    private static int DurationToleranceSeconds(int durationSeconds)
    {
        if (durationSeconds <= 0) return 10;
        return Math.Clamp((int)Math.Round(durationSeconds * 0.04), 5, 8);
    }

    private static string PrimaryAlbum(string? album)
    {
        if (string.IsNullOrWhiteSpace(album)) return string.Empty;
        var cut = album.Split(['.', '·', '|'], 2, StringSplitOptions.TrimEntries);
        var phrase = cut[0].Trim();
        return phrase.Length > 48 ? phrase[..48].Trim() : phrase;
    }

    private static Track CloneForSearch(Track track, string artist, string title, int durationSeconds) =>
        new()
        {
            Source = track.Source,
            SourceId = track.SourceId,
            ArtistName = artist,
            Title = title,
            DurationSeconds = durationSeconds,
            AlbumName = track.AlbumName
        };

    private static string PrimaryTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return title;
        var cut = title.Split(['.', '·', '|', '—', '-'], 2, StringSplitOptions.TrimEntries);
        return cut[0].Trim();
    }

    private async Task<string?> TryYouTubeSearchAsync(
        Track track, string query, string cacheKey, CancellationToken cancellationToken)
    {
        var candidates = 0;
        string? bestVideoId = null;
        string? bestTitle = null;
        var bestScore = -1;

        await foreach (var video in _youtube.Search.GetVideosAsync(query, cancellationToken))
        {
            if (++candidates > MaxSearchCandidates)
                break;

            var videoTitle = video.Title ?? string.Empty;
            var channelTitle = video.Author?.ChannelTitle ?? string.Empty;
            var videoDuration = video.Duration?.TotalSeconds ?? 0;
            if (videoDuration < 30)
            {
                _log.Info($"Skip short video '{videoTitle}' ({videoDuration:F0}s)");
                continue;
            }

            if (track.DurationSeconds > 0)
            {
                var delta = Math.Abs(videoDuration - track.DurationSeconds);
                var tolerance = DurationToleranceSeconds(track.DurationSeconds);
                if (delta > tolerance)
                {
                    _log.Info($"Skip duration mismatch '{videoTitle}' ({videoDuration:F0}s vs {track.DurationSeconds}s, tol {tolerance}s)");
                    continue;
                }
            }

            var score = ScoreVideoMatch(videoTitle, channelTitle, videoDuration, track);
            if (score < MinMatchScore)
            {
                _log.Info($"Skip low score ({score}) '{videoTitle}' / '{channelTitle}' for '{track.ArtistName} — {track.Title}'");
                continue;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestVideoId = video.Id.Value;
                bestTitle = videoTitle;
            }
        }

        if (string.IsNullOrWhiteSpace(bestVideoId))
            return null;

        _videoIdCache[cacheKey] = bestVideoId;
        _log.Info($"Best YouTube match (score {bestScore}): {bestTitle} ({bestVideoId})");

        var streamUrl = await TryYoutubeExplodeStreamUrlAsync(new YoutubeExplode.Videos.VideoId(bestVideoId), cancellationToken);
        if (!string.IsNullOrWhiteSpace(streamUrl))
        {
            _log.Info($"Online stream ready (YouTube): {track.Title}");
            return RememberStream(cacheKey, streamUrl);
        }

        streamUrl = await TryPipedStreamUrlAsync(bestVideoId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(streamUrl))
        {
            _log.Info($"Online stream ready (Piped): {track.Title}");
            return RememberStream(cacheKey, streamUrl);
        }

        return null;
    }

    private async Task<DeezerTrackMeta?> RefreshDeezerTrackAsync(string sourceId, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.deezer.com/track/{sourceId}";
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            var title = json.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var artist = json.TryGetProperty("artist", out var a) && a.TryGetProperty("name", out var an)
                ? an.GetString() ?? string.Empty
                : string.Empty;
            var duration = json.TryGetProperty("duration", out var d) ? d.GetInt32() : 0;
            var preview = json.TryGetProperty("preview", out var p) ? p.GetString() : null;

            return new DeezerTrackMeta(title, artist, duration, preview);
        }
        catch (Exception ex)
        {
            _log.Warning($"Deezer track refresh failed for {sourceId}: {ex.Message}");
            return null;
        }
    }

    private static int ScoreVideoMatch(string videoTitle, string channelTitle, double videoDuration, Track track)
    {
        var video = NormalizeForMatch(videoTitle);
        if (video.Length == 0) return 0;

        var unwanted = new[] { "live", "karaoke", "instrumental", "nightcore", "sped up", "cover version", "cover", "кавер" };
        var trackMeta = NormalizeForMatch($"{track.Title} {track.AlbumName}");
        foreach (var word in unwanted)
        {
            if (video.Contains(word, StringComparison.OrdinalIgnoreCase)
                && !trackMeta.Contains(word, StringComparison.OrdinalIgnoreCase))
                return 0;
        }

        if (video.Contains("tribute", StringComparison.OrdinalIgnoreCase)
            || video.Contains("трибьют", StringComparison.OrdinalIgnoreCase))
        {
            if (!trackMeta.Contains("tribute", StringComparison.OrdinalIgnoreCase)
                && !trackMeta.Contains("трибьют", StringComparison.OrdinalIgnoreCase))
                return 0;
        }

        var title = NormalizeForMatch(PrimaryTitle(track.Title));
        if (title.Length == 0) return 0;

        var titleTokens = title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToList();
        var titleRatio = titleTokens.Count == 0
            ? (video.Contains(title, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0)
            : (double)titleTokens.Count(t => video.Contains(t, StringComparison.OrdinalIgnoreCase)) / titleTokens.Count;
        if (titleRatio < 0.55) return 0;

        var haystack = $"{video} {NormalizeForMatch(channelTitle)}";
        if (!ArtistMatches(haystack, track.ArtistName))
            return 0;

        var score = (int)Math.Round(titleRatio * 50);
        score += ArtistMatchStrength(haystack, track.ArtistName);

        var album = NormalizeForMatch(PrimaryAlbum(track.AlbumName));
        if (album.Length > 3)
        {
            var albumTokens = album.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2)
                .Take(4)
                .ToList();
            if (albumTokens.Count > 0)
            {
                var albumHits = albumTokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
                if (albumHits > 0)
                    score += (int)Math.Round((double)albumHits / albumTokens.Count * 20);
            }
        }

        if (video.Contains("official", StringComparison.OrdinalIgnoreCase))
            score += 8;
        if (video.Contains("audio", StringComparison.OrdinalIgnoreCase))
            score += 4;

        if (track.DurationSeconds > 0 && videoDuration > 0)
        {
            var delta = Math.Abs(videoDuration - track.DurationSeconds);
            var tolerance = DurationToleranceSeconds(track.DurationSeconds);
            if (delta == 0)
                score += 15;
            else if (delta <= tolerance / 2)
                score += 10;
            else if (delta <= tolerance)
                score += 4;
        }

        return score;
    }

    private static int ArtistMatchStrength(string haystack, string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName)) return 20;

        var best = 0;
        foreach (var variant in ArtistMatchVariants(PrimaryArtist(artistName)))
        {
            if (variant.Length >= 2 && haystack.Contains(variant, StringComparison.OrdinalIgnoreCase))
                best = Math.Max(best, variant.Length >= 4 ? 35 : 28);
        }

        if (best > 0) return best;

        var artist = NormalizeForMatch(PrimaryArtist(artistName));
        var tokens = artist.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToList();
        if (tokens.Count == 0) return 0;

        var matched = tokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
        var ratio = (double)matched / tokens.Count;
        if (matched == 0 || ratio < 0.5) return 0;

        return 20 + (int)Math.Round(ratio * 15);
    }

    private static bool ArtistMatches(string haystack, string artistName)
        => ArtistMatchStrength(haystack, artistName) > 0;

    private static string PrimaryArtist(string artist) =>
        artist.Split(',')[0].Trim();

    private static IEnumerable<string> ArtistMatchVariants(string artist)
    {
        var normalized = NormalizeForMatch(artist);
        if (normalized.Length > 0)
            yield return normalized;

        var romanized = TransliterateCyrillicToLatin(normalized);
        if (romanized.Length > 0 && !romanized.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            yield return romanized;
    }

    private static string TransliterateCyrillicToLatin(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        ReadOnlySpan<(char From, string To)> map =
        [
            ('а', "a"), ('б', "b"), ('в', "v"), ('г', "g"), ('д', "d"), ('е', "e"), ('ё', "yo"),
            ('ж', "zh"), ('з', "z"), ('и', "i"), ('й', "y"), ('к', "k"), ('л', "l"), ('м', "m"),
            ('н', "n"), ('о', "o"), ('п', "p"), ('р', "r"), ('с', "s"), ('т', "t"), ('у', "u"),
            ('ф', "f"), ('х', "h"), ('ц', "ts"), ('ч', "ch"), ('ш', "sh"), ('щ', "sch"),
            ('ъ', ""), ('ы', "y"), ('ь', ""), ('э', "e"), ('ю', "yu"), ('я', "ya")
        ];

        var sb = new System.Text.StringBuilder(value.Length * 2);
        foreach (var ch in value)
        {
            var lower = char.ToLowerInvariant(ch);
            var found = false;
            foreach (var (from, to) in map)
            {
                if (lower != from) continue;
                sb.Append(to);
                found = true;
                break;
            }

            if (!found)
                sb.Append(lower);
        }

        return sb.ToString().Trim();
    }

    private static string NormalizeForMatch(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray();
        return new string(chars).ToLowerInvariant().Trim();
    }

    private async Task<string?> RefreshSoundCloudStreamAsync(Track track, CancellationToken ct)
    {
        var clientId = _settings.Current.SoundCloudClientId;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(track.SourceId))
            return null;

        try
        {
            var metaUrl =
                $"https://api-v2.soundcloud.com/tracks/{track.SourceId}?client_id={Uri.EscapeDataString(clientId)}";
            using var resp = await _http.GetAsync(metaUrl, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var progressiveUrl = FindSoundCloudProgressiveUrl(json);
            if (progressiveUrl == null) return null;

            var resolveUrl = $"{progressiveUrl}?client_id={Uri.EscapeDataString(clientId)}";
            using var streamResp = await _http.GetAsync(resolveUrl, ct);
            if (!streamResp.IsSuccessStatusCode) return null;

            var streamJson = await streamResp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return streamJson.TryGetProperty("url", out var u) ? u.GetString() : null;
        }
        catch (Exception ex)
        {
            _log.Warning($"SoundCloud stream refresh failed for '{track.Title}': {ex.Message}");
            return null;
        }
    }

    private static string? FindSoundCloudProgressiveUrl(JsonElement track)
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

    private string? RememberStream(string cacheKey, string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
            _urlCache[cacheKey] = url;
        return url;
    }

    private async Task<string?> TryResolveVideoIdAsync(
        string videoId, Track track, string cacheKey, CancellationToken ct)
    {
        try
        {
            var video = await _youtube.Videos.GetAsync(videoId, ct);
            var channelTitle = video.Author?.ChannelTitle ?? string.Empty;
            if (ScoreVideoMatch(video.Title, channelTitle, video.Duration?.TotalSeconds ?? 0, track) < MinMatchScore)
            {
                _log.Info($"Drop stale video-id cache for '{track.Title}' ({video.Title})");
                return null;
            }

            if (track.DurationSeconds > 0 && video.Duration.HasValue)
            {
                var delta = Math.Abs(video.Duration.Value.TotalSeconds - track.DurationSeconds);
                var tolerance = DurationToleranceSeconds(track.DurationSeconds);
                if (delta > tolerance)
                {
                    _log.Info($"Drop stale video-id cache duration for '{track.Title}'");
                    return null;
                }
            }

            var id = new YoutubeExplode.Videos.VideoId(videoId);
            var streamUrl = await TryYoutubeExplodeStreamUrlAsync(id, ct);
            if (!string.IsNullOrWhiteSpace(streamUrl))
            {
                _log.Info($"Fast video-id cache hit: {track.Title}");
                return RememberStream(cacheKey, streamUrl);
            }

            streamUrl = await TryPipedStreamUrlAsync(videoId, ct);
            if (!string.IsNullOrWhiteSpace(streamUrl))
            {
                _log.Info($"Fast Piped cache hit: {track.Title}");
                return RememberStream(cacheKey, streamUrl);
            }
        }
        catch { /* retry full search */ }

        return null;
    }



    private async Task<string?> TryYoutubeExplodeStreamUrlAsync(

        YoutubeExplode.Videos.VideoId videoId, CancellationToken ct)

    {

        try

        {

            var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoId, ct);

            var streams = manifest.GetAudioOnlyStreams().ToList();

            if (streams.Count == 0)

            {

                _log.Warning($"No audio streams in YouTube manifest for {videoId}");

                return null;

            }



            var quality = _settings.Current.AudioQuality;

            var audio = quality switch

            {

                AudioQuality.Low => streams.OrderBy(s => s.Bitrate).FirstOrDefault(),

                AudioQuality.High => streams.OrderByDescending(s => s.Bitrate).FirstOrDefault(),

                _ => streams.OrderBy(s => s.Bitrate).ElementAt(streams.Count / 2)

            } ?? streams.GetWithHighestBitrate();



            var url = audio.Url;

            if (string.IsNullOrWhiteSpace(url))

                return null;



            _log.Info($"YouTube stream URL ({audio.Bitrate}bps) — online playback");

            return url;

        }

        catch (Exception ex)

        {

            _log.Error($"YoutubeExplode failed for {videoId}", ex);

            return null;

        }

    }



    private async Task<string?> TryPipedStreamUrlAsync(string videoId, CancellationToken ct)

    {

        foreach (var instance in PipedInstances)

        {

            try

            {

                var baseUri = new Uri(instance.TrimEnd('/') + "/", UriKind.Absolute);
                var url = new Uri(baseUri, $"streams/{videoId}").AbsoluteUri;

                using var resp = await _http.GetAsync(url, ct);

                if (!resp.IsSuccessStatusCode)

                {

                    _log.Warning($"Piped HTTP {(int)resp.StatusCode} from {instance}");

                    continue;

                }



                var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

                if (!json.TryGetProperty("audioStreams", out var audioStreams))

                    continue;



                string? streamUrl = null;

                var bestBitrate = -1;

                foreach (var stream in audioStreams.EnumerateArray())

                {

                    if (!stream.TryGetProperty("url", out var urlEl)) continue;

                    var u = urlEl.GetString();

                    if (string.IsNullOrWhiteSpace(u)) continue;



                    var bitrate = stream.TryGetProperty("bitrate", out var br) ? br.GetInt32() : 0;

                    if (bitrate > bestBitrate)

                    {

                        bestBitrate = bitrate;

                        streamUrl = u;

                    }

                }



                if (!string.IsNullOrWhiteSpace(streamUrl))

                {

                    _log.Info($"Piped stream URL ({bestBitrate}kbps) — online playback");

                    return streamUrl;

                }

            }

            catch (Exception ex)

            {

                _log.Warning($"Piped instance {instance} failed: {ex.Message}");

            }

        }



        return null;

    }



    private static bool NeedsFullStream(Track track)
    {
        if (track.Source == MusicSource.Local
            && !string.IsNullOrWhiteSpace(track.StreamUrl)
            && File.Exists(track.StreamUrl))
            return false;

        // SoundCloud CDN links expire — always fetch a fresh URL.
        if (track.Source == MusicSource.SoundCloud)
            return true;

        if (!string.IsNullOrWhiteSpace(track.StreamUrl)
            && track.StreamUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            && !track.StreamUrl.Contains("preview", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(track.StreamUrl))
            return true;

        if (track.Source is MusicSource.Deezer or MusicSource.Spotify)
            return true;

        if (track.StreamUrl.Contains("preview", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public void InvalidateCachedStream(Track track)
    {
        var key = CacheKey(track);
        _urlCache.TryRemove(key, out _);
        _videoIdCache.TryRemove(key, out _);
    }
}


