using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
    private const int MaxSearchCandidates = 10;

    private sealed record DeezerTrackMeta(string Title, string Artist, int DurationSeconds, string? Preview);

    private sealed record ScoredVideo(string VideoId, string Title, int Score);

    public YouTubeStreamResolver(IAppLog log, HttpClient http, ISettingsService settings)
    {
        _log = log;
        _http = http;
        _settings = settings;
    }

    public async Task<string?> ResolveFullStreamAsync(Track track, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKey(track);
        var searchArtist = track.ArtistName;
        var searchTitle = track.Title;

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
                if (!string.IsNullOrWhiteSpace(meta.Title))
                    searchTitle = meta.Title;
                if (!string.IsNullOrWhiteSpace(meta.Artist))
                    searchArtist = meta.Artist;
                if (meta.DurationSeconds > 0)
                    track.DurationSeconds = meta.DurationSeconds;
                if (!string.IsNullOrWhiteSpace(meta.Preview))
                    track.StreamUrl = meta.Preview;
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

        var probe = CloneForSearch(track, searchArtist, searchTitle);
        var titlePhrase = PrimaryTitle(searchTitle);
        var baseQuery = $"{searchArtist} {titlePhrase}".Trim();
        if (string.IsNullOrWhiteSpace(baseQuery))
        {
            _log.Warning($"Empty search query for track '{track.Title}'.");
            return track.StreamUrl;
        }

        var queries = BuildQueries(searchArtist, titlePhrase, track.AlbumName, baseQuery);

        foreach (var query in queries)
        {
            _log.Info($"Resolving online stream via YouTube: {query}");
            var streamUrl = await TryYouTubeSearchAsync(probe, query, cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(streamUrl))
                return streamUrl;
        }

        if (titlePhrase.Length >= 2)
        {
            _log.Info($"Relaxed title-only YouTube search: {titlePhrase}");
            var streamUrl = await TryYouTubeSearchAsync(probe, titlePhrase, cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(streamUrl))
                return streamUrl;
        }

        _log.Warning($"YouTube + Piped: no stream for '{baseQuery}'");
        if (!string.IsNullOrWhiteSpace(track.StreamUrl))
            _log.Warning($"Falling back to preview URL for '{track.Title}'");

        return track.StreamUrl;
    }

    public void InvalidateCachedStream(Track track)
    {
        var key = CacheKey(track);
        _urlCache.TryRemove(key, out _);
        _videoIdCache.TryRemove(key, out _);
    }

    private static string CacheKey(Track track) =>
        string.IsNullOrWhiteSpace(track.SourceId)
            ? $"v2:{track.Source}:{track.ArtistName}:{track.Title}"
            : $"v2:{track.Source}:{track.SourceId}";

    private static Track CloneForSearch(Track track, string artist, string title) =>
        new()
        {
            Source = track.Source,
            SourceId = track.SourceId,
            ArtistName = artist,
            Title = title,
            DurationSeconds = track.DurationSeconds,
            AlbumName = track.AlbumName
        };

    private static IEnumerable<string> BuildQueries(string artist, string title, string? album, string baseQuery)
    {
        var list = new List<string>
        {
            $"{artist} - {title}",
            $"{artist} {title} official audio",
            $"{artist} {title}",
            baseQuery
        };

        var latin = TransliterateCyrillicToLatin(NormalizeForMatch(artist));
        var normalizedArtist = NormalizeForMatch(artist);
        if (latin.Length >= 2 && !latin.Equals(normalizedArtist, StringComparison.OrdinalIgnoreCase))
        {
            list.Add($"{latin} - {title}");
            list.Add($"{latin} {title} official audio");
        }

        if (!string.IsNullOrWhiteSpace(album))
        {
            var albumPhrase = PrimaryAlbum(album);
            if (albumPhrase.Length > 2)
                list.Add($"{artist} {title} {albumPhrase}");
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string?> TryYouTubeSearchAsync(
        Track track, string query, string cacheKey, CancellationToken cancellationToken)
    {
        var candidates = 0;
        ScoredVideo? bestStrict = null;
        ScoredVideo? bestRelaxed = null;
        var minVideoDuration = MinVideoDurationSeconds(track);

        await foreach (var video in _youtube.Search.GetVideosAsync(query, cancellationToken))
        {
            if (++candidates > MaxSearchCandidates)
                break;

            var videoTitle = video.Title ?? string.Empty;
            var channelTitle = video.Author?.ChannelTitle ?? string.Empty;
            var videoDuration = video.Duration?.TotalSeconds ?? 0;
            if (videoDuration < minVideoDuration) continue;

            var strictScore = ScoreCandidate(videoTitle, channelTitle, videoDuration, track);
            if (strictScore > 0 && (bestStrict == null || strictScore > bestStrict.Score))
                bestStrict = new ScoredVideo(video.Id.Value, videoTitle, strictScore);

            var relaxedScore = ScoreCandidateRelaxed(videoTitle, channelTitle, videoDuration, track);
            if (relaxedScore > 0 && (bestRelaxed == null || relaxedScore > bestRelaxed.Score))
                bestRelaxed = new ScoredVideo(video.Id.Value, videoTitle, relaxedScore);
        }

        var best = bestStrict ?? bestRelaxed;
        if (best == null) return null;

        _log.Info(bestStrict != null
            ? $"Best YouTube match (strict, score {best.Score}): {best.Title} ({best.VideoId})"
            : $"Best YouTube match (relaxed, score {best.Score}): {best.Title} ({best.VideoId})");

        var streamUrl = await TryYoutubeExplodeStreamUrlAsync(new YoutubeExplode.Videos.VideoId(best.VideoId), cancellationToken);
        if (string.IsNullOrWhiteSpace(streamUrl))
            streamUrl = await TryPipedStreamUrlAsync(best.VideoId, cancellationToken);

        if (string.IsNullOrWhiteSpace(streamUrl)) return null;

        _videoIdCache[cacheKey] = best.VideoId;
        _log.Info($"Online stream ready: {track.Title}");
        return RememberStream(cacheKey, streamUrl);
    }

    private static int ScoreCandidate(string videoTitle, string channelTitle, double videoDuration, Track track)
    {
        var haystack = $"{NormalizeForMatch(videoTitle)} {NormalizeForMatch(channelTitle)}";
        if (haystack.Length == 0) return 0;

        if (!ArtistMatches(haystack, track.ArtistName)) return 0;

        var title = NormalizeForMatch(PrimaryTitle(track.Title));
        if (title.Length == 0) return 0;

        var titleTokens = TitleTokens(title, minLength: 3);
        var titleHits = titleTokens.Count == 0
            ? (haystack.Contains(title, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            : titleTokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
        var titleRatio = titleTokens.Count == 0 ? 1.0 : (double)titleHits / titleTokens.Count;
        if (titleRatio < 0.5) return 0;

        if (track.DurationSeconds > 0)
        {
            var delta = Math.Abs(videoDuration - track.DurationSeconds);
            var tolerance = DurationToleranceSeconds(track.DurationSeconds);
            if (delta > tolerance) return 0;
        }

        var score = (int)Math.Round(titleRatio * 40);
        score += ArtistMatchStrength(haystack, track.ArtistName);

        if (NormalizeForMatch(videoTitle).Contains("official", StringComparison.OrdinalIgnoreCase)) score += 8;
        if (NormalizeForMatch(videoTitle).Contains("audio", StringComparison.OrdinalIgnoreCase)) score += 4;

        if (track.DurationSeconds > 0 && videoDuration > 0)
        {
            var delta = Math.Abs(videoDuration - track.DurationSeconds);
            var tolerance = DurationToleranceSeconds(track.DurationSeconds);
            score += delta == 0 ? 25 : (int)Math.Round((1.0 - delta / tolerance) * 20);
        }

        return score;
    }

    private static int DurationToleranceSeconds(int durationSeconds)
    {
        if (durationSeconds <= 0) return 30;
        return Math.Clamp((int)Math.Round(durationSeconds * 0.12), 15, 35);
    }

    private static int RelaxedDurationToleranceSeconds(int durationSeconds)
    {
        if (durationSeconds <= 0) return 45;
        return Math.Clamp((int)Math.Round(durationSeconds * 0.30), 25, 55);
    }

    private static double MinVideoDurationSeconds(Track track)
    {
        if (track.DurationSeconds > 0)
            return Math.Clamp(track.DurationSeconds * 0.35, 30, 45);
        return 30;
    }

    private static List<string> TitleTokens(string title, int minLength)
        => title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= minLength)
            .ToList();

    private static int ScoreCandidateRelaxed(string videoTitle, string channelTitle, double videoDuration, Track track)
    {
        var haystack = $"{NormalizeForMatch(videoTitle)} {NormalizeForMatch(channelTitle)}";
        if (haystack.Length == 0) return 0;

        var title = NormalizeForMatch(PrimaryTitle(track.Title));
        if (title.Length == 0) return 0;

        var hasArtist = ArtistMatches(haystack, track.ArtistName);
        var fullTitleMatch = haystack.Contains(title, StringComparison.OrdinalIgnoreCase);

        var titleTokens = TitleTokens(title, minLength: 2);
        var titleHits = titleTokens.Count == 0
            ? (fullTitleMatch ? 1 : 0)
            : titleTokens.Count(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
        var titleRatio = titleTokens.Count == 0 ? 1.0 : (double)titleHits / titleTokens.Count;

        if (!hasArtist && !fullTitleMatch) return 0;
        if (titleRatio < 0.5) return 0;

        if (track.DurationSeconds > 0)
        {
            var delta = Math.Abs(videoDuration - track.DurationSeconds);
            var tolerance = RelaxedDurationToleranceSeconds(track.DurationSeconds);
            if (delta > tolerance) return 0;
        }

        var score = (int)Math.Round(titleRatio * 30);
        if (fullTitleMatch) score += 20;
        if (hasArtist) score += ArtistMatchStrength(haystack, track.ArtistName);

        if (track.DurationSeconds > 0 && videoDuration > 0)
        {
            var delta = Math.Abs(videoDuration - track.DurationSeconds);
            var tolerance = RelaxedDurationToleranceSeconds(track.DurationSeconds);
            score += delta == 0 ? 15 : (int)Math.Round((1.0 - delta / tolerance) * 12);
        }

        return score;
    }

    private static string PrimaryTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return title;
        var cut = title.Split(['.', '·', '|', '—', '-'], 2, StringSplitOptions.TrimEntries);
        return cut[0].Trim();
    }

    private static string PrimaryAlbum(string? album)
    {
        if (string.IsNullOrWhiteSpace(album)) return string.Empty;
        var cut = album.Split(['.', '·', '|'], 2, StringSplitOptions.TrimEntries);
        return cut[0].Trim();
    }

    private static bool ArtistMatches(string haystack, string artistName)
        => ArtistMatchStrength(haystack, artistName) > 0;

    private static int ArtistMatchStrength(string haystack, string artistName)
    {
        if (string.IsNullOrWhiteSpace(artistName)) return 15;

        foreach (var variant in ArtistVariants(artistName))
        {
            if (variant.Length >= 2 && haystack.Contains(variant, StringComparison.OrdinalIgnoreCase))
                return variant.Length >= 4 ? 35 : 25;
        }

        return 0;
    }

    private static IEnumerable<string> ArtistVariants(string artist)
    {
        var primary = artist.Split(',')[0].Trim();
        var normalized = NormalizeForMatch(primary);
        if (normalized.Length > 0) yield return normalized;

        var latin = TransliterateCyrillicToLatin(normalized);
        if (latin.Length > 0 && !latin.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            yield return latin;
    }

    private static string NormalizeForMatch(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var chars = value.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray();
        return new string(chars).ToLowerInvariant().Trim();
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

            if (!found) sb.Append(lower);
        }

        return sb.ToString().Trim();
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

    private async Task<string?> TryResolveVideoIdAsync(
        string videoId, Track track, string cacheKey, CancellationToken ct)
    {
        try
        {
            var video = await _youtube.Videos.GetAsync(videoId, ct);
            var channel = video.Author?.ChannelTitle ?? string.Empty;
            if (ScoreCandidate(video.Title, channel, video.Duration?.TotalSeconds ?? 0, track) <= 0)
            {
                _log.Info($"Drop stale video cache for '{track.Title}' ({video.Title})");
                return null;
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

    private async Task<string?> TryYoutubeExplodeStreamUrlAsync(YoutubeExplode.Videos.VideoId videoId, CancellationToken ct)
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
            if (string.IsNullOrWhiteSpace(url)) return null;

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
                if (!json.TryGetProperty("audioStreams", out var audioStreams)) continue;

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
}
