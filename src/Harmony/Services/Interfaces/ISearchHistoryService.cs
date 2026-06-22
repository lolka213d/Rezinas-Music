namespace Harmony.Services.Interfaces;

/// <summary>Stores recent search queries.</summary>
public interface ISearchHistoryService
{
    Task RecordAsync(string query);
    Task<IReadOnlyList<string>> GetRecentAsync(int limit = 12);
    Task RemoveAsync(string query);
    Task ClearAsync();
}
