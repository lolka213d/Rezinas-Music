using Harmony.Models;

namespace Harmony.Services.Interfaces;

public interface IAlbumService
{
    Task<IReadOnlyList<Album>> GetUserAlbumsAsync();
    Task<Album?> GetUserAlbumAsync(int albumId);
    Task<Album> CreateAsync(string name, string? artistName = null);
    Task RenameAsync(int albumId, string newName);
    Task DeleteAsync(int albumId);
    Task AddTrackAsync(int albumId, Track track);
    Task RemoveTrackAsync(int albumId, int trackId);
    Task<IReadOnlyList<Track>> GetTracksAsync(int albumId);
    Task SetImageAsync(int albumId, string? imagePath);
}
