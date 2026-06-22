using Harmony.Models;

namespace Harmony.Services.Interfaces;

public interface ILocalImportService
{
    /// <summary>Import audio files from disk into the library (copies to app music folder).</summary>
    Task<IReadOnlyList<Track>> ImportFilesAsync(IEnumerable<string> filePaths, int? targetAlbumId = null);
}
