using Harmony.Models;

namespace Harmony.Services.Interfaces;

/// <summary>CRUD operations for playlists and their tracks.</summary>
public interface IPlaylistService
{
    Task<IReadOnlyList<Playlist>> GetPlaylistsAsync();

    Task<Playlist> CreateAsync(string name);

    Task RenameAsync(int playlistId, string newName);

    Task DeleteAsync(int playlistId);

    /// <summary>Add a track to a playlist (appends to the end).</summary>
    Task AddTrackAsync(int playlistId, Track track);

    Task RemoveTrackAsync(int playlistId, int trackId);

    /// <summary>Tracks of a playlist in their stored order.</summary>
    Task<IReadOnlyList<Track>> GetTracksAsync(int playlistId);

    /// <summary>Tracks with position and added-at for playlist UI.</summary>
    Task<IReadOnlyList<PlaylistTrackEntry>> GetTrackEntriesAsync(int playlistId);
}
