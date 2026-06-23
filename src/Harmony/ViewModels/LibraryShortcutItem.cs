namespace Harmony.ViewModels;

public sealed class LibraryShortcutItem
{
    public LibraryShortcutItem(string titleKey, string subtitleKey, string iconData, LibrarySection section)
    {
        TitleKey = titleKey;
        SubtitleKey = subtitleKey;
        IconData = iconData;
        Section = section;
    }

    public string TitleKey { get; }
    public string SubtitleKey { get; }
    public string IconData { get; }
    public LibrarySection Section { get; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
}
