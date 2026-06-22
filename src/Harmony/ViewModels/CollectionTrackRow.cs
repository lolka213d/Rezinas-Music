using Harmony.Models;

namespace Harmony.ViewModels;

/// <summary>Track row in a playlist/album table (index, added date).</summary>
public sealed class CollectionTrackRow
{
    public required int Index { get; init; }
    public required Track Track { get; init; }
    public required string AddedAtDisplay { get; init; }
}
