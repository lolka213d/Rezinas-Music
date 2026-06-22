using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>Last.fm track.scrobble when enabled in settings.</summary>
public sealed class LastFmScrobbler
{
    private readonly ISettingsService _settings;
    private readonly HttpClient _http;
    private readonly IAppLog _log;

    private Track? _pendingTrack;
    private DateTime _startedUtc;
    private bool _submitted;

    public LastFmScrobbler(ISettingsService settings, HttpClient http, IAppLog log)
    {
        _settings = settings;
        _http = http;
        _log = log;
    }

    public void OnTrackStarted(Track track)
    {
        _pendingTrack = track;
        _startedUtc = DateTime.UtcNow;
        _submitted = false;
    }

    public void OnPositionChanged(Track? track, double positionSeconds, double durationSeconds)
    {
        if (_submitted || track == null || !IsEnabled) return;
        if (_pendingTrack == null || !_pendingTrack.Matches(track)) return;

        var listened = positionSeconds >= 240 || (durationSeconds > 0 && positionSeconds >= durationSeconds * 0.5);
        if (!listened) return;

        _submitted = true;
        _ = SubmitAsync(track, _startedUtc, DateTime.UtcNow);
    }

    public void OnTrackStopped() => _pendingTrack = null;

    private bool IsEnabled =>
        _settings.Current.LastFmEnabled
        && !string.IsNullOrWhiteSpace(_settings.Current.LastFmApiKey)
        && !string.IsNullOrWhiteSpace(_settings.Current.LastFmSessionKey);

    private async Task SubmitAsync(Track track, DateTime started, DateTime ended)
    {
        try
        {
            var s = _settings.Current;
            var apiKey = s.LastFmApiKey!;
            var session = s.LastFmSessionKey!;
            var sig = Sign(apiKey, session, track, started, ended, s.LastFmApiSecret);

            var form = new Dictionary<string, string>
            {
                ["method"] = "track.scrobble",
                ["api_key"] = apiKey,
                ["sk"] = session,
                ["artist"] = track.ArtistName,
                ["track"] = track.Title,
                ["timestamp"] = ((long)(started - DateTime.UnixEpoch).TotalSeconds).ToString(),
                ["api_sig"] = sig,
                ["format"] = "json"
            };
            if (!string.IsNullOrWhiteSpace(track.AlbumName))
                form["album"] = track.AlbumName;

            using var content = new FormUrlEncodedContent(form);
            var resp = await _http.PostAsync("https://ws.audioscrobbler.com/2.0/", content);
            if (!resp.IsSuccessStatusCode)
                _log.Warning($"Last.fm scrobble failed: HTTP {(int)resp.StatusCode}");
            else
                _log.Info($"Last.fm scrobble: {track.ArtistName} — {track.Title}");
        }
        catch (Exception ex)
        {
            _log.Error("Last.fm scrobble error", ex);
        }
    }

    private static string Sign(string apiKey, string session, Track track, DateTime started, DateTime ended, string? secret)
    {
        var ts = ((long)(started - DateTime.UnixEpoch).TotalSeconds).ToString();
        var raw = $"api_key{apiKey}artist{track.ArtistName}methodtrack.scrobble" +
                  $"sk{session}timestamp{ts}track{track.Title}{secret ?? ""}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
