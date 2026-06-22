using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Harmony.Models;
using Harmony.Services;
using Harmony.Services.Interfaces;
using Harmony.Services.Localization;

namespace Harmony.ViewModels;

public sealed partial class RadioStationItem : ObservableObject
{
    private readonly ILocalizationService _loc;

    public RadioStationItem(RadioStation station, ILocalizationService loc)
    {
        Station = station;
        _loc = loc;
    }

    public RadioStation Station { get; }

    public string Id => Station.Id;
    public string Title => _loc.T(Station.TitleKey);
    public string Subtitle => _loc.T(Station.SubtitleKey);
    public string AccentColor => Station.AccentColor;
    public string AccentColor2 => Station.AccentColor2;

    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
    }
}

/// <summary>Language / genre radio — chart playlist with endless playback.</summary>
public partial class RadioViewModel : ObservableObject
{
    private readonly DeezerHomeService _deezer;
    private readonly PlayerViewModel _player;
    private readonly RadioPlaybackContext _radioContext;
    private readonly ISettingsService _settings;
    private readonly ILocalizationService _loc;
    private List<Track> _stationTracks = new();

    public RadioViewModel(
        DeezerHomeService deezer,
        PlayerViewModel player,
        RadioPlaybackContext radioContext,
        ISettingsService settings,
        ILocalizationService localization)
    {
        _deezer = deezer;
        _player = player;
        _radioContext = radioContext;
        _settings = settings;
        _loc = localization;
        _loc.LanguageChanged += (_, _) => RefreshLabels();
        _radioContext.TracksAppended += OnTracksAppended;
        _player.PropertyChanged += OnPlayerPropertyChanged;

        foreach (var station in RadioStationCatalog.All)
            Stations.Add(new RadioStationItem(station, _loc));
    }

    public PlayerViewModel Player => _player;
    public ILocalizationService Loc => _loc;

    public ObservableCollection<RadioStationItem> Stations { get; } = new();
    public ObservableCollection<Track> Tracks { get; } = new();

    public string TitleLabel => _loc.T("nav.radio");
    public string SubtitleLabel => _loc.T("radio.subtitle");
    public string PlayLabel => _loc.T("radio.playStation");
    public string LoadingLabel => _loc.T("radio.loading");
    public string EmptyLabel => _loc.T("radio.empty");
    public string ColumnTitleLabel => _loc.T("common.title");
    public string ColumnTimeLabel => _loc.T("common.time");
    public string QueueTitleLabel => _loc.T("radio.queueTitle");

    [ObservableProperty] private RadioStationItem? _selectedStation;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool HasTracks => Tracks.Count > 0;
    public bool IsEmpty => !IsLoading && Tracks.Count == 0;
    public bool CanPlay => Tracks.Count > 0 && !IsLoading;

    public string? SelectedStationTitle => SelectedStation?.Title;

    public Task InitializeAsync()
    {
        if (SelectedStation != null) return Task.CompletedTask;
        var defaultStation = RadioStationCatalog.DefaultForLanguage(_settings.Current.Language);
        SelectedStation = Stations.FirstOrDefault(s => s.Id == defaultStation.Id) ?? Stations.FirstOrDefault();
        return Task.CompletedTask;
    }

    partial void OnSelectedStationChanged(RadioStationItem? value)
    {
        OnPropertyChanged(nameof(SelectedStationTitle));
        OnPropertyChanged(nameof(CanPlay));
        if (value != null)
            _ = LoadStationAsync(value);
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task PlayStation()
    {
        if (SelectedStation == null || _stationTracks.Count == 0) return;
        await StartPlaybackAsync(_stationTracks[0]);
    }

    [RelayCommand]
    private async Task PlayTrack(Track? track)
    {
        if (track == null || SelectedStation == null || _stationTracks.Count == 0) return;
        await StartPlaybackAsync(track);
    }

    [RelayCommand]
    private async Task SelectAndPlay(RadioStationItem item)
    {
        SelectedStation = item;
        while (IsLoading)
            await Task.Delay(30);
        if (_stationTracks.Count > 0)
            await StartPlaybackAsync(_stationTracks[0]);
    }

    private async Task StartPlaybackAsync(Track start)
    {
        if (SelectedStation == null || _stationTracks.Count == 0) return;

        _player.IsShuffle = false;
        _player.RepeatMode = RepeatMode.Off;
        await _player.PlayQueueAsync(_stationTracks, start, SelectedStation.Station);
    }

    private async Task LoadStationAsync(RadioStationItem item)
    {
        IsLoading = true;
        StatusMessage = LoadingLabel;
        Tracks.Clear();
        _stationTracks.Clear();
        OnPropertyChanged(nameof(HasTracks));
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            var tracks = await _deezer.GetRadioStationTracksAsync(item.Station, 40);
            _stationTracks = tracks.ToList();
            foreach (var track in _stationTracks)
                Tracks.Add(track);

            StatusMessage = _stationTracks.Count > 0
                ? string.Format(_loc.T("radio.tracksReady"), _stationTracks.Count)
                : EmptyLabel;
            UpdateNowPlayingStatus();
        }
        catch
        {
            StatusMessage = EmptyLabel;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(CanPlay));
        }
    }

    private void OnTracksAppended(IReadOnlyList<Track> tracks)
    {
        foreach (var track in tracks)
        {
            if (_stationTracks.Any(t => t.Matches(track))) continue;
            _stationTracks.Add(track);
            Tracks.Add(track);
        }

        OnPropertyChanged(nameof(HasTracks));
        OnPropertyChanged(nameof(IsEmpty));
        UpdateNowPlayingStatus();
    }

    private void OnPlayerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PlayerViewModel.CurrentTrack) or nameof(PlayerViewModel.IsPlaying))
            UpdateNowPlayingStatus();
    }

    private void UpdateNowPlayingStatus()
    {
        var track = _player.CurrentTrack;
        if (track != null && _radioContext.ActiveStation?.Id == SelectedStation?.Id && _player.IsPlaying)
        {
            StatusMessage = string.Format(_loc.T("radio.nowPlaying"), track.ArtistName, track.Title);
            return;
        }

        if (Tracks.Count > 0 && !IsLoading)
            StatusMessage = string.Format(_loc.T("radio.tracksReady"), Tracks.Count);
    }

    private void RefreshLabels()
    {
        foreach (var s in Stations)
            s.RefreshLabels();
        OnPropertyChanged(nameof(TitleLabel));
        OnPropertyChanged(nameof(SubtitleLabel));
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(LoadingLabel));
        OnPropertyChanged(nameof(EmptyLabel));
        OnPropertyChanged(nameof(ColumnTitleLabel));
        OnPropertyChanged(nameof(ColumnTimeLabel));
        OnPropertyChanged(nameof(QueueTitleLabel));
        OnPropertyChanged(nameof(SelectedStationTitle));
    }
}