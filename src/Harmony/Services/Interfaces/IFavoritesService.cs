using Harmony.Models;

namespace Harmony.Services.Interfaces;

/// <summary>Manages "liked" tracks.</summary>
public interface IFavoritesService
{
    /// <summary>Toggle the like state for a track; returns the new state.</summary>
    Task<bool> ToggleAsync(Track track);

    Task<bool> IsFavoriteAsync(MusicSource source, string sourceId);

    /// <summary>All favorited tracks, newest first.</summary>
    Task<IReadOnlyList<Track>> GetFavoritesAsync(int limit = 500);

    Task ClearAllAsync();

    /// <summary>Add to favorites if not already liked. Returns true if newly added.</summary>
    Task<bool> EnsureFavoriteAsync(Track track);
}
