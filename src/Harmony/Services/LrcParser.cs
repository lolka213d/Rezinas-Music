using System.Globalization;
using System.Text.RegularExpressions;
using Harmony.Models;

namespace Harmony.Services;

/// <summary>Parses LRC synced lyrics ([mm:ss.xx] text).</summary>
public static class LrcParser
{
    private static readonly Regex LineRx = new(
        @"^\[(\d{1,2}):(\d{2})(?:\.(\d{1,3}))?\]\s*(.*)$",
        RegexOptions.Compiled);

    public static IReadOnlyList<LyricLine> Parse(string lrc)
    {
        var lines = new List<LyricLine>();
        foreach (var raw in lrc.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var m = LineRx.Match(line);
            if (!m.Success) continue;

            var min = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var sec = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var frac = m.Groups[3].Success ? m.Groups[3].Value : "0";
            // LRC uses centiseconds (2 digits) or milliseconds (3 digits).
            var fracSeconds = frac.Length switch
            {
                2 => int.Parse(frac, CultureInfo.InvariantCulture) / 100.0,
                3 => int.Parse(frac, CultureInfo.InvariantCulture) / 1000.0,
                _ => int.Parse(frac, CultureInfo.InvariantCulture) / 100.0
            };

            var start = min * 60 + sec + fracSeconds;
            var text = m.Groups[4].Value.Trim();
            if (text.Length > 0)
                lines.Add(new LyricLine(text, start));
        }

        return lines.OrderBy(l => l.StartSeconds).ToList();
    }

    /// <summary>Split plain lyrics into lines with estimated timestamps.</summary>
    public static IReadOnlyList<LyricLine> DistributePlain(string plain, double totalSeconds)
    {
        var parts = plain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return Array.Empty<LyricLine>();

        if (totalSeconds <= 0)
            return parts.Select(p => new LyricLine(p, 0)).ToList();

        var step = totalSeconds / parts.Length;
        return parts.Select((text, i) => new LyricLine(text, i * step)).ToList();
    }
}
