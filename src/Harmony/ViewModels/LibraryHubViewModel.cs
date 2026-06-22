using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Services.Localization;

namespace Harmony.ViewModels;

public enum LibrarySection
{
    Songs,
    Collections,
    Favorites,
    History
}

public sealed class LibraryTabItem
{
    public LibraryTabItem(LibrarySection section, string labelKey)
    {
        Section = section;
        LabelKey = labelKey;
    }

    public LibrarySection Section { get; }
    public string LabelKey { get; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>Library hub — songs, albums/playlists, favorites, history (Harmony-Music style).</summary>
public partial class LibraryHubViewModel : ObservableObject
{
    private readonly LibraryViewModel _library;
    private readonly CollectionsViewModel _collections;
    private readonly FavoritesViewModel _favorites;
    private readonly HistoryViewModel _history;
    private readonly ILocalizationService _loc;

    public LibraryHubViewModel(
        LibraryViewModel library,
        CollectionsViewModel collections,
        FavoritesViewModel favorites,
        HistoryViewModel history,
        ILocalizationService localization)
    {
        _library = library;
        _collections = collections;
        _favorites = favorites;
        _history = history;
        _loc = localization;

        LibraryShortcuts = new ObservableCollection<LibraryShortcutItem>
        {
            new("library.songs", "F1 M4 10v2h12v-2H4zm0-4v2h12V6H4zm0 8v2h8v-2H4zm14-4v8.17c-.31-.11-.65-.17-1-.17-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3V8h3V6h-5z", LibrarySection.Songs),
            new("nav.playlists", "F1 M15 6H3v2h12V6zm0 4H3v2h12v-2zM3 16h8v-2H3v2zM17 6v8.18c-.31-.11-.65-.18-1-.18-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3V8h3V6h-5z", LibrarySection.Collections),
            new("nav.favorites", "F1 M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z", LibrarySection.Favorites),
            new("nav.history", "F1 M13 3c-4.97 0-9 4.03-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42C8.27 19.99 10.51 21 13 21c4.97 0 9-4.03 9-9s-4.03-9-9-9zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z", LibrarySection.History),
        };

        _loc.LanguageChanged += (_, _) => RefreshLabels();
        RefreshLabels();
        SelectedSection = LibrarySection.Collections;
        ShowSectionContent = true;
        ActivateSection(SelectedSection);
    }

    public ObservableCollection<LibraryShortcutItem> LibraryShortcuts { get; }
    public string SectionTitle => SelectedSection switch
    {
        LibrarySection.Songs => _loc.T("library.songs"),
        LibrarySection.Collections => _loc.T("nav.playlists"),
        LibrarySection.Favorites => _loc.T("nav.favorites"),
        LibrarySection.History => _loc.T("nav.history"),
        _ => _loc.T("nav.library")
    };
    public string SectionSubtitle => SelectedSection switch
    {
        LibrarySection.Songs => _loc.T("library.subtitle"),
        LibrarySection.Collections => _loc.T("library.playlistsSubtitle"),
        LibrarySection.Favorites => _loc.T("favorites.subtitle"),
        LibrarySection.History => _loc.T("library.historySubtitle"),
        _ => string.Empty
    };
    public bool ShowCreateButton => SelectedSection == LibrarySection.Collections;

    [ObservableProperty] private bool _showSectionContent;
    public ILocalizationService Loc => _loc;

    [ObservableProperty] private LibrarySection _selectedSection = LibrarySection.Songs;
    [ObservableProperty] private ObservableObject? _currentSectionPage;

    public void OpenSection(AppPage page)
    {
        if (page is AppPage.Playlists)
            _collections.SelectedKind = CollectionKind.Playlists;
        else if (page is AppPage.Albums or AppPage.Collections)
            _collections.SelectedKind = CollectionKind.Albums;

        SelectedSection = page switch
        {
            AppPage.Collections or AppPage.Albums or AppPage.Playlists => LibrarySection.Collections,
            AppPage.Favorites => LibrarySection.Favorites,
            AppPage.History => LibrarySection.History,
            _ => LibrarySection.Songs
        };
        ShowSectionContent = true;
    }

    [RelayCommand]
    private void OpenShortcut(LibraryShortcutItem item)
    {
        SelectedSection = item.Section;
        ShowSectionContent = true;
    }

    [RelayCommand]
    private void SelectSection(LibrarySection section)
    {
        SelectedSection = section;
        ShowSectionContent = true;
    }

    [RelayCommand]
    private void BackToShortcuts() => ShowSectionContent = true;

    [RelayCommand]
    private void CreatePlaylist()
    {
        SelectedSection = LibrarySection.Collections;
        _collections.SelectedKind = CollectionKind.Playlists;
        ShowSectionContent = true;
        _ = _collections.LoadAsync();
    }

    partial void OnSelectedSectionChanged(LibrarySection value)
    {
        ActivateSection(value);
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionSubtitle));
        OnPropertyChanged(nameof(ShowCreateButton));
    }

    private void ActivateSection(LibrarySection section)
    {
        switch (section)
        {
            case LibrarySection.Songs:
                _ = _library.LoadAsync();
                CurrentSectionPage = _library;
                break;
            case LibrarySection.Collections:
                _collections.SelectedPlaylist = null;
                _collections.SelectedKind = CollectionKind.Playlists;
                _ = _collections.LoadAsync();
                CurrentSectionPage = _collections;
                break;
            case LibrarySection.Favorites:
                _ = _favorites.LoadAsync();
                CurrentSectionPage = _favorites;
                break;
            case LibrarySection.History:
                _ = _history.LoadAsync();
                CurrentSectionPage = _history;
                break;
        }
    }

    private void RefreshLabels()
    {
        foreach (var s in LibraryShortcuts)
            s.Title = _loc.T(s.TitleKey);
        OnPropertyChanged(nameof(LibraryShortcuts));
        OnPropertyChanged(nameof(SectionTitle));
        OnPropertyChanged(nameof(SectionSubtitle));
    }
}
