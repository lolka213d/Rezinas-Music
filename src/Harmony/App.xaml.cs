using System.Net.Http;
using System.Windows;
using Harmony.Data;
using Harmony.Helpers;
using Harmony.Services;
using Harmony.Services.Localization;
using Harmony.Services.Interfaces;
using Harmony.ViewModels;
using Harmony.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Harmony;

/// <summary>
/// Application entry point. Builds the dependency-injection container, creates
/// the SQLite database on first launch, and shows the main window.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private MediaKeysService? _mediaKeys;
    private TrayIconService? _tray;
    private SpotifySyncScheduler? _spotifySync;
    private bool _lastUiErrorShown;
    private DateTime _lastUiErrorUtc;

    /// <summary>When true, the main window close button exits the app instead of hiding to tray.</summary>
    internal static bool ForceShutdown { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _services = services.BuildServiceProvider();

        var log = _services.GetRequiredService<IAppLog>();

        DispatcherUnhandledException += (_, args) =>
        {
            log.Error("Unhandled UI exception", args.Exception);
            if (!_lastUiErrorShown || (DateTime.UtcNow - _lastUiErrorUtc).TotalSeconds > 2)
            {
                _lastUiErrorShown = true;
                _lastUiErrorUtc = DateTime.UtcNow;
                MessageBox.Show(
                    args.Exception.Message,
                    AppBranding.Name,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            log.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        // Create the database schema if it does not exist yet (off UI thread).
        Task.Run(() =>
        {
            using var scope = _services.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = factory.CreateDbContext();
            DatabaseMigrator.Apply(db);
        }).GetAwaiter().GetResult();

        // Load persisted settings before any view model needs them.
        var settings = _services.GetRequiredService<ISettingsService>();
        settings.LoadAsync().GetAwaiter().GetResult();
        ThemeService.Apply(settings.Current.Theme);
        var loc = _services.GetRequiredService<ILocalizationService>();
        loc.SetLanguage(settings.Current.Language);
        LocalizationService.Instance = (LocalizationService)loc;
        void RefreshDynamicLabels() => Resources["NewPlaylistCardLabel"] = loc.T("common.newPlaylist");
        RefreshDynamicLabels();
        loc.LanguageChanged += (_, _) => RefreshDynamicLabels();

        var favoriteLookup = _services.GetRequiredService<FavoriteLookup>();
        Resources["FavoriteLookup"] = favoriteLookup;

        var window = _services.GetRequiredService<MainWindow>();
        window.DataContext = _services.GetRequiredService<MainViewModel>();
        _mediaKeys = _services.GetRequiredService<MediaKeysService>();
        _mediaKeys.IsEnabled = settings.Current.MediaKeysEnabled;
        UiPreferences.ApplyCompactLists(settings.Current.CompactTrackLists);
        settings.SettingsChanged += (_, _) =>
        {
            if (_tray != null)
                _tray.IsEnabled = settings.Current.MiniPlayerInTray;
            UiPreferences.ApplyCompactLists(settings.Current.CompactTrackLists);
        };
        window.Show();
        _ = favoriteLookup.RefreshAsync();
        ChangelogService.ShowIfUpdated(settings, window, loc);
        if (settings.Current.StartWithWindows)
            WindowsStartupService.SetEnabled(true);
        window.ContentRendered += (_, _) =>
        {
            _tray = _services.GetRequiredService<TrayIconService>();
            _tray.IsEnabled = settings.Current.MiniPlayerInTray;
            _spotifySync = _services.GetRequiredService<SpotifySyncScheduler>();
            _spotifySync.Start();
            _ = CheckForUpdatesOnStartupAsync();
        };
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        if (_services == null) return;
        try
        {
            await Task.Delay(2500);
            var updates = _services.GetRequiredService<UpdateCheckService>();
            await updates.CheckAndPromptAsync();
        }
        catch (Exception ex)
        {
            _services.GetRequiredService<IAppLog>().Info($"Startup update check: {ex.Message}");
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ----- Data -----
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite(AppPaths.ConnectionString));

        // ----- Infrastructure -----
        services.AddSingleton<IAppLog, FileAppLog>();
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(60) });

        // ----- Services -----
        services.AddSingleton<UiPerformanceService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<FavoriteLookup>();
        services.AddSingleton<SmartPlaylistService>();
        services.AddSingleton<LastFmScrobbler>();
        services.AddSingleton<ILibraryService, LibraryService>();
        services.AddSingleton<IFavoritesService, FavoritesService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<ISearchHistoryService, SearchHistoryService>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IAlbumService, AlbumService>();
        services.AddSingleton<ILocalImportService, LocalImportService>();
        services.AddSingleton<IAudioPlayerService, NAudioPlayerService>();
        services.AddSingleton<ILyricsService, LyricsService>();
        services.AddSingleton<SoundCloudStreamResolver>();
        services.AddSingleton<IStreamResolverService, YouTubeStreamResolver>();
        services.AddSingleton<ApiTestService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<DeezerHomeService>();
        services.AddSingleton<HomeFeedCache>();
        services.AddSingleton<DailyMixCache>();
        services.AddSingleton<RadioStationCache>();
        services.AddSingleton<LibraryBackupService>();
        services.AddSingleton<PersonalWaveService>();
        services.AddSingleton<DiscordPresenceService>();
        services.AddSingleton<OfflineCacheService>();
        services.AddSingleton<RadioPlaybackContext>();
        services.AddSingleton<MediaKeysService>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<UpdateCheckService>();
        services.AddSingleton<ListeningStatsService>();
        services.AddSingleton<SpotifyAuthService>();
        services.AddSingleton<SpotifyLibrarySyncService>();
        services.AddSingleton<SpotifySyncScheduler>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton(sp => (LocalizationService)sp.GetRequiredService<ILocalizationService>());

        // ----- Search providers (Sample excluded — only real sources) -----
        services.AddSingleton<SampleMusicProvider>();
        services.AddSingleton<IMusicSearchService, DeezerSearchService>();
        services.AddSingleton<IMusicSearchService, YouTubeSearchService>();
        services.AddSingleton<SpotifySearchService>();
        services.AddSingleton<IMusicSearchService, SpotifySearchService>(sp => sp.GetRequiredService<SpotifySearchService>());
        services.AddSingleton<IMusicSearchService, SoundCloudSearchService>();

        // ----- View models -----
        services.AddSingleton<PlayerViewModel>();
        services.AddSingleton<LyricsViewModel>();
        services.AddSingleton<NowPlayingViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<RadioViewModel>();
        services.AddSingleton<SearchViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<LibraryHubViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<FavoritesViewModel>();
        services.AddSingleton<PlaylistsViewModel>();
        services.AddSingleton<CollectionsViewModel>();
        services.AddSingleton<AlbumsViewModel>();
        services.AddSingleton<AlbumDetailViewModel>();
        services.AddSingleton<ArtistDetailViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<ProfileViewModel>();
        services.AddSingleton<MainViewModel>();

        // ----- Windows -----
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Persist the latest volume / settings and release the audio engine.
        if (_services != null)
        {
            var log = _services.GetRequiredService<IAppLog>();
            log.Info($"{AppBranding.Name} shutting down.");

            _mediaKeys?.Dispose();
            _tray?.Dispose();
            _spotifySync?.Dispose();

            var player = _services.GetRequiredService<PlayerViewModel>();
            player.PersistPlaybackState();

            var settings = _services.GetRequiredService<ISettingsService>();
            settings.SaveAsync(settings.Current).GetAwaiter().GetResult();

            (_services.GetRequiredService<IAudioPlayerService>() as IDisposable)?.Dispose();
            _services.GetRequiredService<DiscordPresenceService>().Dispose();
            _services.Dispose();
        }
        base.OnExit(e);
    }
}
