using System.Text.Json;
using Harmony.Models;
using Harmony.Services;

namespace Harmony.Tests;

public class SpotifyTrackParserTests
{
    [Fact]
    public void FromItem_parses_legacy_liked_songs_entry_with_track_field()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "added_at": "2024-01-01T00:00:00Z",
              "track": {
                "type": "track",
                "id": "abc123",
                "name": "Test Song",
                "duration_ms": 180000,
                "artists": [{ "name": "Test Artist" }],
                "album": { "name": "Test Album" }
              }
            }
            """);

        var track = SpotifyTrackParser.FromItem(doc.RootElement);

        Assert.NotNull(track);
        Assert.Equal("Test Song", track!.Title);
        Assert.Equal("Test Artist", track.ArtistName);
        Assert.Equal(MusicSource.Spotify, track.Source);
        Assert.Equal("abc123", track.SourceId);
    }

    [Fact]
    public void FromItem_parses_playlist_items_entry_with_item_field()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "added_at": "2024-01-01T00:00:00Z",
              "is_local": false,
              "item": {
                "type": "track",
                "id": "xyz789",
                "name": "loser club",
                "duration_ms": 205000,
                "artists": [{ "name": "wifiskeleton" }],
                "album": { "name": "single" }
              }
            }
            """);

        var track = SpotifyTrackParser.FromItem(doc.RootElement);

        Assert.NotNull(track);
        Assert.Equal("loser club", track!.Title);
        Assert.Equal("wifiskeleton", track.ArtistName);
        Assert.Equal("xyz789", track.SourceId);
    }
}
