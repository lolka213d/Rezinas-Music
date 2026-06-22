using System.Collections.ObjectModel;
using System.Reflection;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using Harmony.Helpers;

using Harmony.Models;

using Harmony.Services;

using Harmony.Services.Interfaces;

using Harmony.Services.Localization;

using Microsoft.Win32;



namespace Harmony.ViewModels;



public enum SettingsTab { Profile, Look, Playback, Interface, Data }



/// <summary>Settings page: profile, theme/language, audio quality, maintenance.</summary>

public partial class SettingsViewModel : ObservableObject

{

    private readonly ISettingsService _settings;

    private readonly IHistoryService _history;

    private readonly IFavoritesService _favorites;

    private readonly PlayerViewModel _player;

    private readonly IAppLog _log;

    private readonly ILocalizationService _loc;

    private readonly MediaKeysService _mediaKeys;
    private readonly ListeningStatsService _stats;
    private readonly UpdateCheckService _updates;

    private bool _isLoading;

    private CancellationTokenSource? _saveDebounce;



    public SettingsViewModel(

        ISettingsService settings,

        IHistoryService history,

        IFavoritesService favorites,

        PlayerViewModel player,

        IAppLog log,

        ILocalizationService loc,

        MediaKeysService mediaKeys,
        ListeningStatsService stats,
        UpdateCheckService updates)

    {

        _settings = settings;

        _history = history;

        _favorites = favorites;

        _player = player;

        _log = log;

        _loc = loc;

        _mediaKeys = mediaKeys;
        _stats = stats;
        _updates = updates;

        LogFilePath = log.LogFilePath;

        AppVersion = UpdateCheckService.CurrentVersion;

        _loc.LanguageChanged += (_, _) => RefreshLocalizedLabels();

        SupportLinks = new ObservableCollection<SupportLinkItem>
        {
            new("PayPal", AuthorSupport.PayPal),
            new("Buy Me a Coffee", AuthorSupport.BuyMeACoffee),
            new("Boosty", AuthorSupport.Boosty),
            new("Patreon", AuthorSupport.Patreon),
            new("DonateAlerts", AuthorSupport.DonateAlerts),
        };
    }

    public ObservableCollection<SupportLinkItem> SupportLinks { get; }



    private void RefreshLocalizedLabels()

    {

        OnPropertyChanged(nameof(TabProfileLabel));

        OnPropertyChanged(nameof(TabLookLabel));

        OnPropertyChanged(nameof(TabPlaybackLabel));

        OnPropertyChanged(nameof(TabInterfaceLabel));

        OnPropertyChanged(nameof(TabDataLabel));
        OnPropertyChanged(nameof(SupportTitle));
        OnPropertyChanged(nameof(SupportHint));
    }



    public ILocalizationService Loc => _loc;



    [ObservableProperty] private string _userName = "Guest";

    [ObservableProperty] private string? _avatarPath;

    [ObservableProperty] private bool _isDarkTheme = true;

    [ObservableProperty] private string _language = "en";

    [ObservableProperty] private AudioQuality _audioQuality = AudioQuality.Normal;

    [ObservableProperty] private double _defaultVolume = 0.7;

    [ObservableProperty] private double _playbackSpeed = 1.0;

    [ObservableProperty] private bool _radioEnabled = true;

    [ObservableProperty] private bool _openNowPlayingOnPlay = true;

    [ObservableProperty] private bool _mediaKeysEnabled = true;

    [ObservableProperty] private bool _showHomeAlbums = true;

    [ObservableProperty] private int _crossfadeMs;

    [ObservableProperty] private int _sleepTimerMinutes;

    [ObservableProperty] private bool _miniPlayerInTray = true;

    [ObservableProperty] private bool _compactTrackLists;

    [ObservableProperty] private double _lyricsOffsetSeconds;

    [ObservableProperty] private bool _lastFmEnabled;
    [ObservableProperty] private string? _lastFmApiKey;
    [ObservableProperty] private string? _lastFmApiSecret;
    [ObservableProperty] private string? _lastFmSessionKey;
    [ObservableProperty] private double _eqBand60;
    [ObservableProperty] private double _eqBand250;
    [ObservableProperty] private double _eqBand1k;
    [ObservableProperty] private double _eqBand4k;
    [ObservableProperty] private double _eqBand12k;

    [ObservableProperty] private string _statsSummary = string.Empty;

    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private string _logFilePath = string.Empty;

    [ObservableProperty] private string _appVersion = "1.0.0";

    [ObservableProperty] private bool _checkForUpdates = true;
    [ObservableProperty] private string _updateStatusMessage = string.Empty;

    [ObservableProperty] private SettingsTab _selectedTab = SettingsTab.Profile;

    public string CheckUpdatesLabel => _loc.T("settings.checkUpdates");
    public string CheckUpdatesNowLabel => _loc.T("update.checkNow");



    public string TabProfileLabel => _loc.T("settings.tabProfile");

    public string TabLookLabel => _loc.T("settings.tabLook");

    public string TabPlaybackLabel => _loc.T("settings.tabPlayback");

    public string TabInterfaceLabel => _loc.T("settings.tabInterface");

    public string TabDataLabel => _loc.T("settings.tabData");

    public string SupportTitle => _loc.Language == "ru" ? "Поддержка автора" : "Support the author";
    public string SupportHint => _loc.Language == "ru"
        ? "Если вам нравится Rezinas Music, вы можете поддержать разработку:"
        : "If you enjoy Rezinas Music, you can support development:";



    public bool IsProfileTab => SelectedTab == SettingsTab.Profile;

    public bool IsLookTab => SelectedTab == SettingsTab.Look;

    public bool IsPlaybackTab => SelectedTab == SettingsTab.Playback;

    public bool IsInterfaceTab => SelectedTab == SettingsTab.Interface;

    public bool IsDataTab => SelectedTab == SettingsTab.Data;



    public string VolumePercent => $"{(int)(DefaultVolume * 100)}%";



    public AudioQuality[] QualityOptions { get; } = Enum.GetValues<AudioQuality>();

    public double[] SpeedOptions { get; } = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];

    public int[] SleepTimerOptions { get; } = [0, 15, 30, 60];

    public string SleepTimerOffLabel => _loc.T("settings.sleepOff");

    public string HotkeysLabel => _loc.T("settings.hotkeys");

    public string HotkeysHint => _loc.T("settings.hotkeysHint");

    public string VolumeNote => _loc.T("settings.volumeNote");

    public string StatsTitle => _loc.T("settings.statsTitle");

    public string SleepTimerLabel => _loc.T("settings.sleepTimer");

    public string MiniTrayLabel => _loc.T("settings.miniTray");

    public string EqualizerLabel => _loc.T("settings.equalizer");
    public string LastFmLabel => _loc.T("settings.lastFm");
    public string LastFmHint => _loc.T("settings.lastFmHint");

    public string PersonalisationLabel => _loc.T("settings.personalisation");
    public string ContentLabel => _loc.T("settings.content");
    public string MusicPlaybackLabel => _loc.T("settings.musicPlayback");
    public string DownloadLabel => _loc.T("settings.download");
    public string BackupLabel => _loc.T("settings.backup");
    public string MiscLabel => _loc.T("settings.misc");
    public string AppInfoLabel => _loc.T("settings.appInfo");
    public string CompactListsLabel => _loc.T("settings.compactLists");



    public LanguageOption[] LanguageOptions { get; } =

    [

        new("en", "English"),

        new("ru", "Русский"),

        new("uk", "Українська"),

        new("es", "Español"),

        new("de", "Deutsch"),

        new("fr", "Français"),

        new("it", "Italiano"),

        new("pt", "Português"),

        new("pl", "Polski"),

        new("ja", "日本語"),

    ];



    public void ShowTab(SettingsTab tab)
    {
        SelectedTab = tab;
        Load();
    }

    public void Load()

    {

        _isLoading = true;

        var s = _settings.Current;

        UserName = s.UserName;

        AvatarPath = s.AvatarPath;

        IsDarkTheme = s.Theme == AppTheme.Dark;

        Language = s.Language;

        AudioQuality = s.AudioQuality;

        DefaultVolume = s.Volume;

        PlaybackSpeed = s.PlaybackSpeed;

        RadioEnabled = s.RadioEnabled;

        OpenNowPlayingOnPlay = s.OpenNowPlayingOnPlay;

        MediaKeysEnabled = s.MediaKeysEnabled;

        ShowHomeAlbums = s.ShowHomeAlbums;

        CrossfadeMs = s.CrossfadeMs;
        SleepTimerMinutes = s.SleepTimerMinutes;
        MiniPlayerInTray = s.MiniPlayerInTray;
        CompactTrackLists = s.CompactTrackLists;
        LyricsOffsetSeconds = s.LyricsOffsetSeconds;
        LastFmEnabled = s.LastFmEnabled;
        LastFmApiKey = s.LastFmApiKey;
        LastFmApiSecret = s.LastFmApiSecret;
        LastFmSessionKey = s.LastFmSessionKey;
        EqBand60 = s.EqBand60;
        EqBand250 = s.EqBand250;
        EqBand1k = s.EqBand1k;
        EqBand4k = s.EqBand4k;
        EqBand12k = s.EqBand12k;
        CheckForUpdates = s.CheckForUpdates;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(VolumePercent));
        _isLoading = false;
        _ = LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            var stats = await _stats.GetWeeklyAsync();
            var hours = stats.TotalSeconds / 3600.0;
            var top = stats.TopArtists.FirstOrDefault()?.ArtistName ?? "—";
            StatsSummary = $"{stats.PlayCount} plays · {hours:0.#} h · top: {top}";
        }
        catch (Exception ex)
        {
            _log.Error("Failed to load listening stats", ex);
            StatsSummary = string.Empty;
        }
    }

    partial void OnDefaultVolumeChanged(double value)

    {

        OnPropertyChanged(nameof(VolumePercent));

        ScheduleAutoSave();

    }



    partial void OnUserNameChanged(string value) => ScheduleAutoSave();

    partial void OnAvatarPathChanged(string? value) => ScheduleAutoSave();

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (!_isLoading)
            ThemeService.Apply(value ? AppTheme.Dark : AppTheme.Light);
        ScheduleAutoSave();
    }

    partial void OnLanguageChanged(string value)
    {
        if (!_isLoading)
        {
            _loc.SetLanguage(value);
            RefreshLocalizedLabels();
        }
        ScheduleAutoSave();
    }

    partial void OnAudioQualityChanged(AudioQuality value) => ScheduleAutoSave();

    partial void OnPlaybackSpeedChanged(double value) => ScheduleAutoSave();

    partial void OnRadioEnabledChanged(bool value) => ScheduleAutoSave();

    partial void OnOpenNowPlayingOnPlayChanged(bool value) => ScheduleAutoSave();

    partial void OnMediaKeysEnabledChanged(bool value)
    {
        if (!_isLoading)
            _mediaKeys.IsEnabled = value;
        ScheduleAutoSave();
    }

    partial void OnShowHomeAlbumsChanged(bool value) => ScheduleAutoSave();

    partial void OnCrossfadeMsChanged(int value) => ScheduleAutoSave();

    partial void OnSleepTimerMinutesChanged(int value) => ScheduleAutoSave();

    partial void OnMiniPlayerInTrayChanged(bool value) => ScheduleAutoSave();

    partial void OnCompactTrackListsChanged(bool value) => ScheduleAutoSave();

    partial void OnCheckForUpdatesChanged(bool value) => ScheduleAutoSave();

    partial void OnLyricsOffsetSecondsChanged(double value) => ScheduleAutoSave();

    partial void OnLastFmEnabledChanged(bool value) => ScheduleAutoSave();
    partial void OnLastFmApiKeyChanged(string? value) => ScheduleAutoSave();
    partial void OnLastFmApiSecretChanged(string? value) => ScheduleAutoSave();
    partial void OnLastFmSessionKeyChanged(string? value) => ScheduleAutoSave();
    partial void OnEqBand60Changed(double value) => ScheduleAutoSave();
    partial void OnEqBand250Changed(double value) => ScheduleAutoSave();
    partial void OnEqBand1kChanged(double value) => ScheduleAutoSave();
    partial void OnEqBand4kChanged(double value) => ScheduleAutoSave();
    partial void OnEqBand12kChanged(double value) => ScheduleAutoSave();



    partial void OnSelectedTabChanged(SettingsTab value)

    {

        OnPropertyChanged(nameof(IsProfileTab));

        OnPropertyChanged(nameof(IsLookTab));

        OnPropertyChanged(nameof(IsPlaybackTab));

        OnPropertyChanged(nameof(IsInterfaceTab));

        OnPropertyChanged(nameof(IsDataTab));

    }



    [RelayCommand]

    private void SelectTab(SettingsTab tab) => SelectedTab = tab;



    private void ScheduleAutoSave()

    {

        if (_isLoading) return;

        _saveDebounce?.Cancel();

        _saveDebounce = new CancellationTokenSource();

        var token = _saveDebounce.Token;

        _ = DebouncedSaveAsync(token);

    }



    private async Task DebouncedSaveAsync(CancellationToken token)

    {

        try

        {

            await Task.Delay(450, token);

            await ApplyAndSaveAsync();

        }

        catch (OperationCanceledException) { }

    }



    private async Task ApplyAndSaveAsync()

    {

        var s = _settings.Current;

        s.UserName = string.IsNullOrWhiteSpace(UserName) ? "Guest" : UserName.Trim();

        s.AvatarPath = AvatarPath;

        s.Theme = IsDarkTheme ? AppTheme.Dark : AppTheme.Light;

        s.Language = Language;

        s.AudioQuality = AudioQuality;

        s.Volume = Math.Clamp(DefaultVolume, 0, 1);

        s.PlaybackSpeed = Math.Clamp(PlaybackSpeed, 0.5, 2.0);

        s.RadioEnabled = RadioEnabled;

        s.OpenNowPlayingOnPlay = OpenNowPlayingOnPlay;

        s.MediaKeysEnabled = MediaKeysEnabled;

        s.ShowHomeAlbums = ShowHomeAlbums;

        s.CrossfadeMs = Math.Max(0, CrossfadeMs);
        s.SleepTimerMinutes = SleepTimerMinutes;
        s.MiniPlayerInTray = MiniPlayerInTray;
        s.CompactTrackLists = CompactTrackLists;
        s.LyricsOffsetSeconds = LyricsOffsetSeconds;
        s.LastFmEnabled = LastFmEnabled;
        s.LastFmApiKey = LastFmApiKey;
        s.LastFmApiSecret = LastFmApiSecret;
        s.LastFmSessionKey = LastFmSessionKey;
        s.EqBand60 = Math.Clamp(EqBand60, -12, 12);
        s.EqBand250 = Math.Clamp(EqBand250, -12, 12);
        s.EqBand1k = Math.Clamp(EqBand1k, -12, 12);
        s.EqBand4k = Math.Clamp(EqBand4k, -12, 12);
        s.EqBand12k = Math.Clamp(EqBand12k, -12, 12);
        s.CheckForUpdates = CheckForUpdates;

        await _settings.SaveAsync(s);

        _player.Volume = s.Volume;
        ThemeService.Apply(s.Theme);
        _loc.SetLanguage(s.Language);
        _mediaKeys.IsEnabled = s.MediaKeysEnabled;
        UiPreferences.ApplyCompactLists(s.CompactTrackLists);

        StatusMessage = _loc.T("status.settingsSaved");

    }



    [RelayCommand]

    private void PickAvatar()

    {

        var dialog = new OpenFileDialog

        {

            Title = _loc.T("settings.pickAvatar"),

            Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All files|*.*"

        };

        if (dialog.ShowDialog() == true)

            AvatarPath = dialog.FileName;

    }



    [RelayCommand]

    private void ClearAvatar() => AvatarPath = null;



    [RelayCommand]
    private void OpenSupportLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log.Error("Could not open support link", ex);
        }
    }

    [RelayCommand]
    private void OpenLogsFolder()

    {

        try

        {

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo

            {

                FileName = _log.LogsFolder,

                UseShellExecute = true

            });

        }

        catch (Exception ex)

        {

            _log.Error("Could not open logs folder", ex);

            StatusMessage = _loc.T("status.logsOpenFail");

        }

    }



    [RelayCommand]

    private async Task ClearCache()

    {

        await _settings.ClearCacheAsync();

        StatusMessage = _loc.T("status.cacheCleared");

    }



    [RelayCommand]

    private async Task ClearHistory()

    {

        await _history.ClearAsync();

        StatusMessage = _loc.T("status.historyCleared");

    }



    [RelayCommand]

    private async Task ClearFavorites()

    {

        await _favorites.ClearAllAsync();

        StatusMessage = _loc.T("status.favoritesCleared");

    }



    [RelayCommand]

    private async Task ResetDefaults()

    {

        _isLoading = true;

        UserName = "Guest";

        AvatarPath = null;

        IsDarkTheme = true;

        Language = "en";

        AudioQuality = AudioQuality.Normal;

        DefaultVolume = 0.7;

        PlaybackSpeed = 1.0;

        RadioEnabled = true;

        OpenNowPlayingOnPlay = true;

        MediaKeysEnabled = true;

        ShowHomeAlbums = true;

        CrossfadeMs = 0;
        SleepTimerMinutes = 0;
        MiniPlayerInTray = true;
        CompactTrackLists = false;
        LyricsOffsetSeconds = 0;

        _isLoading = false;

        await ApplyAndSaveAsync();

        StatusMessage = _loc.T("status.settingsReset");

    }

    [RelayCommand]
    private async Task CheckForUpdatesNow()
    {
        UpdateStatusMessage = _loc.T("update.checking");
        var prompted = await _updates.CheckAndPromptAsync(force: true);
        UpdateStatusMessage = prompted ? string.Empty : _loc.T("update.none");
    }

}



public sealed record LanguageOption(string Code, string Name);

