using System.Text.Json;
using Harmony.Models;

namespace Harmony.Services;

/// <summary>Parses Spotify Web API track JSON into <see cref="Track"/>.</summary>
public static class SpotifyTrackParser
{
    public static Track? FromItem(JsonElement item)
    {
        if (item.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        if (item.TryGetProperty("is_local", out var outerLocal) && outerLocal.GetBoolean())
            return null;

        if (item.TryGetProperty("track", out var wrapped))
        {
            if (wrapped.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;
            item = wrapped;
        }

        if (item.TryGetProperty("is_local", out var local) && local.GetBoolean())
            return null;

        if (item.TryGetProperty("type", out var typeEl)
            && typeEl.GetString() is { } type
            && !string.Equals(type, "track", StringComparison.OrdinalIgnoreCase))
            return null;

        var title = item.TryGetProperty("name", out var t) ? t.GetString() ?? "Unknown" : "Unknown";
        var artist = item.TryGetProperty("artists", out var artistsEl) && artistsEl.GetArrayLength() > 0
            ? ReadArtistName(artistsEl[0])
            : "";
        var album = item.TryGetProperty("album", out var albumEl) ? ReadAlbumName(albumEl) : null;
        string? cover = null;
        if (item.TryGetProperty("album", out var al) && al.TryGetProperty("images", out var imgs) && imgs.GetArrayLength() > 0)
            cover = imgs[0].TryGetProperty("url", out var url) ? url.GetString() : null;

        var durationMs = item.TryGetProperty("duration_ms", out var d) ? d.GetInt32() : 0;
        var preview = item.TryGetProperty("preview_url", out var p) ? p.GetString() : null;
        var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id)) return null;

        return new Track
        {
            Title = title,
            ArtistName = artist,
            AlbumName = album,
            DurationSeconds = durationMs / 1000,
            Source = MusicSource.Spotify,
            SourceId = id,
            ThumbnailUrl = cover,
            StreamUrl = preview
        };
    }

    private static string ReadArtistName(JsonElement artistEl)
    {
        if (artistEl.TryGetProperty("name", out var nameEl))
            return nameEl.GetString() ?? "";
        return "";
    }

    private static string? ReadAlbumName(JsonElement albumEl)
    {
        if (albumEl.TryGetProperty("name", out var nameEl))
            return nameEl.GetString();
        return null;
    }
}
