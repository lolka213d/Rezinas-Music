using Harmony.Models;

namespace Harmony.Services.Interfaces;

/// <summary>
/// Manages persisted tracks (the user's library) and is also responsible for
/// turning a transient search result into a saved <see cref="Track"/> row,
/// upserting the related Artist/Album records along the way.
/// </summary>
public interface ILibraryService
{
    /// <summary>
    /// Ensure a track exists in the database (matched by Source + SourceId).
    /// Returns the persisted entity id. Does NOT add it to the library list.
    /// </summary>
    Task<int> EnsureTrackAsync(Track track);

    /// <summary>Add a track to the library (sets <c>AddedToLibraryAt</c>).</summary>
    Task AddToLibraryAsync(Track track);

    /// <summary>Remove a track from the library.</summary>
    Task RemoveFromLibraryAsync(int trackId);

    /// <summary>True when the track (by Source + SourceId) is in the library.</summary>
    Task<bool> IsInLibraryAsync(MusicSource source, string sourceId);

    /// <summary>All library tracks, newest first.</summary>
    Task<IReadOnlyList<Track>> GetLibraryAsync();

    /// <summary>Distinct artist names present in the library (for filtering).</summary>
    Task<IReadOnlyList<string>> GetLibraryArtistsAsync();
}
