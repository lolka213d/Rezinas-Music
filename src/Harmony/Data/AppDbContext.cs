using Harmony.Models;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Data;

/// <summary>
/// EF Core context backed by SQLite. The schema is created automatically on
/// first run via <c>EnsureCreated()</c>. For a production app you would switch
/// to migrations; <c>EnsureCreated</c> keeps the MVP simple.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<AlbumTrack> AlbumTracks => Set<AlbumTrack>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistTrack> PlaylistTracks => Set<PlaylistTrack>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<ListeningHistoryEntry> ListeningHistory => Set<ListeningHistoryEntry>();
    public DbSet<SearchHistoryEntry> SearchHistory => Set<SearchHistoryEntry>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ----- Track -----
        modelBuilder.Entity<Track>(e =>
        {
            e.ToTable("Tracks");
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).IsRequired();
            // A given provider track should not be stored twice.
            e.HasIndex(t => new { t.Source, t.SourceId });
            e.HasOne(t => t.Artist)
                .WithMany(a => a.Tracks)
                .HasForeignKey(t => t.ArtistId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.Album)
                .WithMany(a => a.Tracks)
                .HasForeignKey(t => t.AlbumId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ----- Artist -----
        modelBuilder.Entity<Artist>(e =>
        {
            e.ToTable("Artists");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Name);
        });

        // ----- Album -----
        modelBuilder.Entity<Album>(e =>
        {
            e.ToTable("Albums");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Name);
            e.HasIndex(a => a.IsUserCreated);
        });

        modelBuilder.Entity<AlbumTrack>(e =>
        {
            e.ToTable("AlbumTracks");
            e.HasKey(at => at.Id);
            e.HasOne(at => at.Album)
                .WithMany(a => a.Items)
                .HasForeignKey(at => at.AlbumId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(at => at.Track)
                .WithMany()
                .HasForeignKey(at => at.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ----- Playlist -----
        modelBuilder.Entity<Playlist>(e =>
        {
            e.ToTable("Playlists");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired();
        });

        // ----- PlaylistTrack (join) -----
        modelBuilder.Entity<PlaylistTrack>(e =>
        {
            e.ToTable("PlaylistTracks");
            e.HasKey(pt => pt.Id);
            e.HasOne(pt => pt.Playlist)
                .WithMany(p => p.Items)
                .HasForeignKey(pt => pt.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pt => pt.Track)
                .WithMany()
                .HasForeignKey(pt => pt.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ----- Favorite -----
        modelBuilder.Entity<Favorite>(e =>
        {
            e.ToTable("Favorites");
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.TrackId).IsUnique();
            e.HasOne(f => f.Track)
                .WithMany()
                .HasForeignKey(f => f.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ----- ListeningHistory -----
        modelBuilder.Entity<ListeningHistoryEntry>(e =>
        {
            e.ToTable("ListeningHistory");
            e.HasKey(h => h.Id);
            e.HasIndex(h => h.PlayedAt);
            e.HasOne(h => h.Track)
                .WithMany()
                .HasForeignKey(h => h.TrackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SearchHistoryEntry>(e =>
        {
            e.ToTable("SearchHistory");
            e.HasKey(h => h.Id);
            e.HasIndex(h => h.SearchedAt);
            e.HasIndex(h => h.Query);
        });

        // ----- UserSettings (single row) -----
        modelBuilder.Entity<UserSettings>(e =>
        {
            e.ToTable("UserSettings");
            e.HasKey(s => s.Id);
        });

        base.OnModelCreating(modelBuilder);
    }
}
