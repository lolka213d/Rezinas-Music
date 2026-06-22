namespace Harmony.Services;

public sealed record TrackCreditLine(
    string Name,
    string Role,
    string SpotifyUrl,
    bool IsMainArtist);

public sealed class TrackContextData
{
    public string PrimaryArtistName { get; init; } = string.Empty;
    public string? DeezerArtistId { get; init; }
    public string? ArtistImageUrl { get; init; }
    public string? ArtistBio { get; init; }
    public string FansLine { get; init; } = string.Empty;
    public string TrackSpotifyUrl { get; init; } = string.Empty;
    public string PrimaryArtistSpotifyUrl { get; init; } = string.Empty;
    public IReadOnlyList<TrackCreditLine> Credits { get; init; } = Array.Empty<TrackCreditLine>();
}
