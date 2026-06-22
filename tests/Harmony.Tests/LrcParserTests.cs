using Harmony.Services;

namespace Harmony.Tests;

public class LrcParserTests
{
    [Fact]
    public void Parse_reads_centisecond_timestamps()
    {
        var lines = LrcParser.Parse("[00:12.50] Hello world");
        Assert.Single(lines);
        Assert.Equal("Hello world", lines[0].Text);
        Assert.InRange(lines[0].StartSeconds, 12.49, 12.51);
    }

    [Fact]
    public void DistributePlain_splits_evenly()
    {
        var lines = LrcParser.DistributePlain("one\ntwo\nthree", 90);
        Assert.Equal(3, lines.Count);
        Assert.Equal(0, lines[0].StartSeconds);
        Assert.Equal(30, lines[1].StartSeconds, 1);
    }
}
