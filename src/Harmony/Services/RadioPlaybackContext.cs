using Harmony.Models;

namespace Harmony.Services;

/// <summary>Active radio station for endless playback extension.</summary>
public sealed class RadioPlaybackContext
{
    public RadioStation? ActiveStation { get; set; }
    public int ExtendBatch { get; private set; }

    public event Action<IReadOnlyList<Track>>? TracksAppended;

    public void ResetDailyExtension() => ExtendBatch = 0;

    public int NextExtendBatch() => ++ExtendBatch;

    public void NotifyTracksAppended(IReadOnlyList<Track> tracks)
    {
        if (tracks.Count > 0)
            TracksAppended?.Invoke(tracks);
    }
}
