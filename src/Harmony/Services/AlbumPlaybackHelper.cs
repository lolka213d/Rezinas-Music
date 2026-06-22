using Harmony.Models;
using Harmony.ViewModels;

namespace Harmony.Services;

/// <summary>Opens an album page and starts playback from its tracks.</summary>
public static class AlbumPlaybackHelper
{
    public static async Task OpenAndPlayAsync(
        HomeAlbumCard album,
        DeezerHomeService deezer,
        NavigationService navigation,
        PlayerViewModel player,
        CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<Track> tracks;

            if (!string.IsNullOrWhiteSpace(album.SourceId))
            {
                tracks = await deezer.GetAlbumTracksAsync(NormalizeId(album.SourceId), ct);
                if (tracks.Count == 0 && album.PlayTrack != null)
                    tracks = new[] { album.PlayTrack };
            }
            else if (album.PlayTrack != null)
            {
                tracks = new[] { album.PlayTrack };
            }
            else
            {
                navigation.OpenAlbum(AlbumNavigationContext.FromCard(album));
                return;
            }

            if (tracks.Count == 0)
            {
                navigation.OpenAlbum(AlbumNavigationContext.FromCard(album));
                return;
            }

            var start = tracks[0];
            navigation.OpenAlbum(AlbumNavigationContext.FromTrackList(album.Title, tracks, start));
            await player.PlayQueueAsync(tracks, start);
        }
        catch
        {
            navigation.OpenAlbum(AlbumNavigationContext.FromCard(album));
        }
    }

    private static string NormalizeId(string id) => id.Trim().Trim('"');
}
