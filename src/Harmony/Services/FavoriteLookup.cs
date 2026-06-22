using System.ComponentModel;
using Harmony.Data;
using Harmony.Models;
using Microsoft.EntityFrameworkCore;
using Harmony.Services.Interfaces;

namespace Harmony.Services;

/// <summary>In-memory favorite keys with UI revision counter for heart icons.</summary>
public sealed class FavoriteLookup : INotifyPropertyChanged
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

    public static FavoriteLookup Instance { get; private set; } = null!;

    public FavoriteLookup(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
        Instance = this;
    }

    public int Revision { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static string Key(Track track) => $"{(int)track.Source}:{track.SourceId}";

    public static string Key(MusicSource source, string sourceId) => $"{(int)source}:{sourceId}";

    public bool IsFavorite(Track? track) =>
        track != null && _keys.Contains(Key(track));

    public async Task RefreshAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        var keys = await db.Favorites
            .Include(f => f.Track)
            .AsNoTracking()
            .Select(f => f.Track!)
            .ToListAsync();
        _keys.Clear();
        foreach (var t in keys)
            _keys.Add(Key(t));
        Bump();
    }

    public void ApplyToggle(Track track, bool isFavorite)
    {
        var key = Key(track);
        if (isFavorite) _keys.Add(key);
        else _keys.Remove(key);
        Bump();
    }

    private void Bump()
    {
        Revision++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Revision)));
    }
}
