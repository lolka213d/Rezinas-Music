using Harmony.Models;

namespace Harmony.ViewModels;

public sealed class SmartPlaylistCard
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public IReadOnlyList<Track> Tracks { get; init; } = Array.Empty<Track>();
}
