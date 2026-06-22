using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services.Interfaces;

/// <summary>Fetches lyrics (synced LRC when available, plain text fallback).</summary>
public interface ILyricsService
{
    Task<LyricsData?> GetLyricsAsync(string artist, string title, double trackDurationSeconds = 0,
        CancellationToken cancellationToken = default);
}
