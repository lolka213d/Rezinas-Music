using Harmony.Models;

namespace Harmony.Services.Interfaces;

/// <summary>
/// Abstraction over a music search provider (YouTube, Spotify, Sample, ...).
/// Implementations return transient <see cref="Track"/> objects that are not
/// yet persisted.
/// </summary>
public interface IMusicSearchService
{
    /// <summary>The source this provider represents.</summary>
    MusicSource Source { get; }

    /// <summary>Friendly provider name for the UI.</summary>
    string DisplayName { get; }

    /// <summary>True when the provider is configured/usable (e.g. has an API key).</summary>
    bool IsAvailable { get; }

    /// <summary>Search for tracks matching <paramref name="query"/>.</summary>
    Task<IReadOnlyList<Track>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>Human-readable error from the last search (null when OK or not run yet).</summary>
    string? LastError { get; }
}
