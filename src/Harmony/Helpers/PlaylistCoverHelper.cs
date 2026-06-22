using Harmony.Services.Interfaces;

namespace Harmony.Helpers;

public static class PlaylistCoverHelper
{
    public static async Task<IReadOnlyList<string?>> GetThumbnailsAsync(IPlaylistService playlists, int playlistId)
    {
        try
        {
            var tracks = await playlists.GetTracksAsync(playlistId);
            return tracks
                .Select(t => t.ThumbnailUrl)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Take(4)
                .Cast<string?>()
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
