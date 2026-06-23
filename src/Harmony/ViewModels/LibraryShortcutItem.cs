using Harmony.Models;

namespace Harmony.ViewModels;

public sealed class LibraryShortcutItem
{
    public LibraryShortcutItem(string titleKey, string iconData, LibrarySection section)
    {
        TitleKey = titleKey;
        IconData = iconData;
        Section = section;
    }

    public string TitleKey { get; }
    public string IconData { get; }
    public LibrarySection Section { get; }
    public string Title { get; set; } = string.Empty;
}
