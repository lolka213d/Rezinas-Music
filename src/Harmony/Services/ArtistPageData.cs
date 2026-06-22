using Harmony.Models;

using Harmony.ViewModels;



namespace Harmony.Services;



/// <summary>Deezer artist page payload.</summary>

public sealed record ArtistPageData(

    string Name,

    string? PictureUrl,

    string? Bio,

    int Fans,

    IReadOnlyList<Track> TopTracks,

    IReadOnlyList<HomeAlbumCard> Albums);


