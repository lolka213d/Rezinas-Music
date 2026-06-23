namespace Harmony.Services.Interfaces;

/// <summary>
/// Resolves a full-length playable stream URL for tracks that only expose
/// 30-second previews (Deezer/Spotify) or have no direct URL.
/// </summary>
public interface IStreamResolverService
{
    Task<string?> ResolveFullStreamAsync(Models.Track track, CancellationToken cancellationToken = default);

    /// <summary>Clears in-memory stream cache so the next resolve is fresh.</summary>
    void InvalidateCachedStream(Models.Track track);
}
