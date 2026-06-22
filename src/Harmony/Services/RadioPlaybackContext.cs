using Harmony.Models;

namespace Harmony.Services;

/// <summary>Active radio station for endless playback extension.</summary>
public sealed class RadioPlaybackContext
{
    public RadioStation? ActiveStation { get; set; }

    public event Action<IReadOnlyList<Track>>? TracksAppended;

    public void NotifyTracksAppended(IReadOnlyList<Track> tracks)
    {
        if (tracks.Count > 0)
            TracksAppended?.Invoke(tracks);
    }
}
