using Harmony.Models;
using Harmony.Services;
using Harmony.ViewModels;

namespace Harmony.Helpers;

/// <summary>Shared lyric highlight timing for album and overlay views.</summary>
public static class LyricSyncHelper
{
    /// <summary>Prepare lyrics for playback (distribute plain text, compute optional time scale).</summary>
    public static (IReadOnlyList<LyricLine> Lines, bool IsSynced, double SyncScale) Prepare(
        LyricsData data,
        double metadataDurationSeconds,
        double playbackDurationSeconds)
    {
        var duration = playbackDurationSeconds > 0
            ? playbackDurationSeconds
            : metadataDurationSeconds;

        if (data.IsSynced)
            return (data.Lines, true, 1.0);

        var lines = data.Lines;
        if (lines.Count > 0 && lines.All(l => l.StartSeconds <= 0.001))
        {
            var plain = data.PlainText
                        ?? string.Join('\n', lines.Select(l => l.Text));
            lines = LrcParser.DistributePlain(plain, duration > 0 ? duration : 180);
        }

        var baseDuration = metadataDurationSeconds > 0 ? metadataDurationSeconds : duration;
        var scale = 1.0;
        if (!data.IsSynced && baseDuration > 0 && duration > 0 && Math.Abs(baseDuration - duration) > 1.5)
            scale = duration / baseDuration;

        return (lines, false, scale);
    }

    /// <summary>Recalculate scale when playback duration becomes known (unsynced lyrics only).</summary>
    public static double RecalculateScale(
        bool isSynced,
        double metadataDurationSeconds,
        double playbackDurationSeconds)
    {
        if (isSynced) return 1.0;

        var playback = playbackDurationSeconds > 0 ? playbackDurationSeconds : metadataDurationSeconds;
        var metadata = metadataDurationSeconds > 0 ? metadataDurationSeconds : playback;
        if (playback <= 0 || metadata <= 0) return 1.0;
        if (Math.Abs(playback - metadata) <= 1.5) return 1.0;
        return playback / metadata;
    }

    /// <summary>Index of the line active at <paramref name="positionSeconds"/>.</summary>
    public static int FindActiveIndex(
        IReadOnlyList<LyricLineViewModel> lines,
        double positionSeconds,
        double syncScale,
        double offsetSeconds)
    {
        if (lines.Count == 0) return -1;

        var adjusted = positionSeconds / Math.Max(0.01, syncScale) + offsetSeconds;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (adjusted >= lines[i].StartSeconds - 0.05)
                return i;
        }

        return -1;
    }
}
