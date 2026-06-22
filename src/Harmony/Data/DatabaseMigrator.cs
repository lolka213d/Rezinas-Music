using Microsoft.EntityFrameworkCore;

namespace Harmony.Data;

/// <summary>Lightweight schema patches when <c>EnsureCreated</c> cannot alter existing DBs.</summary>
public static class DatabaseMigrator
{
    public static void Apply(AppDbContext db)
    {
        db.Database.EnsureCreated();
        try { db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;"); } catch { /* ignore */ }
        try { db.Database.ExecuteSqlRaw("PRAGMA synchronous=NORMAL;"); } catch { /* ignore */ }

        TryAddColumn(db, "Albums", "IsUserCreated", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "Albums", "CreatedAt", "TEXT");
        TryAddColumn(db, "Albums", "ExternalSourceId", "TEXT");
        TryAddColumn(db, "Albums", "ExternalSource", "INTEGER");

        TryAddColumn(db, "UserSettings", "PlaybackSpeed", "REAL NOT NULL DEFAULT 1.0");
        TryAddColumn(db, "UserSettings", "RadioEnabled", "INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(db, "UserSettings", "CrossfadeMs", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "LyricsOffsetSeconds", "REAL NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "OpenNowPlayingOnPlay", "INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(db, "UserSettings", "MediaKeysEnabled", "INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(db, "UserSettings", "ShowHomeAlbums", "INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(db, "UserSettings", "MiniPlayerInTray", "INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(db, "UserSettings", "CompactTrackLists", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "SleepTimerMinutes", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "LastFmEnabled", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "LastFmApiKey", "TEXT");
        TryAddColumn(db, "UserSettings", "LastFmApiSecret", "TEXT");
        TryAddColumn(db, "UserSettings", "LastFmSessionKey", "TEXT");
        TryAddColumn(db, "UserSettings", "EqBand60", "REAL NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "EqBand250", "REAL NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "EqBand1k", "REAL NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "EqBand4k", "REAL NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "EqBand12k", "REAL NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "CheckForUpdates", "INTEGER NOT NULL DEFAULT 1");
        TryAddColumn(db, "UserSettings", "SkippedUpdateVersion", "TEXT");
        TryAddColumn(db, "UserSettings", "LastPlayedSource", "INTEGER");
        TryAddColumn(db, "UserSettings", "LastPlayedSourceId", "TEXT");
        TryAddColumn(db, "UserSettings", "LastPlayedPositionSeconds", "REAL NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "InstalledAt", "TEXT");
        TryAddColumn(db, "UserSettings", "StartWithWindows", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "DiscordPresenceEnabled", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "GaplessPlayback", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "MiniPlayerWindowEnabled", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(db, "UserSettings", "OfflineCacheLimitMb", "INTEGER NOT NULL DEFAULT 512");
        TryAddColumn(db, "UserSettings", "SyncFolderPath", "TEXT");
        TryAddColumn(db, "UserSettings", "LastSeenAppVersion", "TEXT");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS AlbumTracks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AlbumId INTEGER NOT NULL,
                TrackId INTEGER NOT NULL,
                Position INTEGER NOT NULL DEFAULT 0,
                AddedAt TEXT NOT NULL,
                FOREIGN KEY (AlbumId) REFERENCES Albums(Id) ON DELETE CASCADE,
                FOREIGN KEY (TrackId) REFERENCES Tracks(Id) ON DELETE CASCADE
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS SearchHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Query TEXT NOT NULL,
                SearchedAt TEXT NOT NULL
            );
            """);
    }

    private static void TryAddColumn(AppDbContext db, string table, string column, string definition)
    {
        if (!IsSafeIdentifier(table) || !IsSafeIdentifier(column) || string.IsNullOrWhiteSpace(definition))
            return;

        try
        {
            db.Database.ExecuteSqlRaw(
                "ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition + ";");
        }
        catch
        {
            // Column already exists.
        }
    }

    private static bool IsSafeIdentifier(string name) =>
        !string.IsNullOrEmpty(name) && name.All(static c => char.IsLetterOrDigit(c) || c == '_');
}
