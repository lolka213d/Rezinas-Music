namespace Harmony.Models;

/// <summary>One line of lyrics with optional sync timestamp (seconds from track start).</summary>
public sealed class LyricLine
{
    public LyricLine(string text, double startSeconds)
    {
        Text = text;
        StartSeconds = startSeconds;
    }

    public string Text { get; }
    public double StartSeconds { get; }
}

/// <summary>Lyrics payload: synced lines and/or plain fallback text.</summary>
public sealed class LyricsData
{
    public LyricsData(IReadOnlyList<LyricLine> lines, bool isSynced, string? plainText = null)
    {
        Lines = lines;
        IsSynced = isSynced;
        PlainText = plainText;
    }

    public IReadOnlyList<LyricLine> Lines { get; }
    public bool IsSynced { get; }
    public string? PlainText { get; }
}
