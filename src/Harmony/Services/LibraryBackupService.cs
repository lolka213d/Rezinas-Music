using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

/// <summary>Export/import playlists and full library backups.</summary>
public sealed class LibraryBackupService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPlaylistService _playlists;
    private readonly ISettingsService _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LibraryBackupService(
        IDbContextFactory<AppDbContext> factory,
        IPlaylistService playlists,
        ISettingsService settings)
    {
        _factory = factory;
        _playlists = playlists;
        _settings = settings;
    }

    public async Task ExportPlaylistsJsonAsync(string filePath, CancellationToken ct = default)
    {
        var payload = await BuildExportPayloadAsync(ct);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, ct);
    }

    public async Task<int> ImportPlaylistsJsonAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var payload = await JsonSerializer.DeserializeAsync<LibraryExportPayload>(stream, JsonOptions, ct);
        if (payload?.Playlists == null || payload.Playlists.Count == 0) return 0;

        var imported = 0;
        foreach (var playlist in payload.Playlists)
        {
            if (string.IsNullOrWhiteSpace(playlist.Name)) continue;
            var created = await _playlists.CreateAsync(playlist.Name);
            foreach (var track in playlist.Tracks ?? [])
            {
                if (string.IsNullOrWhiteSpace(track.Title)) continue;
                await _playlists.AddTrackAsync(created.Id, track);
            }
            imported++;
        }
        return imported;
    }

    public async Task CreateBackupZipAsync(string zipPath, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "rezinas-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var dbPath = AppPaths.DatabaseFile;
            if (File.Exists(dbPath))
                File.Copy(dbPath, Path.Combine(tempDir, "harmony.db"), overwrite: true);

            var payload = await BuildExportPayloadAsync(ct);
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "playlists.json"),
                JsonSerializer.Serialize(payload, JsonOptions),
                ct);

            var settingsCopy = CloneSettings(_settings.Current);
            await File.WriteAllTextAsync(
                Path.Combine(tempDir, "settings.json"),
                JsonSerializer.Serialize(settingsCopy, JsonOptions),
                ct);

            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(tempDir, zipPath);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    public async Task RestoreBackupZipAsync(string zipPath, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "rezinas-restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            var dbBackup = Path.Combine(tempDir, "harmony.db");
            if (File.Exists(dbBackup))
            {
                var target = AppPaths.DatabaseFile;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(dbBackup, target, overwrite: true);
            }

            var playlistsFile = Path.Combine(tempDir, "playlists.json");
            if (File.Exists(playlistsFile))
                await ImportPlaylistsJsonAsync(playlistsFile, ct);

            var settingsFile = Path.Combine(tempDir, "settings.json");
            if (File.Exists(settingsFile))
            {
                await using var stream = File.OpenRead(settingsFile);
                var restored = await JsonSerializer.DeserializeAsync<UserSettings>(stream, JsonOptions, ct);
                if (restored != null)
                {
                    restored.Id = 1;
                    await _settings.SaveAsync(restored);
                }
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
        }
    }

    public async Task ExportSyncSnapshotAsync(string folderPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(folderPath);
        var payload = await BuildExportPayloadAsync(ct);
        payload.Favorites = await LoadFavoriteTracksAsync(ct);
        var file = Path.Combine(folderPath, "rezinas-sync.json");
        await using var stream = File.Create(file);
        await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, ct);
    }

    private async Task<LibraryExportPayload> BuildExportPayloadAsync(CancellationToken ct)
    {
        var list = new List<ExportedPlaylist>();
        foreach (var playlist in await _playlists.GetPlaylistsAsync())
        {
            var tracks = await _playlists.GetTracksAsync(playlist.Id);
            list.Add(new ExportedPlaylist
            {
                Name = playlist.Name,
                Tracks = tracks.Select(CloneTrack).ToList()
            });
        }

        return new LibraryExportPayload
        {
            Version = 1,
            ExportedAtUtc = DateTime.UtcNow,
            Playlists = list
        };
    }

    private async Task<List<Track>> LoadFavoriteTracksAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Favorites.AsNoTracking()
            .Include(f => f.Track)
            .Where(f => f.Track != null)
            .Select(f => f.Track!)
            .ToListAsync(ct);
    }

    private static Track CloneTrack(Track t) => new()
    {
        Title = t.Title,
        ArtistName = t.ArtistName,
        AlbumName = t.AlbumName,
        DurationSeconds = t.DurationSeconds,
        Source = t.Source,
        SourceId = t.SourceId,
        ThumbnailUrl = t.ThumbnailUrl,
        StreamUrl = t.StreamUrl
    };

    private static UserSettings CloneSettings(UserSettings s) => new()
    {
        Id = 1,
        UserName = s.UserName,
        AvatarPath = s.AvatarPath,
        Theme = s.Theme,
        Language = s.Language,
        AudioQuality = s.AudioQuality,
        Volume = s.Volume,
        PlaybackSpeed = s.PlaybackSpeed,
        RadioEnabled = s.RadioEnabled,
        CrossfadeMs = s.CrossfadeMs,
        LyricsOffsetSeconds = s.LyricsOffsetSeconds,
        OpenNowPlayingOnPlay = s.OpenNowPlayingOnPlay,
        MediaKeysEnabled = s.MediaKeysEnabled,
        ShowHomeAlbums = s.ShowHomeAlbums,
        MiniPlayerInTray = s.MiniPlayerInTray,
        ReduceGpuUsage = s.ReduceGpuUsage,
        CompactTrackLists = s.CompactTrackLists,
        SleepTimerMinutes = s.SleepTimerMinutes,
        CheckForUpdates = s.CheckForUpdates,
        StartWithWindows = s.StartWithWindows,
        DiscordPresenceEnabled = s.DiscordPresenceEnabled,
        GaplessPlayback = s.GaplessPlayback,
        OfflineCacheLimitMb = s.OfflineCacheLimitMb,
        SyncFolderPath = s.SyncFolderPath
    };

    public sealed class LibraryExportPayload
    {
        public int Version { get; set; } = 1;
        public DateTime ExportedAtUtc { get; set; }
        public List<ExportedPlaylist> Playlists { get; set; } = [];
        public List<Track>? Favorites { get; set; }
    }

    public sealed class ExportedPlaylist
    {
        public string Name { get; set; } = string.Empty;
        public List<Track> Tracks { get; set; } = [];
    }
}
