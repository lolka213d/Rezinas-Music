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
    private const int MaxSearchCandidates = 6;



    public YouTubeStreamResolver(IAppLog log, HttpClient http, ISettingsService settings)

    {

        _log = log;

        _http = http;

        _settings = settings;

    }



    public async Task<string?> ResolveFullStreamAsync(Track track, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{track.Source}:{track.SourceId}";

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
            var preview = await RefreshDeezerPreviewAsync(track.SourceId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(preview))
                track.StreamUrl = preview;
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
            var cachedUrl = await TryResolveVideoIdAsync(knownVideoId, track.Title, cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cachedUrl))
                return cachedUrl;
            _videoIdCache.TryRemove(cacheKey, out _);
        }

        var baseQuery = $"{track.ArtistName} {track.Title}".Trim();
        if (string.IsNullOrWhiteSpace(baseQuery))
        {
            _log.Warning($"Empty search query for track '{track.Title}', using preview.");
            return track.StreamUrl;
        }

        var queries = new[]
        {
            $"{baseQuery} official audio",
            baseQuery
        };

        foreach (var query in queries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _log.Info($"Resolving online stream via YouTube: {query}");
            var streamUrl = await TryYouTubeSearchAsync(track, query, cacheKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(streamUrl))
                return streamUrl;
        }

        _log.Warning($"YouTube + Piped: no stream for '{baseQuery}'");

        if (!string.IsNullOrWhiteSpace(track.StreamUrl))
            _log.Warning($"Falling back to preview URL for '{track.Title}'");

        return track.StreamUrl;
    }

    private async Task<string?> TryYouTubeSearchAsync(
        Track track, string query, string cacheKey, CancellationToken cancellationToken)
    {
        var candidates = 0;
        await foreach (var video in _youtube.Search.GetVideosAsync(query, cancellationToken))
        {
            if (++candidates > MaxSearchCandidates)
                break;

            var videoDuration = video.Duration?.TotalSeconds ?? 0;
            if (videoDuration < 30)
            {
                _log.Info($"Skip short video '{video.Title}' ({videoDuration:F0}s)");
                continue;
            }

            if (track.DurationSeconds > 0)
            {
                var delta = Math.Abs(videoDuration - track.DurationSeconds);
                var tolerance = track.DurationSeconds < 180 ? 75 : 45;
                if (delta > tolerance)
                {
                    _log.Info($"Skip duration mismatch '{video.Title}' ({videoDuration:F0}s vs {track.DurationSeconds}s)");
                    continue;
                }
            }

            var videoId = video.Id.Value;
            _videoIdCache[cacheKey] = videoId;

            _log.Info($"Trying YouTube video: {video.Title} ({videoId})");

            var streamUrl = await TryYoutubeExplodeStreamUrlAsync(video.Id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(streamUrl))
            {
                _log.Info($"Online stream ready (YouTube): {track.Title}");
                return RememberStream(cacheKey, streamUrl);
            }

            streamUrl = await TryPipedStreamUrlAsync(videoId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(streamUrl))
            {
                _log.Info($"Online stream ready (Piped): {track.Title}");
                return RememberStream(cacheKey, streamUrl);
            }
        }

        return null;
    }

    private async Task<string?> RefreshDeezerPreviewAsync(string sourceId, CancellationToken ct)
    {
        try
        {
            var url = $"https://api.deezer.com/track/{sourceId}";
            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return json.TryGetProperty("preview", out var p) ? p.GetString() : null;
        }
        catch (Exception ex)
        {
            _log.Warning($"Deezer preview refresh failed for {sourceId}: {ex.Message}");
            return null;
        }
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
        string videoId, string trackTitle, string cacheKey, CancellationToken ct)
    {
        try
        {
            var id = new YoutubeExplode.Videos.VideoId(videoId);
            var streamUrl = await TryYoutubeExplodeStreamUrlAsync(id, ct);
            if (!string.IsNullOrWhiteSpace(streamUrl))
            {
                _log.Info($"Fast video-id cache hit: {trackTitle}");
                return RememberStream(cacheKey, streamUrl);
            }

            streamUrl = await TryPipedStreamUrlAsync(videoId, ct);
            if (!string.IsNullOrWhiteSpace(streamUrl))
            {
                _log.Info($"Fast Piped cache hit: {trackTitle}");
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
        var key = $"{track.Source}:{track.SourceId}";
        _urlCache.TryRemove(key, out _);
        _videoIdCache.TryRemove(key, out _);
    }
}


