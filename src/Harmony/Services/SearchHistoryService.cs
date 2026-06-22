using Harmony.Data;
using Harmony.Models;
using Harmony.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Services;

public sealed class SearchHistoryService : ISearchHistoryService
{
    private const int MaxStored = 40;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public SearchHistoryService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task RecordAsync(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length < 2) return;

        await using var db = await _factory.CreateDbContextAsync();

        var existing = await db.SearchHistory
            .FirstOrDefaultAsync(h => h.Query == trimmed);
        if (existing != null)
        {
            existing.SearchedAt = DateTime.UtcNow;
        }
        else
        {
            db.SearchHistory.Add(new SearchHistoryEntry { Query = trimmed, SearchedAt = DateTime.UtcNow });
        }

        await db.SaveChangesAsync();

        var overflow = await db.SearchHistory
            .OrderByDescending(h => h.SearchedAt)
            .Skip(MaxStored)
            .ToListAsync();
        if (overflow.Count > 0)
        {
            db.SearchHistory.RemoveRange(overflow);
            await db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<string>> GetRecentAsync(int limit = 12)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.SearchHistory.AsNoTracking()
            .OrderByDescending(h => h.SearchedAt)
            .Select(h => h.Query)
            .Take(Math.Max(1, limit))
            .ToListAsync();
    }

    public async Task RemoveAsync(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return;

        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.SearchHistory.Where(h => h.Query == trimmed).ToListAsync();
        if (rows.Count == 0) return;
        db.SearchHistory.RemoveRange(rows);
        await db.SaveChangesAsync();
    }

    public async Task ClearAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.SearchHistory.RemoveRange(db.SearchHistory);
        await db.SaveChangesAsync();
    }
}
