namespace Harmony.Services;

/// <summary>Deezer album metadata for the album detail page.</summary>
public sealed record AlbumInfo(
    string Title,
    string ArtistName,
    string? CoverUrl,
    int? Year,
    int TrackCount,
    string? Label,
    string? RecordType,
    int Fans);
