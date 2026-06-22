namespace Harmony.ViewModels;



public sealed class ArtistNavigationContext

{

    public string? DeezerArtistId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? ThumbnailUrl { get; init; }



    public static ArtistNavigationContext FromCard(SearchArtistCard card) => new()

    {

        DeezerArtistId = card.SourceId,

        Name = card.Name,

        ThumbnailUrl = card.ThumbnailUrl

    };

}


