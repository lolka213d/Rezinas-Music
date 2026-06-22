using System.IO;
using System.Security.Cryptography;
using System.Text;
using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

public sealed class LocalImportService : ILocalImportService
{
    public static bool IsSupportedExtension(string ext) =>
        AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);

    private static readonly string[] AllowedExtensions =
        [".mp3", ".m4a", ".aac", ".wav", ".flac", ".ogg", ".wma"];

    private readonly ILibraryService _library;
    private readonly IAlbumService _albums;
    private readonly IAppLog _log;

    public LocalImportService(ILibraryService library, IAlbumService albums, IAppLog log)
    {
        _library = library;
        _albums = albums;
        _log = log;
    }

    public async Task<IReadOnlyList<Track>> ImportFilesAsync(
        IEnumerable<string> filePaths, int? targetAlbumId = null)
    {
        var imported = new List<Track>();

        foreach (var sourcePath in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!File.Exists(sourcePath)) continue;

                var ext = Path.GetExtension(sourcePath);
                if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                {
                    _log.Warning($"Skipped unsupported file: {sourcePath}");
                    continue;
                }

                var track = await ImportSingleAsync(sourcePath);
                await _library.AddToLibraryAsync(track);

                if (targetAlbumId is int albumId)
                    await _albums.AddTrackAsync(albumId, track);

                imported.Add(track);
                _log.Info($"Imported local track: {track.ArtistName} — {track.Title}");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to import {sourcePath}", ex);
            }
        }

        return imported;
    }

    private static async Task<Track> ImportSingleAsync(string sourcePath)
    {
        var fileName = Path.GetFileName(sourcePath);
        var destPath = Path.Combine(AppPaths.LocalMusicFolder, fileName);

        if (!PathsEqual(sourcePath, destPath))
        {
            var uniqueDest = destPath;
            var n = 1;
            while (File.Exists(uniqueDest) && !PathsEqual(uniqueDest, sourcePath))
            {
                uniqueDest = Path.Combine(
                    AppPaths.LocalMusicFolder,
                    $"{Path.GetFileNameWithoutExtension(fileName)}_{n}{Path.GetExtension(fileName)}");
                n++;
            }

            if (!File.Exists(uniqueDest))
                await Task.Run(() => File.Copy(sourcePath, uniqueDest, overwrite: false));

            destPath = uniqueDest;
        }

        string title = Path.GetFileNameWithoutExtension(fileName);
        string artist = "Unknown artist";
        string? album = null;
        int duration = 0;
        string? picturePath = null;

        try
        {
            using var tagFile = TagLib.File.Create(destPath);
            title = string.IsNullOrWhiteSpace(tagFile.Tag.Title)
                ? title
                : tagFile.Tag.Title;
            artist = tagFile.Tag.FirstPerformer ?? tagFile.Tag.FirstAlbumArtist ?? artist;
            album = string.IsNullOrWhiteSpace(tagFile.Tag.Album) ? null : tagFile.Tag.Album;
            duration = (int)tagFile.Properties.Duration.TotalSeconds;

            if (tagFile.Tag.Pictures.Length > 0)
            {
                var pic = tagFile.Tag.Pictures[0];
                picturePath = Path.Combine(AppPaths.CacheFolder, $"cover_{ComputeId(destPath)}.jpg");
                await File.WriteAllBytesAsync(picturePath, pic.Data.Data);
            }
        }
        catch
        {
            // Playable even without tags.
        }

        if (string.Equals(artist, "Unknown artist", StringComparison.OrdinalIgnoreCase))
            TryParseArtistFromFileName(fileName, ref title, ref artist);

        return new Track
        {
            Title = title,
            ArtistName = artist,
            AlbumName = album,
            DurationSeconds = duration,
            Source = MusicSource.Local,
            SourceId = ComputeId(destPath),
            StreamUrl = destPath,
            ThumbnailUrl = picturePath
        };
    }

    private static string ComputeId(string path) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant())))[..16];

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static void TryParseArtistFromFileName(string fileName, ref string title, ref string artist)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var sep = name.IndexOf(" - ", StringComparison.Ordinal);
        if (sep < 0)
            sep = name.IndexOf(" – ", StringComparison.Ordinal);
        if (sep <= 0) return;

        var parsedArtist = name[..sep].Trim();
        var parsedTitle = name[(sep + 3)..].Trim();
        if (string.IsNullOrWhiteSpace(parsedArtist) || string.IsNullOrWhiteSpace(parsedTitle)) return;

        artist = parsedArtist;
        title = parsedTitle;
    }
}
