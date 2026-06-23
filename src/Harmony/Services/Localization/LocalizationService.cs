using System.ComponentModel;
using Harmony.ViewModels;

namespace Harmony.Services.Localization;

public interface ILocalizationService : INotifyPropertyChanged
{
    string Language { get; }
    event EventHandler? LanguageChanged;
    void SetLanguage(string code);
    string T(string key);
    string QualityLabel(string qualityName);
    string SearchTabLabel(SearchTab tab);
    string NavLabel(AppPage page);
}

/// <summary>Runtime UI localization for all supported languages.</summary>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "ru", "uk", "es", "de", "fr", "it", "pt", "pl", "ja"
    };

    private string _language = "en";

    public static LocalizationService Instance { get; internal set; } = new();

    public string Language => _language;

    public event EventHandler? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetLanguage(string code)
    {
        var next = string.IsNullOrWhiteSpace(code) ? "en" : code.Trim();
        if (!Supported.Contains(next)) next = "en";
        if (_language == next) return;
        _language = next;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public string T(string key) => LocalizationTable.Get(_language, key);

    public string QualityLabel(string qualityName) => LocalizationTable.QualityLabel(_language, qualityName);

    public string SearchTabLabel(SearchTab tab) => LocalizationTable.SearchTabLabel(_language, tab);

    public string NavLabel(AppPage page) => page switch
    {
        AppPage.Home => NavHome,
        AppPage.Radio => NavRadio,
        AppPage.Search => NavSearch,
        AppPage.Library => NavLibrary,
        AppPage.Albums => NavAlbums,
        AppPage.Collections => NavCollections,
        AppPage.History => NavHistory,
        AppPage.Favorites => NavFavorites,
        AppPage.Playlists => NavPlaylists,
        AppPage.Settings => NavSettings,
        _ => page.ToString()
    };

    // Navigation
    public string NavHome => T("nav.home");
    public string NavRadio => T("nav.radio");
    public string NavSearch => T("nav.search");
    public string NavLibrary => T("nav.library");
    public string NavAlbums => T("nav.albums");
    public string NavCollections => T("nav.collections");
    public string NavHistory => T("nav.history");
    public string NavFavorites => T("nav.favorites");
    public string NavPlaylists => T("nav.playlists");
    public string NavSettings => T("nav.settings");

    // Common
    public string CommonPlay => T("common.play");
    public string CommonCreate => T("common.create");
    public string CommonDelete => T("common.delete");
    public string CommonRename => T("common.rename");
    public string CommonImport => T("common.import");
    public string CommonOpen => T("common.open");
    public string CommonSave => T("common.save");
    public string CommonSearch => T("common.search");
    public string CommonSongs => T("common.songs");
    public string CommonTracks => T("common.tracks");
    public string CommonArtists => T("common.artists");
    public string CommonAlbums => T("common.albums");
    public string CommonBack => T("common.back");
    public string CommonAddCurrent => T("common.addCurrent");
    public string CommonFilterArtist => T("common.filterArtist");
    public string CommonLocalProfile => T("common.localProfile");
    public string CommonNowPlaying => T("common.nowPlaying");
    public string CommonAllArtists => T("common.allArtists");
    public string CommonAppName => T("common.appName");
    public string CommonProfile => T("common.profile");
    public string CommonLoading => T("common.loading");
    public string CommonTitle => T("common.title");
    public string CommonTime => T("common.time");
    public string CommonCancel => T("common.cancel");
    public string CommonClose => T("common.close");

    // Search
    public string SearchTitle => T("search.title");
    public string SearchHint => T("search.hint");
    public string SearchPlaceholder => T("search.placeholder");
    public string SearchTabAll => T("search.tab.all");
    public string SearchTabTracks => T("search.tab.tracks");
    public string SearchTabAlbums => T("search.tab.albums");
    public string SearchTabArtists => T("search.tab.artists");

    // Library
    public string LibraryTitle => T("library.title");
    public string LibrarySubtitle => T("library.subtitle");
    public string LibraryEmpty => T("library.empty");
    public string LibraryImport => T("common.import");
    public string LibrarySearchPlaceholder => T("library.searchPlaceholder");
    public string LibraryEmptyImportHint => T("library.emptyImportHint");

    // Albums
    public string AlbumsTitle => T("albums.title");
    public string AlbumsSubtitle => T("albums.subtitle");
    public string AlbumsCreateHint => T("albums.createHint");
    public string AlbumsNewPlaceholder => T("albums.newPlaceholder");
    public string AlbumsSongs => T("albums.songs");
    public string AlbumLyricsTitle => T("album.lyricsTitle");
    public string AlbumTracksHeader => T("album.tracksHeader");
    public string CollectionsShuffle => T("collections.shuffle");
    public string HistoryTitle => T("history.title");
    public string HistorySubtitle => T("history.subtitle");
    public string HistoryClear => T("history.clear");
    public string HistorySearchPlaceholder => T("history.searchPlaceholder");
    public string HistoryEmptyTitle => T("history.emptyTitle");
    public string HistoryEmptyDesc => T("history.emptyDesc");
    public string ProfileInstalled => T("profile.installed");
    public string ProfileMyPlaylists => T("profile.myPlaylists");
    public string ProfileMyAlbums => T("profile.myAlbums");
    public string ProfileNoPlaylists => T("profile.noPlaylists");
    public string ProfileNoPlaylistsHint => T("profile.noPlaylistsHint");
    public string ProfileNoAlbums => T("profile.noAlbums");
    public string ProfileNoAlbumsHint => T("profile.noAlbumsHint");
    public string PlayerSelectTrack => T("player.selectTrack");
    public string ArtistPopular => T("artist.popular");
    public string ArtistPageLabel => T("artist.pageLabel");
    public string CommonAlbumsLabel => T("collections.albumColumn");
    public string FavoritesShuffle => T("collections.shuffle");

    // Playlists
    public string PlaylistsTitle => T("playlists.title");
    public string PlaylistsSubtitle => T("playlists.subtitle");
    public string PlaylistsNewPlaceholder => T("playlists.newPlaceholder");
    public string PlaylistsAddCurrentTrack => T("playlists.addCurrentTrack");

    // Settings
    public string SettingsTitle => T("settings.title");
    public string SettingsProfile => T("settings.profile");
    public string SettingsUsername => T("settings.username");
    public string SettingsAvatar => T("settings.avatar");
    public string SettingsPickAvatar => T("settings.pickAvatar");
    public string PickCover => T("common.pickCover");
    public string SettingsAppearance => T("settings.appearance");
    public string SettingsDarkTheme => T("settings.darkTheme");
    public string SettingsDarkThemeHint => T("settings.darkThemeHint");
    public string SettingsLanguage => T("settings.language");
    public string SettingsPlayback => T("settings.playback");
    public string SettingsQuality => T("settings.quality");
    public string SettingsRadio => T("settings.radio");
    public string SettingsLyricsOffset => T("settings.lyricsOffset");
    public string SettingsCrossfade => T("settings.crossfade");
    public string SettingsApiKeys => T("settings.apiKeys");
    public string SettingsApiHint => T("settings.apiHint");
    public string SettingsSpotifyNote => T("settings.spotifyNote");
    public string SettingsYoutube => T("settings.youtube");
    public string SettingsSpotify => T("settings.spotify");
    public string SettingsSoundcloud => T("settings.soundcloud");
    public string SettingsSoundcloudNote => T("settings.soundcloudNote");
    public string SettingsMaintenance => T("settings.maintenance");
    public string SettingsLogs => T("settings.logs");
    public string SettingsOpenLogs => T("settings.openLogs");
    public string SettingsClearCache => T("settings.clearCache");
    public string SettingsClearHistory => T("settings.clearHistory");
    public string SettingsInterface => T("settings.interface");
    public string SettingsOpenNowPlaying => T("settings.openNowPlaying");
    public string SettingsOpenNowPlayingHint => T("settings.openNowPlayingHint");
    public string SettingsSeconds => T("settings.seconds");
    public string SettingsMilliseconds => T("settings.milliseconds");
    public string SettingsMediaKeys => T("settings.mediaKeys");
    public string SettingsMediaKeysHint => T("settings.mediaKeysHint");
    public string SettingsShowHomeAlbums => T("settings.showHomeAlbums");
    public string SettingsShowHomeAlbumsHint => T("settings.showHomeAlbumsHint");
    public string SettingsResetDefaults => T("settings.resetDefaults");
    public string SettingsDefaultVolume => T("settings.defaultVolume");
    public string SettingsPlaybackSpeed => T("settings.playbackSpeed");
    public string SettingsClearFavorites => T("settings.clearFavorites");
    public string SettingsData => T("settings.data");
    public string SettingsAbout => T("settings.about");
    public string SettingsVersion => T("settings.version");
    public string SettingsTestYoutube => T("settings.testYoutube");
    public string SettingsTestSpotify => T("settings.testSpotify");
    public string SettingsTestSoundcloud => T("settings.testSoundcloud");
    public string SettingsTestDeezer => T("settings.testDeezer");
    public string StatusSettingsSaved => T("status.settingsSaved");
    public string StatusSettingsReset => T("status.settingsReset");
    public string StatusCacheCleared => T("status.cacheCleared");
    public string StatusHistoryCleared => T("status.historyCleared");
    public string StatusFavoritesCleared => T("status.favoritesCleared");
    public string StatusLogsOpenFail => T("status.logsOpenFail");

    // Tooltips
    public string TipPlay => T("tip.play");
    public string TipLike => T("tip.like");
    public string TipAddLibrary => T("tip.addLibrary");
    public string TipAddPlaylist => T("tip.addPlaylist");
    public string TipRemoveLibrary => T("tip.removeLibrary");
    public string TipRemovePlaylist => T("tip.removePlaylist");
    public string QualityLow => T("quality.low");
    public string QualityNormal => T("quality.normal");
    public string QualityHigh => T("quality.high");
}
