namespace Harmony.Data;

/// <summary>v1.2.11 — adds <see cref="Models.PlaybackSourceMode"/> column to existing databases.</summary>
public static class DatabaseMigration_1_2_11
{
    public static void Apply(AppDbContext db)
    {
        DatabaseMigrator.TryAddColumn(db, "UserSettings", "PlaybackSourceMode", "INTEGER NOT NULL DEFAULT 0");
    }
}
