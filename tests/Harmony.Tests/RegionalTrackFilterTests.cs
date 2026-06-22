using Harmony.Models;
using Harmony.Services;

namespace Harmony.Tests;

public class RegionalTrackFilterTests
{
    [Fact]
    public void Apply_ru_keeps_cyrillic_tracks()
    {
        var tracks = new[]
        {
            new Track { Title = "Моя песня", ArtistName = "Исполнитель", Source = MusicSource.Deezer, SourceId = "1" },
            new Track { Title = "Blinding Lights", ArtistName = "The Weeknd", Source = MusicSource.Deezer, SourceId = "2" },
        };

        var filtered = RegionalTrackFilter.Apply("ru", tracks, 10);

        Assert.Single(filtered);
        Assert.Equal("Моя песня", filtered[0].Title);
    }
}
