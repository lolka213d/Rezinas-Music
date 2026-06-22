using Harmony.Models;
using Harmony.Services;

namespace Harmony.Tests;

public class RadioDailySeedTests
{
    [Fact]
    public void Same_day_produces_same_shuffle_order()
    {
        var tracks = Enumerable.Range(1, 10).Select(i => new Track
        {
            Title = $"Track {i}",
            Source = MusicSource.Deezer,
            SourceId = i.ToString()
        }).ToList();

        var a = RadioDailySeed.ShuffleForDay(tracks, "ru", 738_000);
        var b = RadioDailySeed.ShuffleForDay(tracks, "ru", 738_000);

        Assert.Equal(a.Select(t => t.SourceId), b.Select(t => t.SourceId));
    }

    [Fact]
    public void Different_day_changes_order()
    {
        var tracks = Enumerable.Range(1, 12).Select(i => new Track
        {
            Title = $"Track {i}",
            Source = MusicSource.Deezer,
            SourceId = i.ToString()
        }).ToList();

        var day1 = RadioDailySeed.ShuffleForDay(tracks, "ru", 738_000);
        var day2 = RadioDailySeed.ShuffleForDay(tracks, "ru", 738_001);

        Assert.NotEqual(day1.Select(t => t.SourceId), day2.Select(t => t.SourceId));
    }
}
