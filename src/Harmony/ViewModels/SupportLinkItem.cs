namespace Harmony.ViewModels;

public sealed class SupportLinkItem(string title, string url, string? subtitle = null)
{
    public string Title { get; } = title;
    public string Url { get; } = url;
    public string? Subtitle { get; } = subtitle;
}
