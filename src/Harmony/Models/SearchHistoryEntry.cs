namespace Harmony.Models;

/// <summary>Recent search query persisted for quick reuse.</summary>
public class SearchHistoryEntry
{
    public int Id { get; set; }
    public string Query { get; set; } = string.Empty;
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
}
