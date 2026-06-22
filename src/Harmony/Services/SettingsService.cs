using System.IO;
using Harmony.Config;
using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

/// <summary>SQLite-backed implementation of <see cref="ISettingsService"/>.</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SettingsService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
        Current = new UserSettings();
    }

    public UserSettings Current { get; private set; }

    public event EventHandler? SettingsChanged;

    public async Task<UserSettings> LoadAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (settings == null)
        {
            settings = new UserSettings
            {
                Id = 1,
                Language = InstallLanguage.ReadOrDefault(),
                InstalledAt = DateTime.UtcNow
            };
            db.UserSettings.Add(settings);
            await db.SaveChangesAsync();
        }
        else if (settings.InstalledAt == null)
        {
            settings.InstalledAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        Current = settings;
        BuiltInApiCredentials.Apply(Current);
        SpotifyCredentials.ApplyBuiltIn(Current);
        return settings;
    }

    public async Task SaveAsync(UserSettings settings)
    {
        await using var db = await _factory.CreateDbContextAsync();
        settings.Id = 1;
        var existing = await db.UserSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (existing == null)
            db.UserSettings.Add(settings);
        else
            db.Entry(existing).CurrentValues.SetValues(settings);

        await db.SaveChangesAsync();
        BuiltInApiCredentials.Apply(settings);
        SpotifyCredentials.ApplyBuiltIn(settings);
        Current = settings;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveLastPlaybackAsync(MusicSource? source, string? sourceId, double positionSeconds)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.UserSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (row == null) return;

        row.LastPlayedSource = source.HasValue ? (int)source.Value : null;
        row.LastPlayedSourceId = sourceId;
        row.LastPlayedPositionSeconds = Math.Max(0, positionSeconds);
        await db.SaveChangesAsync();

        Current.LastPlayedSource = row.LastPlayedSource;
        Current.LastPlayedSourceId = row.LastPlayedSourceId;
        Current.LastPlayedPositionSeconds = row.LastPlayedPositionSeconds;
    }

    public Task ClearCacheAsync()
    {
        if (Directory.Exists(AppPaths.CacheFolder))
        {
            foreach (var file in Directory.EnumerateFiles(AppPaths.CacheFolder))
            {
                try { File.Delete(file); } catch { /* ignore locked files */ }
            }
        }
        return Task.CompletedTask;
    }
}
